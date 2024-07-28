using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable UnusedMethodReturnValue.Local

namespace Rydr.Api.Services.Services;

public class DealRequestServiceInternal : BaseInternalOnlyApiService
{
    private static readonly IDealService _dealService = RydrEnvironment.Container.Resolve<IDealService>();
    private static readonly IDealRequestService _dealRequestService = RydrEnvironment.Container.Resolve<IDealRequestService>();
    private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();

    private readonly IAuthorizeService _authorizeService;
    private readonly ICacheClient _cacheClient;
    private readonly IRydrDataService _rydrDataService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
    private readonly IElasticClient _esClient;
    private readonly IOpsNotificationService _opsNotificationService;
    private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;

    public DealRequestServiceInternal(IAuthorizeService authorizeService,
                                      ICacheClient cacheClient,
                                      IRydrDataService rydrDataService,
                                      IPublisherAccountService publisherAccountService,
                                      IServiceCacheInvalidator serviceCacheInvalidator,
                                      IElasticClient esClient,
                                      IOpsNotificationService opsNotificationService,
                                      IWorkspaceSubscriptionService workspaceSubscriptionService)
    {
        _authorizeService = authorizeService;
        _cacheClient = cacheClient;
        _rydrDataService = rydrDataService;
        _publisherAccountService = publisherAccountService;
        _serviceCacheInvalidator = serviceCacheInvalidator;
        _esClient = esClient;
        _opsNotificationService = opsNotificationService;
        _workspaceSubscriptionService = workspaceSubscriptionService;
    }

    public async Task Post(CheckDealRequestAllowances request)
    {
        var dynDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.PublisherAccountId);

        if (dynDealRequest == null || dynDealRequest.IsDeleted())
        {
            return;
        }

        var dynPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

        var secondsAllowed = 0L;
        var updateToRequestStatus = DealRequestStatus.Unknown;
        var nowTimestamp = _dateTimeProvider.UtcNowTs;

        switch (dynDealRequest.RequestStatus)
        {
            case DealRequestStatus.InProgress:
                secondsAllowed = dynDealRequest.HoursAllowedInProgress * 3600;
                updateToRequestStatus = DealRequestStatus.Cancelled;

                break;

            case DealRequestStatus.Redeemed:
                secondsAllowed = dynDealRequest.HoursAllowedRedeemed * 3600;
                updateToRequestStatus = DealRequestStatus.Cancelled;

                break;

            default:
                _log.DebugInfoFormat("Request for deal [{0}] from publisher [{1}] is not in a status that is restricted by time allowances.", dynDealRequest.DealId, dynPublisherAccount.DisplayName());

                // Use a seconds allowed that will never pass
                secondsAllowed = nowTimestamp * 2;

                return;
        }

        if (secondsAllowed <= 0)
        {
            _log.DebugInfoFormat("Request for deal [{0}] from publisher [{1}] is not restricted by time allowance in current status of [{2}].", dynDealRequest.DealId, dynPublisherAccount.DisplayName(), dynDealRequest.RequestStatus);

            return;
        }

        var dealRequestStatusLimitAt = dynDealRequest.ReferenceId.ToLong(0) + secondsAllowed;
        var currentStatusSecondsRemaining = dealRequestStatusLimitAt - nowTimestamp;

        if (currentStatusSecondsRemaining > 0)
        {
            _log.DebugInfoFormat("Request for deal [{0}] from publisher [{1}] has [{2}] seconds remaining in current status of [{3}] before reaching allowance limit.",
                                 dynDealRequest.DealId, dynPublisherAccount.DisplayName(), currentStatusSecondsRemaining, dynDealRequest.RequestStatus);

            if (request.DeferAsAffectedOnPass)
            {
                _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                     {
                                                         CompositeIds = new List<DynamoItemIdEdge>
                                                                        {
                                                                            new(dynDealRequest.Id, dynDealRequest.EdgeId)
                                                                        },
                                                         Type = RecordType.DealRequest
                                                     });
            }

            return;
        }

        // Allowed time in current status has expired
        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        _deferRequestsService.DeferDealRequest(new UpdateDealRequest
                                               {
                                                   DealId = dynDealRequest.DealId,
                                                   Reason = $"Time allowed for this Pact in a status of {dynDealRequest.RequestStatus.ToString()} has expired.",
                                                   UpdatedByPublisherAccountId = dynDealRequest.DealPublisherAccountId,
                                                   Model = new DealRequest
                                                           {
                                                               DealId = dynDealRequest.DealId,
                                                               PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                               Status = updateToRequestStatus
                                                           },

                                                   // Populate this request as if it originated from the deal owner...
                                                   WorkspaceId = dynDeal.WorkspaceId,
                                                   RequestPublisherAccountId = dynDeal.PublisherAccountId
                                               });
    }

    public async Task Post(DeleteDealRequestInternal request)
    {
        var existingDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.PublisherAccountId);

        var dealRequestUpdatedModel = new DealRequestUpdated
                                      {
                                          DealId = existingDealRequest.DealId,
                                          PublisherAccountId = existingDealRequest.PublisherAccountId,
                                          FromStatus = existingDealRequest.RequestStatus,
                                          ToStatus = DealRequestStatus.Cancelled,
                                          UpdatedByPublisherAccountId = request.ToPublisherAccountId(),
                                          OccurredOn = _dateTimeProvider.UtcNowTs,
                                          Reason = request.Reason.Coalesce("Removed with unknown reason")
                                      };

        // Deleting the request effectively needs to cancel it from a status perspective
        await _dealRequestService.UpdateDealRequestAsync(existingDealRequest.DealId, existingDealRequest.PublisherAccountId, DealRequestStatus.Cancelled, 0, 0);

        // Then soft-delete the record
        await _dynamoDb.SoftDeleteAsync(existingDealRequest, request);
        _deferRequestsService.DeferPrimaryDealRequest(dealRequestUpdatedModel);
    }

    public async Task Post(DealRequested request)
    {
        var dynDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.RequestedByPublisherAccountId);

        await UpdateEsDealRequestedByAsync(dynDealRequest);

        _deferRequestsService.DeferDealRequest(request.CreateCopy<DealRequestedLow>());

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(dynDealRequest.Id, dynDealRequest.EdgeId)
                                                                },
                                                 Type = RecordType.DealRequest
                                             });

        _deferRequestsService.DeferDealRequest(new DealRequestStatusUpdated
                                               {
                                                   DealId = dynDealRequest.DealId,
                                                   PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                   FromStatus = DealRequestStatus.Unknown,
                                                   ToStatus = DealRequestStatus.Requested,
                                                   OccurredOn = dynDealRequest.CreatedOnUtc,
                                                   UpdatedByPublisherAccountId = request.RequestedByPublisherAccountId
                                               });
    }

    public async Task Post(DealRequestedLow request)
    {
        var dynDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.RequestedByPublisherAccountId);
        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        // Deal requester is contacting the deal creator
        await _authorizeService.AuthorizeAsync(dynDealRequest.PublisherAccountId, dynDeal.PublisherAccountId, PublisherAccountConnectionType.Contacted.ToString());

        // Deal creator being contaced by the requester
        await _authorizeService.AuthorizeAsync(dynDeal.PublisherAccountId, dynDealRequest.PublisherAccountId, PublisherAccountConnectionType.ContactedBy.ToString());

        // Flush the recent pending request list for this deal
        // NOTE: Flush this is the correct action (vs. removing from a list or something), as we populate the entire object from a query on request for it if it is not cached
        _cacheClient.TryRemove<List<PublisherAccountProfile>>(string.Concat("RecentPendingRequests|", request.DealId));

        // Put in a map from this requester signifying they have an active request for a given deal group
        if (dynDeal.DealGroupId.HasValue())
        {
            await MapItemService.DefaultMapItemService
                                .PutMapAsync(new DynItemMap
                                             {
                                                 Id = dynDealRequest.PublisherAccountId,
                                                 EdgeId = DynItemMap.BuildEdgeId(DynItemType.DealGroup, string.Concat("active|", dynDeal.DealGroupId))
                                             });
        }
    }

    public async Task Post(DealRequestStatusUpdated request)
    {
        if (request.ToStatus == DealRequestStatus.Unknown || request.FromStatus == request.ToStatus)
        {
            return;
        }

        var dynDeal = await _dealService.GetDealAsync(request.DealId, true);
        var dynDealRequest = await _dealRequestService.GetDealRequestAsync(dynDeal.DealId, request.PublisherAccountId);

        // Log the status change
        var occurredOn = request.OccurredOn.Gz(_dateTimeProvider.UtcNowTs);

        var dealRequestStatusChange = new DynDealRequestStatusChange
                                      {
                                          DealId = request.DealId,
                                          EdgeId = DynDealRequestStatusChange.BuildEdgeId(request.ToStatus, request.PublisherAccountId),
                                          PublisherAccountId = request.PublisherAccountId,
                                          FromDealRequestStatus = request.FromStatus,
                                          ToDealRequestStatus = request.ToStatus,
                                          OccurredOn = occurredOn,
                                          ModifiedByPublisherAccountId = request.UpdatedByPublisherAccountId.Gz(request.PublisherAccountId),
                                          Reason = request.Reason.ToNullIfEmpty(),
                                          DynItemType = DynItemType.DealRequestStatusChange,
                                          Latitude = request.Latitude,
                                          Longitude = request.Longitude,
                                          ReferenceId = string.Concat(DynDealRequestStatusChange.BuildReferecneIdPrefix(request.ToStatus), occurredOn),
                                          OwnerId = dynDeal.PublisherAccountId,
                                          WorkspaceId = dynDeal.WorkspaceId
                                      };

        dealRequestStatusChange.UpdateDateTimeTrackedValues(request);

        await _dynamoDb.PutItemAsync(dealRequestStatusChange);

        // Notifications for status changes
        var updatedByDealOwner = request.UpdatedByPublisherAccountId == dynDeal.PublisherAccountId;

        RecordTypeId msgFromRecordTypeId = null;
        RecordTypeId msgToRecordTypeId = null;

        // Msg is from the deal object if updated by the deal owner, and to the requester
        //    UNLESS this is a move to redemption, in which case the approvalNotes are sent as the message, which should come from the dealOwner
        // If updated by the requester (i.e. accepted an invite), msg is from the requester and to the deal object
        if (updatedByDealOwner || request.ToStatus == DealRequestStatus.Redeemed)
        {
            msgFromRecordTypeId = new RecordTypeId(RecordType.Deal, dynDeal.DealId);
            msgToRecordTypeId = new RecordTypeId(RecordType.PublisherAccount, dynDealRequest.PublisherAccountId);
        }
        else
        {
            msgFromRecordTypeId = new RecordTypeId(RecordType.PublisherAccount, dynDealRequest.PublisherAccountId);
            msgToRecordTypeId = new RecordTypeId(RecordType.Deal, dynDeal.DealId);
        }

        string msgReason = null;
        string opsNotificationMessage = null;

        // ReSharper disable once SwitchStatementMissingSomeCases
        switch (request.ToStatus)
        {
            case DealRequestStatus.InProgress:
                // 2-way agreement to deal together
                await _authorizeService.AuthorizeDuplexedAsync(dynDeal.PublisherAccountId, request.PublisherAccountId, PublisherAccountConnectionType.DealtWith.ToString());

                // Changes to in-progress are made by the owner if accepting a request, or by the creator if accepting an invite...
                // NOTE: We no longer stick on an InProgress status, it's ephemeral - so we do not send notifications about it any longer,
                //     goes to redeemed directly on approval now...
                // if (updatedByDealOwner)
                // {
                //     await _dealService.SendDealNotificationAsync(dynDeal.PublisherAccountId, request.PublisherAccountId, request.DealId,
                //                                                  "Appproved emoji.PartyPopper", $"fromPublisherAccount.UserName: {dynDeal.Title}",
                //                                                  ServerNotificationType.DealRequestApproved, dynDealRequest.DealWorkspaceId);
                // }
                // else
                // {
                //     await _dealService.SendDealNotificationAsync(request.PublisherAccountId, dynDeal.PublisherAccountId, request.DealId,
                //                                                  "Accepted emoji.PartyPopper", $"fromPublisherAccount.UserName: {dynDeal.Title}",
                //                                                  ServerNotificationType.DealRequestApproved, dynDealRequest.DealWorkspaceId);
                // }

                break;

            case DealRequestStatus.Denied:
                // Changes to denied have to have been made by the deal owner, notify the requestor of the deal on behalf of the deal owner
                await _dealService.SendDealNotificationAsync(dynDeal.PublisherAccountId, request.PublisherAccountId, request.DealId,
                                                             "Declined", $"fromPublisherAccount.UserName: {dynDeal.Title}",
                                                             ServerNotificationType.DealRequestDenied, dynDealRequest.DealWorkspaceId);

                msgReason = request.Reason?.Trim();

                opsNotificationMessage = "Deal Request Denied";

                break;

            case DealRequestStatus.Cancelled:
                // Either side of a deal can cancel a request
                await _dealService.SendDealNotificationAsync(updatedByDealOwner
                                                                 ? dynDeal.PublisherAccountId
                                                                 : request.PublisherAccountId,
                                                             updatedByDealOwner
                                                                 ? request.PublisherAccountId
                                                                 : dynDeal.PublisherAccountId,
                                                             request.DealId,
                                                             "Cancelled", $"fromPublisherAccount.UserName: {dynDeal.Title}",
                                                             ServerNotificationType.DealRequestCancelled,
                                                             dynDealRequest.DealWorkspaceId);

                msgReason = request.Reason?.Trim();

                opsNotificationMessage = "Deal Request Cancelled";

                break;

            case DealRequestStatus.Completed:
                // Either side of a deal can complete a request
                await _dealService.SendDealNotificationAsync(updatedByDealOwner
                                                                 ? dynDeal.PublisherAccountId
                                                                 : request.PublisherAccountId,
                                                             updatedByDealOwner
                                                                 ? request.PublisherAccountId
                                                                 : dynDeal.PublisherAccountId,
                                                             request.DealId,
                                                             "Completed emoji.PartyPopper", $"fromPublisherAccount.UserName: {dynDeal.Title}",
                                                             ServerNotificationType.DealRequestCompleted,
                                                             dynDealRequest.DealWorkspaceId);

                msgReason = request.Reason?.Trim();

                opsNotificationMessage = "Deal Request Completed";

                break;

            case DealRequestStatus.Requested:
                // If auto approving requests, do so
                // If not being auto-approved, send a notification to the deal owner that they've received a request for their deal
                if (dynDeal.AutoApproveRequests)
                { // Only actually move to inProgress if the deal request is still in the requested state (can occurr synchronously sometimes in-band with a user request)
                    if (dynDealRequest.RequestStatus == DealRequestStatus.Requested)
                    {
                        _deferRequestsService.DeferDealRequest(new UpdateDealRequest
                                                               {
                                                                   DealId = dynDealRequest.DealId,
                                                                   Reason = "Auto approved",
                                                                   UpdatedByPublisherAccountId = dynDeal.PublisherAccountId,
                                                                   Model = new DealRequest
                                                                           {
                                                                               DealId = dynDealRequest.DealId,
                                                                               PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                                               Status = DealRequestStatus.InProgress
                                                                           },

                                                                   // Populate this request as if it originated from the deal owner...
                                                                   WorkspaceId = dynDeal.WorkspaceId,
                                                                   RequestPublisherAccountId = dynDeal.PublisherAccountId
                                                               });
                    }
                }
                else
                {
                    await _dealService.SendDealNotificationAsync(request.PublisherAccountId, dynDeal.PublisherAccountId, dynDeal.DealId,
                                                                 "Request from fromPublisherAccount.UserName", dynDeal.Title,
                                                                 ServerNotificationType.DealRequested, dynDealRequest.DealWorkspaceId);

                    opsNotificationMessage = "Deal Requested";
                }

                break;

            case DealRequestStatus.Redeemed:
                // Either side of a deal can redeem...
                await _dealService.SendDealNotificationAsync(updatedByDealOwner
                                                                 ? dynDeal.PublisherAccountId
                                                                 : request.PublisherAccountId,
                                                             updatedByDealOwner
                                                                 ? request.PublisherAccountId
                                                                 : dynDeal.PublisherAccountId,
                                                             request.DealId, updatedByDealOwner
                                                                                 ? "Appproved emoji.PartyPopper" // "Redeemed emoji.Handshake" - since InProgress is now ephemeral...
                                                                                 : "Claimed by fromPublisherAccount.UserName",
                                                             dynDeal.Title, ServerNotificationType.DealRequestRedeemed, dynDealRequest.DealWorkspaceId);

                var inProgressStatusChange = dynDeal.AutoApproveRequests
                                                 ? null
                                                 : await _dynamoDb.GetItemAsync<DynDealRequestStatusChange>(dynDeal.DealId,
                                                                                                            DynDealRequestStatusChange.BuildEdgeId(DealRequestStatus.InProgress,
                                                                                                                                                   request.PublisherAccountId));

                msgReason = (dynDeal.AutoApproveRequests
                                 ? dynDeal.ApprovalNotes.ToString()
                                 : (inProgressStatusChange?.Reason).Coalesce(dynDeal.ApprovalNotes.ToString()))?.Trim();

                opsNotificationMessage = "Deal Request Redeemed";

                break;

            case DealRequestStatus.Delinquent:
                // Only the business / deal creator can set a deal delinquent
                await _dealService.SendDealNotificationAsync(dynDeal.PublisherAccountId, request.PublisherAccountId, request.DealId,
                                                             "Delinquent", $"fromPublisherAccount.UserName: {dynDeal.Title}",
                                                             ServerNotificationType.DealRequestDelinquent, dynDealRequest.DealWorkspaceId);

                msgReason = (request.Reason?.Trim()).Coalesce("You are now delinquent on this Pact - please complete it as soon as possible.");

                break;

            case DealRequestStatus.Invited:
                await _dealService.SendDealNotificationAsync(dynDeal.PublisherAccountId, request.PublisherAccountId, dynDeal.DealId,
                                                             "Invitation from fromPublisherAccount.UserName", dynDeal.Title,
                                                             ServerNotificationType.DealInvited, dynDealRequest.DealWorkspaceId);

                break;

            default:
                throw new ArgumentOutOfRangeException($"Unhanlded DealRequestStatus of [{request.ToStatus.ToString()}] in DealRequestStatusUpdated processing");
        }

        if (msgReason.HasValue())
        {
            _deferRequestsService.DeferRequest(new PostMessage
                                               {
                                                   From = msgFromRecordTypeId,
                                                   To = msgToRecordTypeId,
                                                   Message = msgReason
                                               }.PopulateWithRequestInfo(request));
        }

        if (opsNotificationMessage.HasValue())
        {
            var dealRequestPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(dynDealRequest.PublisherAccountId);
            var dealPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(dynDeal.PublisherAccountId);

            var location = request.ToStatus == DealRequestStatus.Requested
                               ? (await _dynamoDb.TryGetPlaceAsync(dynDeal.ReceivePlaceId.Gz(dynDeal.PlaceId))).ToDisplayLocation()
                               : null;

            var externalUrl = $"https://cdn.getrydr.com/x/{dynDeal.ToDealPublicLinkId()}";

            await _opsNotificationService.TrySendAppNotificationAsync($"[{opsNotificationMessage}] ({dynDeal.DealId})", string.Concat($@"
Deal Name :      {dynDeal.Title}
Creator   :      <https://instagram.com/{dealRequestPublisherAccount.UserName}|IG: {dealRequestPublisherAccount.UserName}> ({dealRequestPublisherAccount.PublisherAccountId})
Business  :      <https://instagram.com/{dealPublisherAccount.UserName}|IG: {dealPublisherAccount.UserName}> ({dealPublisherAccount.PublisherAccountId})",
                                                                                                                                      location.IsNullOrEmpty()
                                                                                                                                          ? null
                                                                                                                                          : @"
Location :       
", location, $@"

<{externalUrl}|View on the web>

<https://in.getrydr.com/share/?link={externalUrl}&apn=com.rydr.app&ibi=com.rydr.app&isi=1480064664&ofl=https://onelink.to/fsnv3y&efr=1|View in PostPact>
"));
        }
    }

    public async Task Post(UpdateDealRequest request)
    {
        var publisherAccountId = request.Model.PublisherAccountId.Gz(request.RequestPublisherAccountId);

        var existingDealRequest = await _dealRequestService.GetDealRequestAsync(request.Model.DealId, publisherAccountId);

        var dealRequestUpdatedModel = new DealRequestUpdated
                                      {
                                          DealId = existingDealRequest.DealId,
                                          PublisherAccountId = existingDealRequest.PublisherAccountId,
                                          FromStatus = existingDealRequest.RequestStatus,
                                          ToStatus = request.Model.Status,
                                          UpdatedByPublisherAccountId = request.UpdatedByPublisherAccountId.Gz(request.RequestPublisherAccountId),
                                          OccurredOn = DateTimeHelper.UtcNowTs,
                                          Reason = request.Reason,
                                          Latitude = request.Latitude,
                                          Longitude = request.Longitude,
                                          HoursAllowedRedeemed = request.Model.HoursAllowedRedeemed,
                                          HoursAllowedInProgress = request.Model.HoursAllowedInProgress
                                      };

        var dealStatType = request.Model.Status == DealRequestStatus.Unknown
                               ? DealStatType.Unknown
                               : request.Model.Status.ToStatType();

        var fromDealStatType = existingDealRequest.RequestStatus.ToStatType();

        await _dealRequestService.UpdateDealRequestAsync(existingDealRequest.DealId, existingDealRequest.PublisherAccountId,
                                                         request.Model.Status, request.Model.HoursAllowedInProgress, request.Model.HoursAllowedRedeemed);

        _deferRequestsService.DeferPrimaryDealRequest(dealRequestUpdatedModel);

        if (request.Model.Status != DealRequestStatus.Unknown && request.Model.Status != existingDealRequest.RequestStatus &&
            dealStatType != DealStatType.Unknown && dealStatType != fromDealStatType)
        {
            _deferRequestsService.DeferFifoRequest(new DealStatIncrement
                                                   {
                                                       DealId = existingDealRequest.DealId,
                                                       FromStatType = fromDealStatType,
                                                       StatType = dealStatType,
                                                       FromPublisherAccountId = existingDealRequest.PublisherAccountId
                                                   });
        }

        // InProgress is an ephemeral status nowadays, always move directly from that to redeemed. If the creator (requestor) is making the update, have
        // to perform the status change synchronously...otherwise, can do it asynchronously
        if (request.Model.Status == DealRequestStatus.InProgress)
        {
            var dynDeal = await _dealService.GetDealAsync(existingDealRequest.DealId);

            var updateDealRequest = new UpdateDealRequest
                                    {
                                        DealId = dynDeal.DealId,
                                        Reason = "Auto redeemed",
                                        UpdatedByPublisherAccountId = existingDealRequest.PublisherAccountId, // Requestor is the updater when auto redeeming a deal
                                        Model = new DealRequest
                                                {
                                                    DealId = dynDeal.DealId,
                                                    PublisherAccountId = existingDealRequest.PublisherAccountId,
                                                    Status = DealRequestStatus.Redeemed
                                                },
                                        WorkspaceId = dynDeal.WorkspaceId,
                                        RequestPublisherAccountId = dynDeal.PublisherAccountId
                                    };

            if (dynDeal.AutoApproveRequests || dealRequestUpdatedModel.UpdatedByPublisherAccountId == existingDealRequest.PublisherAccountId)
            {
                await _adminServiceGatewayFactory().SendAsync(updateDealRequest);
            }
            else
            {
                _deferRequestsService.DeferDealRequest(updateDealRequest);
            }
        }
    }

    public async Task Post(DealRequestUpdated request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId, true);
        var dynDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.PublisherAccountId);

        // If no longer in a requested status, remove from the recent request set
        if (request.ToStatus != DealRequestStatus.Unknown && !request.ToStatus.IsPendingRequest())
        { // Flush the recent pending request list for this deal
            _cacheClient.TryRemove<List<PublisherAccountProfile>>(string.Concat("RecentPendingRequests|", request.DealId));
        }

        // If no longer an 'active' group, remove the map if this deal is part of a group
        if (dynDeal.DealGroupId.HasValue() && request.ToStatus != DealRequestStatus.Unknown &&
            (request.ToStatus.IsAfterRedeemed() || dynDealRequest.RequestStatus.IsAfterRedeemed()))
        {
            await MapItemService.DefaultMapItemService
                                .DeleteMapAsync(dynDealRequest.PublisherAccountId, DynItemMap.BuildEdgeId(DynItemType.DealGroup, string.Concat("active|", dynDeal.DealGroupId)));
        }

        // Update status if deleted - purposely not sending notifications/status updates here, they'd already be handled
        if (dynDealRequest.IsDeleted() && dynDealRequest.RequestStatus != DealRequestStatus.Cancelled)
        {
            await _dynamoDb.PutItemTrackedInterlockedDeferAsync(dynDealRequest, dr => dr.RequestStatus = DealRequestStatus.Cancelled, RecordType.DealRequest);
        }

        // If the deal is completed or being set to completed, update the recent deal completion stats for each of the requesting publisher and deal owner publisher
        if (request.ToStatus == DealRequestStatus.Completed || request.FromStatus == DealRequestStatus.Completed || dynDealRequest.RequestStatus == DealRequestStatus.Completed)
        {
            _deferRequestsService.DeferLowPriRequest(new PublisherAccountRecentDealStatsUpdate
                                                     {
                                                         PublisherAccountId = dynDeal.PublisherAccountId,
                                                         InWorkspaceId = dynDeal.WorkspaceId
                                                     });

            _deferRequestsService.DeferLowPriRequest(new PublisherAccountRecentDealStatsUpdate
                                                     {
                                                         PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                         InWorkspaceId = dynDealRequest.DealWorkspaceId
                                                     });
        }

        if (request.FromStatus == DealRequestStatus.Invited)
        { // If moving from an invited status, add this as a "requestedby" publisher in es - this is treated as the same as a person manually requesting a deal,
            // which for one stops showing it in the published/available map for an influencer. When invited, the person accepting the invite (or otherwise acting on it)
            // is what acts as the "requestedby" action
            await UpdateEsDealRequestedByAsync(dynDealRequest);
        }

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(dynDealRequest.Id, dynDealRequest.EdgeId)
                                                                },
                                                 Type = RecordType.DealRequest
                                             });

        if (request.FromStatus != request.ToStatus && request.ToStatus != DealRequestStatus.Unknown)
        {
            _deferRequestsService.DeferDealRequest(new DealRequestStatusUpdated
                                                   {
                                                       DealId = request.DealId,
                                                       PublisherAccountId = request.PublisherAccountId,
                                                       FromStatus = request.FromStatus,
                                                       ToStatus = request.ToStatus,
                                                       OccurredOn = request.OccurredOn.Gz(dynDealRequest.ModifiedOnUtc),
                                                       Reason = request.Reason,
                                                       UpdatedByPublisherAccountId = request.UpdatedByPublisherAccountId,
                                                       Latitude = request.Latitude,
                                                       Longitude = request.Longitude
                                                   });
        }
    }

    public async Task Post(DealRequestCompletionMediaSubmitted request)
    {
        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);
        var existingDealRequest = await _dealRequestService.GetDealRequestAsync(request.DealId, request.PublisherAccountId);

        var publisherMediaSyncService = RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(publisherAccount.PublisherType.ToString());

        // Ensure we have synced copies of each of these items and mark them to remain pinned
        var dynPublisherMediaIds = request.CompletionMediaPublisherMediaIds.IsNullOrEmpty()
                                       ? request.CompletionRydrMediaIds.AsHashSet()
                                       : (await publisherMediaSyncService.SyncMediaAsync(request.CompletionMediaPublisherMediaIds, publisherAccount.PublisherAccountId, true)).AsHashSet();

        if (!request.CompletionRydrMediaIds.IsNullOrEmpty() && !request.CompletionMediaPublisherMediaIds.IsNullOrEmpty())
        {
            dynPublisherMediaIds.UnionWith(request.CompletionRydrMediaIds);
        }

        Guard.AgainstInvalidData(dynPublisherMediaIds.IsNullOrEmpty(),
                                 $"Failed to sync CompletionMedia for DealRequest dealId [{request.DealId}], publisherAccountId [{request.PublisherAccountId}] - dyn count [{dynPublisherMediaIds.Count}]");

        var chargedUsage = await _workspaceSubscriptionService.ChargeCompletedRequestUsageAsync(existingDealRequest);

        // Update the tracked IDs in dynamo
        await _dynamoDb.PutItemTrackedInterlockedDeferAsync(existingDealRequest, ddr =>
                                                                                 {
                                                                                     ddr.CompletionMediaIds = dynPublisherMediaIds;

                                                                                     ddr.UsageChargedOn = chargedUsage
                                                                                                              ? _dateTimeProvider.UtcNowTs
                                                                                                              : ddr.UsageChargedOn;
                                                                                 },
                                                            RecordType.DealRequest);

        var dynPublisherMedias = await _dynamoDb.QueryItemsAsync<DynPublisherMedia>(dynPublisherMediaIds.Select(mid => new DynamoId(publisherAccount.PublisherAccountId, mid.ToEdgeId())))
                                                .ToList(dynPublisherMediaIds.Count);

        await _rydrDataService.SaveRangeAsync(dynPublisherMedias.Select(pm => new RydrDealRequestMedia
                                                                              {
                                                                                  DealId = existingDealRequest.DealId,
                                                                                  PublisherAccountId = existingDealRequest.PublisherAccountId,
                                                                                  MediaId = pm.PublisherMediaId,
                                                                                  MediaType = pm.MediaType,
                                                                                  ContentType = pm.ContentType,
                                                                              }),
                                              rm => rm.Id);

        _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                           {
                                               PublisherAccountId = publisherAccount.PublisherAccountId,
                                               PublisherMediaIds = dynPublisherMediaIds.AsList(),
                                               StoreAsPermanentMedia = true,
                                               IsCompletionMedia = true
                                           }.WithAdminRequestInfo());

        // Flush the completion stats endpoint for the deal owners publisher account
        await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(existingDealRequest.DealPublisherAccountId, "dealmetrics");
    }

    private async Task UpdateEsDealRequestedByAsync(DynDealRequest dynDealRequest)
        => await _esClient.UpdateAsync(EsDeal.GetDocumentPath(dynDealRequest.DealId),
                                       d => d.Script(sd => sd.Source(@"
if (!ctx._source.containsKey(""requestedByPublisherAccountIds"")) {
    ctx._source.requestedByPublisherAccountIds = []; 
}

if (!ctx._source.requestedByPublisherAccountIds.contains(params.reqPubAcctId)) {
    ctx._source.requestedByPublisherAccountIds.add(params.reqPubAcctId);
} else {
    ctx.op = 'none';
}
")
                                                             .Params(pd => pd.Add("reqPubAcctId", dynDealRequest.PublisherAccountId))
                                                             .Lang(ScriptLang.Painless))
                                             .Index(ElasticIndexes.DealsAlias)
                                             .DetectNoop());
}
