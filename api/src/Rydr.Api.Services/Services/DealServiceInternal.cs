using System.Text;
using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable UnusedMethodReturnValue.Local

namespace Rydr.Api.Services.Services;

public class DealServiceInternal : BaseInternalOnlyApiService
{
    private const int _secondsToKeepPublicDealLinkValid = 60 * 60 * 24 * 180; // 180 days

    private static readonly string _externalDealPath = RydrEnvironment.GetAppSetting("Deals.ExternalPath", "rydr-cdn-dev/x");

    private readonly IDealService _dealService;
    private readonly IDealRequestService _dealRequestService;
    private readonly IPersistentCounterAndListService _counterAndListService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IElasticClient _esClient;
    private readonly IAssociationService _associationService;
    private readonly IAuthorizeService _authorizeService;
    private readonly IDealMetricService _dealMetricService;
    private readonly ICacheClient _cacheClient;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IOpsNotificationService _opsNotificationService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IRydrDataService _rydrDataService;

    public DealServiceInternal(IDealService dealService,
                               IDealRequestService dealRequestService,
                               IPersistentCounterAndListService counterAndListService,
                               IDeferRequestsService deferRequestsService,
                               IElasticClient esClient,
                               IAssociationService associationService,
                               IAuthorizeService authorizeService,
                               IDealMetricService dealMetricService,
                               ICacheClient cacheClient,
                               IFileStorageProvider fileStorageProvider,
                               IOpsNotificationService opsNotificationService,
                               IPublisherAccountService publisherAccountService,
                               IRydrDataService rydrDataService)
    {
        _dealService = dealService;
        _dealRequestService = dealRequestService;
        _counterAndListService = counterAndListService;
        _deferRequestsService = deferRequestsService;
        _esClient = esClient;
        _associationService = associationService;
        _authorizeService = authorizeService;
        _dealMetricService = dealMetricService;
        _cacheClient = cacheClient;
        _fileStorageProvider = fileStorageProvider;
        _opsNotificationService = opsNotificationService;
        _publisherAccountService = publisherAccountService;
        _rydrDataService = rydrDataService;
    }

    public async Task Post(UpdateExternalDeal request)
    {
        var (deal, dealHtml, dealLink) = await DealPublicService.GetDealExternalHtmlAsync(request.DealId);

        if (deal == null || dealLink.IsNullOrEmpty())
        {
            return;
        }

        var isDeleted = deal.DeletedOn.HasValue || deal.Status == DealStatus.Deleted;

        var externalDealFileMeta = new FileMetaData(_externalDealPath, dealLink)
                                   {
                                       Bytes = isDeleted
                                                   ? null
                                                   : dealHtml.CompressGzip(Encoding.UTF8),
                                       Tags =
                                       {
                                           {
                                               FileStorageTag.Lifecycle.ToString(), FileStorageTags.LifecyclePurge
                                           }
                                       }
                                   };

        if (isDeleted)
        {
            await Try.ExecAsync(() => _fileStorageProvider.DeleteAsync(externalDealFileMeta));
        }
        else
        {
            await _fileStorageProvider.StoreAsync(externalDealFileMeta, new FileStorageOptions
                                                                        {
                                                                            ContentEncoding = "gzip",
                                                                            ContentType = "text/html; charset=UTF-8",
                                                                            Encrypt = true,
                                                                            StorageClass = FileStorageClass.Intelligent
                                                                        });
        }
    }

    public async Task Post(DeleteDealInternal request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId, true);

        if (dynDeal == null || dynDeal.IsDeleted())
        {
            return;
        }

        var dealUpdatedModel = new DealUpdated
                               {
                                   DealId = request.DealId,
                                   FromStatus = dynDeal.DealStatus,
                                   ToStatus = DealStatus.Deleted,
                                   Reason = request.Reason.Coalesce("Deleted without reason"),
                                   OccurredOn = _dateTimeProvider.UtcNowTs
                               };

        dynDeal.DealStatus = DealStatus.Deleted;

        await _dynamoDb.SoftDeleteAsync(dynDeal, request);

        _deferRequestsService.DeferPrimaryDealRequest(dealUpdatedModel);
    }

    public async Task Post(DealStatIncrement request)
    {
        if (request.StatType == DealStatType.Unknown)
        {
            return;
        }

        if (request.StatType == request.FromStatType)
        {
            _log.WarnFormat("From and To DealStatTypes are the same - should not happen. DealId [{0}], statType [{1}]", request.DealId, request.StatType.ToString());

            return;
        }

        await _dealService.ProcesDealStatsAsync(request.DealId, request.FromPublisherAccountId, request.StatType, request.FromStatType);

        _deferRequestsService.TryDeferDealRequest(new DealStatIncremented
                                                  {
                                                      DealId = request.DealId,
                                                      StatType = request.StatType,
                                                      FromStatType = request.FromStatType,
                                                      FromPublisherAccountId = request.FromPublisherAccountId
                                                  });
    }

    public async Task Post(DealStatIncremented request)
    {
        var trackMetricType = request.StatType switch
                              {
                                  DealStatType.TotalRequests => DealTrackMetricType.Requested,
                                  DealStatType.TotalDenied => DealTrackMetricType.RequestDenied,
                                  DealStatType.TotalCompleted => DealTrackMetricType.RequestCompleted,
                                  DealStatType.TotalCancelled => DealTrackMetricType.RequestCancelled,
                                  DealStatType.TotalApproved => DealTrackMetricType.RequestApproved,
                                  DealStatType.TotalInvites => DealTrackMetricType.Invited,
                                  DealStatType.TotalRedeemed => DealTrackMetricType.RequestRedeemed,
                                  _ => DealTrackMetricType.Unknown
                              };

        if (trackMetricType == DealTrackMetricType.Unknown)
        {
            return;
        }

        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        // If we approved a request, and the deal is still published, ensure it has not yet met its maxApproval setting
        // If it has, turn it to completed
        if (trackMetricType == DealTrackMetricType.RequestApproved && !dynDeal.IsDeleted() && dynDeal.DealStatus == DealStatus.Published)
        {
            var totalApproved = await _dealService.GetDealStatAsync(dynDeal.DealId, dynDeal.PublisherAccountId, DealStatType.TotalApproved);

            if (totalApproved.Cnt.GetValueOrDefault() >= dynDeal.ApprovalLimit)
            {
                var fromStatus = dynDeal.DealStatus;

                await _dynamoDb.PutItemTrackedInterlockedDeferAsync(dynDeal, d => d.DealStatus = DealStatus.Completed, RecordType.Deal);

                await _dealService.SendDealNotificationAsync(0, dynDeal.PublisherAccountId, dynDeal.DealId,
                                                             "Completed emoji.Checkmark", $"toPublisherAccount.UserName: {dynDeal.Title} quantity has been used up",
                                                             ServerNotificationType.DealCompleted, dynDeal.WorkspaceId);

                _deferRequestsService.DeferPrimaryDealRequest(new DealStatusUpdated
                                                              {
                                                                  DealId = dynDeal.DealId,
                                                                  FromStatus = fromStatus,
                                                                  ToStatus = DealStatus.Completed,
                                                                  OccurredOn = _dateTimeProvider.UtcNowTs,
                                                                  Reason = "Deal max approval count met"
                                                              });
            }
        }

        _dealMetricService.Measure(trackMetricType, dynDeal, request.FromPublisherAccountId, request.WorkspaceId, request.UserId);
    }

    public async Task Post(DealPosted request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        await UpdateEsDealAsync(dynDeal);

        // Status change and follow-up stuff
        _deferRequestsService.DeferDealRequest(new DealStatusUpdated
                                               {
                                                   DealId = dynDeal.DealId,
                                                   FromStatus = DealStatus.Unknown,
                                                   ToStatus = dynDeal.DealStatus == DealStatus.Draft || dynDeal.DealStatus == DealStatus.Published
                                                                  ? dynDeal.DealStatus
                                                                  : DealStatus.Draft,
                                                   OccurredOn = dynDeal.CreatedOnUtc,
                                                   EsUpdated = true
                                               });

        _deferRequestsService.DeferDealRequest(request.CreateCopy<DealPostedLow>());
    }

    public async Task Post(DealPostedLow request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        _dealMetricService.Measure(DealTrackMetricType.Created, dynDeal, request.WorkspaceId, request.UserId);

        // Associate the deal to an external link
        var dealPublicLinkId = dynDeal.ToDealPublicLinkId();

        if (!(await _associationService.IsAssociatedAsync(dynDeal.DealId, dealPublicLinkId)))
        {
            await _associationService.AssociateAsync(RecordType.Deal, dynDeal.DealId, RecordType.DealLink, dynDeal.ToDealPublicLinkId(),
                                                     expiresAt: (_dateTimeProvider.UtcNowTs + _secondsToKeepPublicDealLinkValid));
        }

        // Ensure the place is associated to the account
        if (dynDeal.PlaceId > 0 &&
            !(await _associationService.IsAssociatedAsync(dynDeal.PublisherAccountId, dynDeal.PlaceId)))
        {
            await _associationService.AssociateAsync(RecordType.PublisherAccount, dynDeal.PublisherAccountId, RecordType.Place, dynDeal.PlaceId);
        }

        // Ensure the receive-place is associated to the account
        if (dynDeal.ReceivePlaceId > 0 && dynDeal.ReceivePlaceId != dynDeal.PlaceId &&
            !(await _associationService.IsAssociatedAsync(dynDeal.PublisherAccountId, dynDeal.ReceivePlaceId)))
        {
            await _associationService.AssociateAsync(RecordType.PublisherAccount, dynDeal.PublisherAccountId, RecordType.Place, dynDeal.ReceivePlaceId);
        }

        // Invitees
        if (!dynDeal.InvitedPublisherAccountIds.IsNullOrEmpty())
        {
            await ProcessDealInvitesAsync(dynDeal, dynDeal.InvitedPublisherAccountIds);
        }

        if (!dynDeal.PublisherMediaIds.IsNullOrEmpty())
        {
            _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                               {
                                                   PublisherAccountId = dynDeal.PublisherAccountId,
                                                   PublisherMediaIds = dynDeal.PublisherMediaIds.AsList(),
                                                   StoreAsPermanentMedia = true
                                               }.WithAdminRequestInfo());
        }

        _deferRequestsService.DeferLowPriRequest(new UpdateExternalDeal
                                                 {
                                                     DealId = dynDeal.DealId
                                                 });

        await _rydrDataService.InsertAsync(dynDeal.ToRydrDealTextValue());

        await SendOpsNotificationAsync(dynDeal, "New Deal Created");
    }

    public async Task Post(DealStatusUpdated request)
    {
        var setReferenceIdToNull = false;
        var setDealStatusTo = DealStatus.Unknown;
        var dynDeal = await _dealService.GetDealAsync(request.DealId, true);

        // Only published/paused deals have referenceId values
        if (dynDeal.DealStatus != DealStatus.Published && dynDeal.DealStatus != DealStatus.Paused && dynDeal.ReferenceId.HasValue())
        {
            setReferenceIdToNull = true;
        }

        // Update all non-final-state requests to cancelled (no handling for InProgress here as you should not be able to delete/cancel a deal if there
        // are outstanding InProgress requests)
        if (dynDeal.IsDeleted() || (request.ToStatus == DealStatus.Deleted && dynDeal.DealStatus != DealStatus.Deleted))
        {
            setDealStatusTo = DealStatus.Deleted;
        }

        // Update the item in dynamo
        if (setDealStatusTo != DealStatus.Unknown || setReferenceIdToNull)
        {
            await _dynamoDb.PutItemTrackedInterlockedDeferAsync(dynDeal,
                                                                d =>
                                                                {
                                                                    if (setReferenceIdToNull)
                                                                    {
                                                                        d.ReferenceId = null;
                                                                    }

                                                                    if (setDealStatusTo != DealStatus.Unknown)
                                                                    {
                                                                        d.DealStatus = setDealStatusTo;
                                                                    }
                                                                },
                                                                RecordType.Deal);
        }

        // Update the status in ES
        if (setDealStatusTo != DealStatus.Unknown || !request.EsUpdated)
        {
            await _esClient.UpdateAsync<EsDeal, object>(EsDeal.GetDocumentPath(dynDeal.DealId),
                                                        d => d.Doc(new
                                                                   {
                                                                       DealStatus = setDealStatusTo == DealStatus.Unknown
                                                                                        ? (int)dynDeal.DealStatus
                                                                                        : (int)setDealStatusTo
                                                                   })
                                                              .Index(ElasticIndexes.DealsAlias));
        }

        // Log the status change if needed
        if (request.ToStatus != DealStatus.Unknown)
        {
            var occurredOn = request.OccurredOn.Gz(_dateTimeProvider.UtcNowTs);

            var dealStatusChangeLog = new DynDealStatusChange
                                      {
                                          DealId = request.DealId,
                                          ToDealStatus = request.ToStatus,
                                          FromDealStatus = request.FromStatus,
                                          OccurredOn = occurredOn,
                                          ModifiedByPublisherAccountId = request.RequestPublisherAccountId,
                                          Reason = request.Reason,
                                          DynItemType = DynItemType.DealStatusChange,
                                          ReferenceId = string.Concat(DynDealStatusChange.BuildReferecneIdPrefix(request.ToStatus), occurredOn),
                                          OwnerId = dynDeal.PublisherAccountId,
                                          WorkspaceId = dynDeal.WorkspaceId
                                      };

            dealStatusChangeLog.UpdateDateTimeTrackedValues(request);

            await _dynamoDb.PutItemAsync(dealStatusChangeLog);
        }

        // If the deal is deleted or being deleted, cancel any open requests
        if (dynDeal.IsDeleted() || request.ToStatus == DealStatus.Deleted)
        {
            var activeDealRequests = await _dealRequestService.GetAllActiveDealRequestsAsync(dynDeal.DealId);

            foreach (var activeDealRequest in activeDealRequests)
            {
                _deferRequestsService.DeferDealRequest(new DeleteDealRequestInternal
                                                       {
                                                           DealId = activeDealRequest.DealId,
                                                           PublisherAccountId = activeDealRequest.PublisherAccountId,
                                                           Reason = request.Reason.Coalesce("Deal was removed by business")
                                                       });
            }
        }

        if (request.ToStatus == DealStatus.Published && request.FromStatus != DealStatus.Unknown)
        {
            await SendOpsNotificationAsync(dynDeal, "Deal Published");
        }
    }

    public async Task Post(DealUpdated request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId, true);

        await UpdateEsDealAsync(dynDeal);

        _dealMetricService.Measure(DealTrackMetricType.Updated, dynDeal, request.WorkspaceId, request.UserId);

        // Status change and follow-up stuff
        _deferRequestsService.DeferDealRequest(request.CreateCopy<DealStatusUpdated>().WithEsAlreadyUpdated());
        _deferRequestsService.DeferDealRequest(request.CreateCopy<DealUpdatedLow>());
    }

    public async Task Post(DealUpdatedLow request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId, true);

        // If the deal was updated to a published state, send out all invites, otherwise, only new invites
        await ProcessDealInvitesAsync(dynDeal, request.ToStatus == DealStatus.Published
                                                   ? dynDeal.InvitedPublisherAccountIds
                                                   : request.NewlyInvitedPublisherAccountIds);

        _deferRequestsService.DeferLowPriRequest(new UpdateExternalDeal
                                                 {
                                                     DealId = dynDeal.DealId
                                                 });

        await _rydrDataService.SaveAsync(dynDeal.ToRydrDealTextValue());

        if (!dynDeal.PublisherMediaIds.IsNullOrEmpty())
        {
            _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                               {
                                                   PublisherAccountId = dynDeal.PublisherAccountId,
                                                   PublisherMediaIds = dynDeal.PublisherMediaIds.AsList(),
                                                   StoreAsPermanentMedia = true
                                               }.WithAdminRequestInfo());
        }
    }

    private async Task UpdateEsDealAsync(DynDeal dynDeal)
    {
        var esDeal = await dynDeal.ToEsDealAsync();

        var response = await _esClient.IndexAsync(esDeal, idx => idx.Index(ElasticIndexes.DealsAlias)
                                                                    .Id(esDeal.DealId));

        if (!response.SuccessfulOnly())
        {
            throw response.ToException();
        }
    }

    private async Task SendOpsNotificationAsync(DynDeal dynDeal, string message)
    {
        var dealPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(dynDeal.PublisherAccountId);
        var location = (await _dynamoDb.TryGetPlaceAsync(dynDeal.ReceivePlaceId.Gz(dynDeal.PlaceId))).ToDisplayLocation();
        var externalUrl = $"https://cdn.getrydr.com/x/{dynDeal.ToDealPublicLinkId()}";

        await _opsNotificationService.TrySendAppNotificationAsync($"{message} ({dynDeal.DealId})", string.Concat($@"
Name :           {dynDeal.Title}
Status :         {dynDeal.DealStatus.ToString()}
Deal Publisher : <https://instagram.com/{dealPublisherAccount.UserName}|IG: {dealPublisherAccount.UserName}> ({dealPublisherAccount.PublisherAccountId})",
                                                                                                                 location.IsNullOrEmpty()
                                                                                                                     ? null
                                                                                                                     : @"
Location :
", location, $@"

<{externalUrl}|View on the web>

<https://in.getrydr.com/share/?link={externalUrl}&apn=com.rydr.app&ibi=com.rydr.app&isi=1480064664&ofl=https://onelink.to/fsnv3y&efr=1|View in PostPact>
"));
    }

    private async Task ProcessDealInvitesAsync(DynDeal dynDeal, IEnumerable<long> invitedPublisherAccountIds)
    {
        if (invitedPublisherAccountIds == null || dynDeal.IsDeleted() || dynDeal.DealStatus != DealStatus.Published || dynDeal.IsExpired())
        {
            return;
        }

        var invitedKey = string.Concat("DealInvites|", dynDeal.DealId);

        var isRydrInternalDeal = dynDeal.Tags != null && dynDeal.Tags.Any(t => t.Value.EqualsOrdinalCi(Tag.TagRydrInternalDeal));

        var inviteRequestIds = new List<DynamoItemIdEdge>();

        foreach (var publisherAccountId in invitedPublisherAccountIds)
        {
            // Deal creator is contacting the influencer
            await _authorizeService.AuthorizeAsync(dynDeal.PublisherAccountId, publisherAccountId, PublisherAccountConnectionType.Contacted.ToString());

            // Influencer being contacted by the creator
            await _authorizeService.AuthorizeAsync(publisherAccountId, dynDeal.PublisherAccountId, PublisherAccountConnectionType.ContactedBy.ToString());

            if (isRydrInternalDeal || _counterAndListService.Exists(invitedKey, publisherAccountId.ToStringInvariant()))
            { // Internal rydr deal or already processed, do not actually process the request/etc.
                continue;
            }

            if (!(await _dealRequestService.TryRequestDealAsync(dynDeal.DealId, publisherAccountId, true)))
            {
                _log.WarnFormat("Could not request deal for invited PublisherAccountId [{0}], deal [{1}]", publisherAccountId, dynDeal.DealId);

                continue;
            }

            inviteRequestIds.Add(new DynamoItemIdEdge(dynDeal.DealId, publisherAccountId.ToEdgeId()));

            _deferRequestsService.DeferDealRequest(new DealRequestStatusUpdated
                                                   {
                                                       DealId = dynDeal.DealId,
                                                       PublisherAccountId = publisherAccountId,
                                                       FromStatus = DealRequestStatus.Unknown,
                                                       ToStatus = DealRequestStatus.Invited,
                                                       OccurredOn = _dateTimeProvider.UtcNowTs,
                                                       UpdatedByPublisherAccountId = dynDeal.PublisherAccountId
                                                   });

            _deferRequestsService.DeferFifoRequest(new DealStatIncrement
                                                   {
                                                       DealId = dynDeal.DealId,
                                                       StatType = DealStatType.TotalInvites,
                                                       FromPublisherAccountId = publisherAccountId
                                                   });

            _counterAndListService.AddUniqueItem(invitedKey, publisherAccountId.ToStringInvariant());
        }

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = inviteRequestIds,
                                                 Type = RecordType.DealRequest
                                             });

        // Made it through all of them successfully, done with tracking sends
        _counterAndListService.Clear(invitedKey);

        // And finally, if we get here that means invites were added (or intiially created) for a deal - flush the recent key
        _cacheClient.TryRemove<List<PublisherAccountProfile>>(string.Concat("RecentPendingRequests|", dynDeal.DealId));
    }
}
