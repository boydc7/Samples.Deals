using Amazon.DAX;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.FbSdk.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable NotAccessedField.Local

namespace Rydr.Api.Core.Services;

public class DynamoDealRequestService : IDealRequestService
{
    public static readonly string[] DealRequestTypeRefBetweenMinMax =
    {
        string.Concat((int)DynItemType.DealRequest, "|1500000000"), string.Concat((int)DynItemType.DealRequest, "|3000000000")
    };

    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IMapItemService _mapItemService;
    private static readonly ILog _log = LogManager.GetLogger("DynamoDealRequestService");

    private static readonly List<string> _allDealRequestStatusStrings = EnumsNET.Enums.GetNames<DealRequestStatus>()
                                                                                .Where(es => !es.EqualsOrdinalCi(DealRequestStatus.Unknown.ToString()))
                                                                                .AsList();

    private readonly IPocoDynamo _dynamoDb;
    private readonly IRequestStateManager _requestStateManager;
    private readonly IDealService _dealService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDialogMessageService _dialogMessageService;
    private readonly IDealRestrictionFilterService _dealRestrictionFilterService;

    public DynamoDealRequestService(IPocoDynamo dynamoDb, IDealRestrictionFilterService dealRestrictionFilterService,
                                    IRequestStateManager requestStateManager, IDealService dealService,
                                    IAuthorizationService authorizationService, IDialogMessageService dialogMessageService,
                                    IPublisherAccountService publisherAccountService, IMapItemService mapItemService)
    {
        _publisherAccountService = publisherAccountService;
        _mapItemService = mapItemService;
        _dynamoDb = dynamoDb;
        _dealRestrictionFilterService = dealRestrictionFilterService;
        _requestStateManager = requestStateManager;
        _dealService = dealService;
        _authorizationService = authorizationService;
        _dialogMessageService = dialogMessageService;
    }

    public IAsyncEnumerable<DynDealRequest> GetDealOwnerRequestsEverInStatusAsync(long dealOwnerPublisherAccountId, DealRequestStatus status,
                                                                                  DateTime statusChangeStart, DateTime statusChangeEnd, long inWorkspaceId = 0)
    {
        var startTimestamp = statusChangeStart.ToUnixTimestamp();
        var endTimestamp = statusChangeEnd.ToUnixTimestamp();

        var completedStatusStart = string.Concat(DynDealRequestStatusChange.BuildReferecneIdPrefix(status), startTimestamp);
        var completedStatusEnd = string.Concat(DynDealRequestStatusChange.BuildReferecneIdPrefix(status), endTimestamp);

        var query = _dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequestStatusChange,
                                                                                                                                                 dealOwnerPublisherAccountId) &&
                                                                                             Dynamo.Between(i.ReferenceId, completedStatusStart, completedStatusEnd));

        if (inWorkspaceId > 0)
        {
            query = query.Filter(i => i.WorkspaceId == inWorkspaceId);
        }

        return _dynamoDb.GetItemsFromAsync<DynDealRequest, DynItemTypeOwnerSpaceReferenceGlobalIndex>(query.Select(i => new
                                                                                                                        {
                                                                                                                            i.Id,
                                                                                                                            i.EdgeId
                                                                                                                        })
                                                                                                           .QueryAsync(_dynamoDb),
                                                                                                      i => new DynamoId(i.Id,
                                                                                                                        DynItem.GetFirstEdgeSegment(i.EdgeId)
                                                                                                                               .ToLong(0)
                                                                                                                               .ToEdgeId()));
    }

    public async Task<bool> CanBeRequestedAsync(long dealId, long byPublisherAccountId, IHasUserAuthorizationInfo withState = null)
    {
        if (dealId <= 0 || byPublisherAccountId <= 0)
        {
            return false;
        }

        var deal = await _dealService.GetDealAsync(dealId);

        var result = await CanBeRequestedAsync(deal, byPublisherAccountId, withState: withState);

        return result;
    }

    public async Task<bool> CanBeRequestedAsync(DynDeal deal, long byPublisherAccountId, HashSet<long> knownRequestedDealIds = null,
                                                bool isExistingBeingApproved = false, bool readOnlyIntent = false, IHasUserAuthorizationInfo withState = null)
    {
        // Optimized for read conditions, which are used in the QueryPublishedDeals requests...hence the extra logics...
        var validationState = readOnlyIntent && byPublisherAccountId > 0
                                  ? null
                                  : _requestStateManager.GetState();

        var authState = withState ?? validationState;

        if (authState == null)
        {
            validationState = _requestStateManager.GetState();
            authState = validationState;
        }

        // If deal is deleted/expired, nothing allowed
        // If deal is published, all set (published means can be requested)
        // If checking this on behalf of an EXISTING request to be approved, deal can be paused or completed also...
        if (deal == null || deal.IsDeleted() || deal.IsExpired() ||
            (deal.DealStatus != DealStatus.Published &&
             (!isExistingBeingApproved || (deal.DealStatus != DealStatus.Paused && deal.DealStatus != DealStatus.Completed))))
        {
            if (validationState != null)
            {
                validationState.ValidationMessage = "Pact is expired, deleted, or not in a valid status for request.";
            }

            return false;
        }

        var publisherAccountId = byPublisherAccountId > 0
                                     ? byPublisherAccountId
                                     : authState.RequestPublisherAccountId;

        // Deal cannot be requested by owner
        if (deal.PublisherAccountId == publisherAccountId)
        {
            if (validationState != null)
            {
                validationState.ValidationMessage = "Pact cannot be requested by its owner.";
            }

            return false;
        }

        var isInvited = deal.InvitedPublisherAccountIds.SafeContains(publisherAccountId);

        // If private, only invitees can partake
        if (deal.IsPrivateDeal && !isInvited && !authState.IsSystemRequest)
        {
            if (validationState != null)
            {
                validationState.ValidationMessage = "Only invitees can request private Pacts.";
            }

            return false;
        }

        if (!isInvited && !authState.IsSystemRequest)
        { // Invitees by-pass restrictions, they were invited
#if !SKIP_DEALRESTRICTION_FILTERS // || RELEASE
                var publisherAccount = publisherAccountId > 0
                                           ? await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId)
                                           : null;

                var workspacePublisherAccount = authState.WorkspaceId > 0
                                                    ? await WorkspaceService.DefaultWorkspaceService
                                                                            .TryGetDefaultPublisherAccountAsync(authState.WorkspaceId)
                                                                            
                                                    : null;

                if (!(await _dealRestrictionFilterService.MatchesAsync(deal.Restrictions, publisherAccount, workspacePublisherAccount)))
                {
                    if (validationState != null)
                    {
                        validationState.ValidationMessage = "You do not satisfy one or more of this deals restrictions.";
                    }

                    return false;
                }
#endif
        }

        // Is the request at max approval count (this is just a pre-check - it does not count as an interlocked operation to try and actually approve a deal
        if (!readOnlyIntent)
        {
            var currentApproved = await _dealService.GetDealStatAsync(deal.DealId, DealStatType.TotalApproved);

            if (currentApproved.Cnt.GetValueOrDefault() >= deal.ApprovalLimit)
            {
                if (validationState != null)
                {
                    validationState.ValidationMessage = "This Pact has reached its limit of approved requests.";
                }

                return false;
            }
        }

        // LEAVE AS THE LAST check (to push request checks as late as possible in the filter purposely)
        // Has the user already requested this deal
        if (!isExistingBeingApproved)
        {
            if (knownRequestedDealIds == null)
            {
                var existingDealRequest = readOnlyIntent || publisherAccountId <= 0
                                              ? null
                                              : await _dynamoDb.GetItemAsync<DynDealRequest>(deal.DealId, publisherAccountId.ToEdgeId());

                if (!readOnlyIntent && publisherAccountId > 0 && existingDealRequest != null)
                {
                    if (validationState != null)
                    {
                        validationState.ValidationMessage = "You have already requested this Pact.";
                    }

                    return false;
                }
            }
            else if (knownRequestedDealIds.Contains(deal.DealId))
            {
                if (validationState != null)
                {
                    validationState.ValidationMessage = "You have already requested this Pact.";
                }

                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanBeApprovedAsync(DynDealRequest dynDealRequest, bool forceAllowStatusChange = false)
    {
        if (dynDealRequest.RequestStatus != DealRequestStatus.Requested && dynDealRequest.RequestStatus != DealRequestStatus.Invited &&
            (dynDealRequest.RequestStatus != DealRequestStatus.Cancelled || !forceAllowStatusChange))
        {
            _requestStateManager.GetState().ValidationMessage = "Requests must be one of [Requested, Invited] in order to be Approved.";

            _log.WarnFormat("DealRequest cannot be approved - existing request not in valid status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return false;
        }

        var dynDeal = await _dealService.GetDealAsync(dynDealRequest.DealId);

        // To be approved, have to pass all the same filters as requesting initially with the exception of having already requested
        if (!(await CanBeRequestedAsync(dynDeal, dynDealRequest.PublisherAccountId, isExistingBeingApproved: true)))
        {
            _log.WarnFormat("DealRequest cannot be approved - see previous 'Deal cannot be requested' warning for details. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return false;
        }

        return true;
    }

    public Task<bool> CanBeDeniedAsync(DynDealRequest dynDealRequest)
    {
        if (dynDealRequest.RequestStatus != DealRequestStatus.Invited && dynDealRequest.RequestStatus != DealRequestStatus.Requested)
        {
            _requestStateManager.GetState().ValidationMessage = "Requests must be one of [Requested, Invited] in order to be Denied.";

            _log.WarnFormat("DealRequest cannot be denied - existing request not in valid status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task<bool> CanBeCancelledAsync(long dealId, long publisherAccountId)
    {
        var existingRequest = await _dynamoDb.GetItemAsync<DynDealRequest>(dealId, publisherAccountId.ToEdgeId());

        var result = await CanBeCancelledAsync(existingRequest);

        return result;
    }

    public Task<bool> CanBeCancelledAsync(DynDealRequest dynDealRequest)
    {
        if (dynDealRequest == null ||
            dynDealRequest.RequestStatus == DealRequestStatus.Cancelled ||
            dynDealRequest.RequestStatus == DealRequestStatus.Completed)
        {
            _requestStateManager.GetState().ValidationMessage = "Request is already Cancelled or Completed and cannot be Cancelled.";

            _log.WarnFormat("DealRequest cannot be cancelled - existing request not in valid status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest?.DealId ?? 0, dynDealRequest?.PublisherAccountId ?? 0, dynDealRequest?.RequestStatus ?? DealRequestStatus.Unknown);

            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanBeCompletedAsync(DynDealRequest dynDealRequest)
    {
        if (dynDealRequest.RequestStatus != DealRequestStatus.InProgress &&
            dynDealRequest.RequestStatus != DealRequestStatus.Redeemed)
        {
            _requestStateManager.GetState().ValidationMessage = "Only InProgress or Redeemed requests can be Completed.";

            _log.WarnFormat("DealRequest cannot be completed - existing request not in valid status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return Task.FromResult(false);
        }

        // TODO: Put various completion requirements here - i.e. agreed as completed by the deal owner/influencer, automated checks
        // for required Receive attributes are met, etc.

        return Task.FromResult(true);
    }

    public Task<bool> CanBeRedeemedAsync(DynDealRequest dynDealRequest)
    {
        if (dynDealRequest.RequestStatus != DealRequestStatus.InProgress)
        {
            _requestStateManager.GetState().ValidationMessage = "Only InProgress requests can be marked Redeemed.";

            _log.WarnFormat("DealRequest cannot be redeemed - existing request not in valid status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanBeDelinquentAsync(DynDealRequest dynDealRequest, bool ignoreTimeConstraint = false)
    {
        if (dynDealRequest.RequestStatus == DealRequestStatus.Delinquent || !dynDealRequest.IsDelinquent(ignoreTimeConstraint))
        {
            _requestStateManager.GetState().ValidationMessage = "Request is already Delinquent and cannot be marked Delinquent.";

            _log.WarnFormat("DealRequest cannot be made delinquent - existing request not in valid status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<DynDealRequest> GetDealRequestAsync(long dealId, long publisherAccountId)
        => _dynamoDb.GetItemAsync<DynDealRequest>(dealId, publisherAccountId.ToEdgeId());

    public async IAsyncEnumerable<DealResponse> GetDealResponseRequestExtendedAsync(ICollection<DynDealRequest> dynDealRequests)
    {
        // IEnumerable<DynamoId> dealIds
        var deals = await _dealService.GetDynDealsAsync(dynDealRequests.GroupBy(r => r.DealId)
                                                                       .Select(g => new DynamoId(g.Max(gr => gr.DealPublisherAccountId),
                                                                                                 g.Key.ToEdgeId())));

        var maps = await _dealService.GetDealMapsForTransformAsync(deals);

        var dealResponseMap = deals.Select(dd => dd.ToDealResponse(maps.PublisherMediaMap, maps.PlaceMap, maps.HashtagMap, maps.PublisherMap))
                                   .ToDictionarySafe(d => d.Deal.Id);

        var totalApprovedMap = await _dynamoDb.QueryItemsAsync<DynDealStat>(deals.Select(d => new DynamoId(d.DealId,
                                                                                                           DynDealStat.BuildEdgeId(d.PublisherAccountId,
                                                                                                                                   DealStatType.TotalApproved))))
                                              .ToDictionarySafe(ds => ds.DealId);

        var completionMediaMap = await _dynamoDb.QueryItemsAsync<DynPublisherMedia>(dynDealRequests.Where(dr => !dr.CompletionMediaIds.IsNullOrEmptyRydr())
                                                                                                   .SelectMany(dr => dr.CompletionMediaIds.Select(cmi => new DynamoId(dr.PublisherAccountId, cmi.ToEdgeId())))
                                                                                                   .Distinct())
                                                .Where(dm => dm != null && !dm.IsDeleted())
                                                .ToDictionarySafe(dm => dm.PublisherMediaId);

        var requestStatusChangeMap = await _dynamoDb.QueryItemsAsync<DynDealRequestStatusChange>(dynDealRequests.SelectMany(dr => _allDealRequestStatusStrings.Select(drs => new DynamoId(dr.DealId,
                                                                                                                                                                                          DynDealRequestStatusChange.BuildEdgeId(drs,
                                                                                                                                                                                                                                 dr.PublisherAccountId)))))
                                                    .ToDictionaryManySafe(sc => (sc.DealId, sc.PublisherAccountId));

        var lifetimeStatsMap = await _dynamoDb.QueryItemsAsync<DynPublisherMediaStat>(dynDealRequests.Where(dr => !dr.CompletionMediaIds.IsNullOrEmptyRydr())
                                                                                                     .SelectMany(dr => dr.CompletionMediaIds
                                                                                                                         .Select(cmi => new DynamoId(cmi,
                                                                                                                                                     DynPublisherMediaStat.BuildEdgeId(FbIgInsights.LifetimePeriod,
                                                                                                                                                                                       FbIgInsights.LifetimeEndTime))))
                                                                                                     .Distinct())
                                              .ToDictionarySafe(dm => dm.PublisherMediaId);

        var dialogKeyMap = dynDealRequests.Select(dr => (dr.DealId, dr.PublisherAccountId, DialogKey: new[]
                                                                                                      {
                                                                                                          dr.DealId, dr.PublisherAccountId, dr.DealPublisherAccountId
                                                                                                      }.ToDialogKey()))
                                          .Where(t => !t.DialogKey.IsNullOrEmpty())
                                          .ToDictionarySafe(t => (t.DealId, t.PublisherAccountId), t => t.DialogKey);

        var dialogIdMap = new Dictionary<string, long>(dialogKeyMap.Count, StringComparer.OrdinalIgnoreCase);

        var lastMessageMap = await _dynamoDb.GetItemsFromAsync<DynDialogMessage, DynItemMap>(_dynamoDb.QueryItemsAsync<DynItemMap>(dialogKeyMap.Select(k => new DynamoId(k.Key.DealId,
                                                                                                                                                                         DynItemMap.BuildEdgeId(DynItemType.Message, k.Value)))),
                                                                                             m =>
                                                                                             {
                                                                                                 dialogIdMap[DynItem.GetFinalEdgeSegment(m.EdgeId)] = m.ReferenceNumber.Value;

                                                                                                 return new DynamoId(m.ReferenceNumber.Value, m.MappedItemEdgeId);
                                                                                             })
                                            .ToDictionarySafe(dm => dm.DialogId);

        foreach (var dynDealRequest in dynDealRequests)
        {
            var dealRequestKey = (dynDealRequest.DealId, dynDealRequest.PublisherAccountId);

            var dealResponseCopy = dealResponseMap[dynDealRequest.DealId].CreateCopy();

            DynDialogMessage lastMessage = null;

            if (dialogKeyMap.ContainsKey(dealRequestKey))
            {
                var dealRequestDialogKey = dialogKeyMap[dealRequestKey];

                var dialogId = dialogIdMap.ContainsKey(dealRequestDialogKey)
                                   ? dialogIdMap[dealRequestDialogKey]
                                   : 0;

                lastMessage = dialogId > 0 && lastMessageMap.ContainsKey(dialogId)
                                  ? lastMessageMap[dialogId]
                                  : null;
            }

            dealResponseCopy.DealRequest = await new DealRequestExtended
                                                 {
                                                     DealRequest = dynDealRequest,
                                                     CompletionMedia = dynDealRequest.CompletionMediaIds.IsNullOrEmptyRydr()
                                                                           ? null
                                                                           : dynDealRequest.CompletionMediaIds
                                                                                           .Select(cmi => completionMediaMap.ContainsKey(cmi)
                                                                                                              ? completionMediaMap[cmi]
                                                                                                              : null)
                                                                                           .Where(dpm => dpm != null)
                                                                                           .AsList(),
                                                     StatusChanges = requestStatusChangeMap.ContainsKey(dealRequestKey)
                                                                         ? requestStatusChangeMap[dealRequestKey]
                                                                         : null,
                                                     LifetimeStats = dynDealRequest.CompletionMediaIds.IsNullOrEmptyRydr()
                                                                         ? null
                                                                         : dynDealRequest.CompletionMediaIds
                                                                                         .Select(cmi => lifetimeStatsMap.ContainsKey(cmi)
                                                                                                            ? lifetimeStatsMap[cmi]
                                                                                                            : null)
                                                                                         .Where(dpm => dpm != null)
                                                                                         .ToDictionarySafe(dpm => dpm.PublisherMediaId),
                                                     LastMessage = lastMessage == null
                                                                       ? null
                                                                       : await lastMessage.ToDialogMessageAsync(),
                                                 }.ToDealRequestAsync();

            dealResponseCopy.ScrubDeal();

            // After scrubbing always get the approvalsRemaining...
            var totalApproved = totalApprovedMap.TryGetValue(dynDealRequest.DealId, out var dealStat)
                                    ? dealStat.Value.ToLong(0)
                                    : 0;

            dealResponseCopy.PopulateApprovalsRemaining(totalApproved);

            yield return dealResponseCopy;
        }
    }

    public async Task<DealRequestExtended> GetDealRequestExtendedAsync(long dealId, long publisherAccountId)
    {
        var dealRequest = await GetDealRequestAsync(dealId, publisherAccountId);

        return dealRequest == null
                   ? null
                   : await GetDealRequestExtendedAsync(dealRequest);
    }

    public async Task<List<DynDealRequest>> GetDealRequestsAsync(IEnumerable<long> forDealIds, long publisherAccountId)
    {
        var publisherAccountEdgeId = publisherAccountId.ToEdgeId();

        var results = await _dynamoDb.QueryItemsAsync<DynDealRequest>(forDealIds.Select(i => new DynamoId(i, publisherAccountEdgeId)))
                                     .Take(5000)
                                     .ToList();

        return results;
    }

    public Task<List<DynDealRequest>> GetDealRequestsAsync(long dealId, IEnumerable<long> publisherAccountIds)
        => _dynamoDb.QueryItemsAsync<DynDealRequest>(publisherAccountIds.Select(i => new DynamoId(dealId, i.ToEdgeId())))
                    .Take(5000)
                    .ToList();

    public Task<List<DynDealRequest>> GetAllActiveDealRequestsAsync(long dealId)
        => _dynamoDb.FromQuery<DynDealRequest>(dr => dr.Id == dealId &&
                                                     Dynamo.BeginsWith(dr.EdgeId, "00"))
                    .Filter(dr => dr.TypeId == (int)DynItemType.DealRequest &&
                                  dr.DeletedOnUtc == null &&
                                  dr.StatusId != DealRequestStatus.Cancelled.ToString() &&
                                  dr.StatusId != DealRequestStatus.Completed.ToString())
                    .QueryAsync(_dynamoDb)
                    .Where(dr => dr.RequestStatus != DealRequestStatus.Cancelled &&
                                 dr.RequestStatus != DealRequestStatus.Completed)
                    .Take(25000)
                    .ToList();

    public IAsyncEnumerable<DynDealRequest> GetPublisherAccountRequestsAsync(long publisherAccountId, IEnumerable<DealRequestStatus> statuses = null)
    {
        var query = _dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(dr => dr.EdgeId == publisherAccountId.ToEdgeId() &&
                                                                             Dynamo.Between(dr.TypeReference,
                                                                                            DealRequestTypeRefBetweenMinMax[0],
                                                                                            DealRequestTypeRefBetweenMinMax[1]));

        if (statuses != null)
        {
            query.Filter(dr => dr.TypeId == (int)DynItemType.DealRequest &&
                               dr.DeletedOnUtc == null &&
                               Dynamo.In(dr.StatusId, statuses.Select(s => s.ToString())));
        }
        else
        {
            query.Filter(dr => dr.TypeId == (int)DynItemType.DealRequest &&
                               dr.DeletedOnUtc == null);
        }

        return _dynamoDb.GetItemsFromAsync<DynDealRequest, DynItemEdgeIdGlobalIndex>(query.QueryAsync(_dynamoDb),
                                                                                     i => i.GetDynamoId(),
                                                                                     take: 10000);
    }

    public Task<List<string>> GetActiveDealGroupIdsAsync(long forPublisherAccountId)
        => _dynamoDb.FromQuery<DynItemMap>(m => m.Id == forPublisherAccountId &&
                                                Dynamo.BeginsWith(m.EdgeId, string.Concat((int)DynItemType.DealGroup, "|active|")))
                    .QueryColumnAsync(m => m.EdgeId, _dynamoDb)
                    .Select(DynItemMap.GetFinalEdgeSegment)
                    .Where(s => s.HasValue())
                    .Take(2500)
                    .ToList();

    public async Task RequestDealAsync(long dealId, long forPublisherAccountId, bool fromInvite, int hoursAllowedInProgress, int hoursAllowedRedeemed)
    {
        var dynDeal = await _dealService.GetDealAsync(dealId);

        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(forPublisherAccountId);

        await DoRequestDealAsync(dynDeal, publisherAccount, fromInvite, hoursAllowedInProgress, hoursAllowedRedeemed);
    }

    public async Task UpdateDealRequestAsync(long dealId, long publisherAccountId, DealRequestStatus toStatus, int hoursAllowedInProgress,
                                             int hoursAllowedRedeemed, bool forceAllowUncancel = false)
    {
        var existingDealRequest = await _dynamoDb.GetItemAsync<DynDealRequest>(dealId, publisherAccountId.ToEdgeId());
        Guard.AgainstRecordNotFound(existingDealRequest == null, string.Concat(dealId, ":", publisherAccountId));

        if (hoursAllowedInProgress > 0)
        {
            existingDealRequest.HoursAllowedInProgress = hoursAllowedInProgress;
        }

        if (hoursAllowedRedeemed > 0)
        {
            existingDealRequest.HoursAllowedRedeemed = hoursAllowedRedeemed;
        }

        if (existingDealRequest.RequestStatus == toStatus || toStatus == DealRequestStatus.Unknown)
        {
            if (hoursAllowedInProgress > 0 || hoursAllowedRedeemed > 0)
            { // Changed the timers...store and return
                await _dynamoDb.PutItemTrackedAsync(existingDealRequest);

                return;
            }

            _log.WarnFormat("Not updating DealRequest - existing request already in requested status or requested status unknown. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", existingDealRequest.DealId, existingDealRequest.PublisherAccountId, existingDealRequest.RequestStatus);

            return;
        }

        switch (toStatus)
        {
            case DealRequestStatus.Unknown:
            case DealRequestStatus.Invited:
            case DealRequestStatus.Requested:
                throw new ArgumentOutOfRangeException(nameof(toStatus), toStatus, "DealRequest cannot be updated to invalid status");

            case DealRequestStatus.Cancelled:
                await DoCancelRequestAsync(existingDealRequest);

                return;

            case DealRequestStatus.Denied:
                await DoDenyRequestAsync(existingDealRequest);

                return;

            case DealRequestStatus.Completed:
                await DoCompleteRequestAsync(existingDealRequest);

                return;

            case DealRequestStatus.InProgress:
                await DoApproveRequestAsync(existingDealRequest, forceAllowUncancel);

                return;

            case DealRequestStatus.Redeemed:
                await DoRedeemRequestAsync(existingDealRequest);

                return;

            case DealRequestStatus.Delinquent:
                await DoDelinquentRequestAsync(existingDealRequest);

                return;

            default:

                throw new ArgumentOutOfRangeException(nameof(toStatus), toStatus, "Unknown/Unhandled DealRequestStatus value passed");
        }
    }

    private async Task DoRequestDealAsync(DynDeal dynDeal, DynPublisherAccount publisherAccount, bool fromInvite, int hoursAllowedInProgress, int hoursAllowedRedeemed)
    { // NOTE: purposely setting the status here to unknown, as it gets changed to invited/requested correctly next
        // in the DoChangeRequestStatus call, which includes checks for not changing status if the request is already
        // in the status being requested.
        var dynDealRequest = await dynDeal.ToDynDealRequestAsync(publisherAccount.Id, DealRequestStatus.Unknown, hoursAllowedInProgress, hoursAllowedRedeemed);

        if (!(await DoChangeRequestStatusAsync(dynDealRequest,
                                               fromInvite
                                                   ? DealRequestStatus.Invited
                                                   : DealRequestStatus.Requested,
                                               dr => CanBeRequestedAsync(dynDeal, publisherAccount.Id))))
        {
            throw new OperationCannotBeCompletedException("Deal cannot be requested as it is in a invalid state, has already been requested, has reached a request limit, or otherwise is not able to be requested.");
        }
    }

    private async Task DoDenyRequestAsync(DynDealRequest dynDealRequest)
    {
        if (!(await DoChangeRequestStatusAsync(dynDealRequest, DealRequestStatus.Denied, CanBeDeniedAsync)))
        {
            throw new OperationCannotBeCompletedException("DealRequest cannot be denied as it is in a invalid state, has already been denied for this request, or otherwise is not able to be denied.");
        }
    }

    private async Task DoCompleteRequestAsync(DynDealRequest dynDealRequest)
    {
        if (!(await DoChangeRequestStatusAsync(dynDealRequest, DealRequestStatus.Completed, CanBeCompletedAsync)))
        {
            throw new OperationCannotBeCompletedException("DealRequest cannot be completed as it is in a invalid state, has already been completed for this request, or otherwise is not able to be completed.");
        }
    }

    private async Task DoRedeemRequestAsync(DynDealRequest dynDealRequest)
    {
        if (!(await DoChangeRequestStatusAsync(dynDealRequest, DealRequestStatus.Redeemed, CanBeRedeemedAsync)))
        {
            throw new OperationCannotBeCompletedException("DealRequest cannot be redeemed as it is in a invalid state, has already been completed for this request, or otherwise is not able to be completed.");
        }
    }

    private async Task DoDelinquentRequestAsync(DynDealRequest dynDealRequest)
    {
        if (!(await DoChangeRequestStatusAsync(dynDealRequest, DealRequestStatus.Delinquent, r => CanBeDelinquentAsync(r, true))))
        {
            throw new OperationCannotBeCompletedException("DealRequest cannot be set delinquent as it is in a invalid state, has already been completed for this request, or otherwise is not able to be completed.");
        }
    }

    private async Task DoCancelRequestAsync(DynDealRequest dynDealRequest)
    {
        if (dynDealRequest.RequestStatus != DealRequestStatus.InProgress && dynDealRequest.RequestStatus != DealRequestStatus.Redeemed)
        { // If not moving from InProgress/Redeemed->Cancelled, normal change
            if (!(await DoChangeRequestStatusAsync(dynDealRequest, DealRequestStatus.Cancelled, CanBeCancelledAsync)))
            {
                throw new OperationCannotBeCompletedException("DealRequest cannot be cancelled as it is in a invalid state, has already been cancelled for this request, or otherwise is not able to be cancelled.");
            }

            return;
        }

        if (!(await CanChangeRequestStatusToAsync(dynDealRequest, DealRequestStatus.Cancelled, CanBeCancelledAsync)))
        {
            throw new OperationCannotBeCompletedException("DealRequest cannot be cancelled as it is in a invalid state, has already been cancelled for this request, or otherwise is not able to be cancelled.");
        }

        // If moving from InProgress/Redeemed to Cancelled, add this back as a returned inProgress request, interlocked with the status change
        // MaxApprovals is limitted - interlock with a tracking map
        var dealRequestTable = DynamoMetadata.GetTable<DynDealRequest>();
        var dealTable = DynamoMetadata.GetTable<DynDeal>();

        dynDealRequest.RequestStatus = DealRequestStatus.Cancelled;
        dynDealRequest.ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant();

        var request = new TransactWriteItemsRequest
                      {
                          TransactItems = new List<TransactWriteItem>
                                          { // Deal ReturnedApprovals must be incremented interlocked with the request status update
                                              new()
                                              {
                                                  Update = new Update
                                                           {
                                                               TableName = dealTable.Name,
                                                               Key = new Dictionary<string, AttributeValue>
                                                                     {
                                                                         {
                                                                             "Id", new AttributeValue
                                                                                   {
                                                                                       N = dynDealRequest.DealPublisherAccountId.ToStringInvariant()
                                                                                   }
                                                                         },
                                                                         {
                                                                             "EdgeId", new AttributeValue
                                                                                       {
                                                                                           S = dynDealRequest.DealId.ToEdgeId()
                                                                                       }
                                                                         }
                                                                     },
                                                               ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                                                                                           {
                                                                                               {
                                                                                                   ":incrOne", new AttributeValue
                                                                                                               {
                                                                                                                   N = "1"
                                                                                                               }
                                                                                               },
                                                                                               {
                                                                                                   ":zeroVal", new AttributeValue
                                                                                                               {
                                                                                                                   N = "0"
                                                                                                               }
                                                                                               }
                                                                                           },
                                                               UpdateExpression = "SET ReturnedApprovals = if_not_exists(ReturnedApprovals, :zeroVal) + :incrOne"
                                                           }
                                              },
                                              new()
                                              {
                                                  Put = new Put
                                                        {
                                                            TableName = dealRequestTable.Name,
                                                            Item = _dynamoDb.Converters.ToAttributeValues(_dynamoDb, dynDealRequest, dealRequestTable),
                                                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                                                                                        {
                                                                                            {
                                                                                                ":inProgressStatus", new AttributeValue
                                                                                                                     {
                                                                                                                         S = DealRequestStatus.InProgress.ToString()
                                                                                                                     }
                                                                                            },
                                                                                            {
                                                                                                ":redeemedStatus", new AttributeValue
                                                                                                                   {
                                                                                                                       S = DealRequestStatus.Redeemed.ToString()
                                                                                                                   }
                                                                                            }
                                                                                        },
                                                            ConditionExpression = "attribute_exists(StatusId) AND StatusId IN(:inProgressStatus, :redeemedStatus)",
                                                            ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                        }
                                              }
                                          }
                      };

        // Execute
        try
        {
            _dynamoDb.DynamoDb.TransactWriteItemsAsync(request).GetAwaiter().GetResult();
        }
        catch(TransactionCanceledException tx) when(_log.LogExceptionReturnFalse(tx, $"DoCancelRequestAsync for DealId [{dynDealRequest.DealId}], PublisherAccountId [{dynDealRequest.PublisherAccountId}]"))
        { // Unreachable code
            throw new OperationCannotBeCompletedException("DealRequest cannot be cancelled as it is in a invalid state, has already been cancelled for this request, or otherwise is not able to be cancelled.");
        }
        catch(DaxTransactionCanceledException dx) when(_log.LogExceptionReturnFalse(dx, $"DoCancelRequestAsync for DealId [{dynDealRequest.DealId}], PublisherAccountId [{dynDealRequest.PublisherAccountId}]"))
        { // Unreachable code
            throw new OperationCannotBeCompletedException("DealRequest cannot be cancelled as it is in a invalid state, has already been cancelled for this request, or otherwise is not able to be cancelled.");
        }
    }

    private async Task DoApproveRequestAsync(DynDealRequest dynDealRequest, bool forceAllowUncancel)
    { // Approval has to be interlocked with a check for maxApprovals...
        var dynDeal = await _dealService.GetDealAsync(dynDealRequest.DealId);

        // MaxApprovals is limitted - interlock with a tracking map
        var table = DynamoMetadata.GetTable<DynDealRequest>();

        if (!(await CanChangeRequestStatusToAsync(dynDealRequest, DealRequestStatus.InProgress, d => CanBeApprovedAsync(d, forceAllowUncancel))))
        {
            throw new OperationCannotBeCompletedException("DealRequest cannot be approved as it is in a invalid state, has already been approved for this request, the deal has reached it's approval limits, or otherwise is not able to be approved.");
        }

        dynDealRequest.RequestStatus = DealRequestStatus.InProgress;
        dynDealRequest.ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant();

        var mapId = dynDeal.DealId;
        var mapEdgeId = DynItemMap.BuildEdgeId(DynItemType.Deal, "InterlockedApproved");

        var request = new TransactWriteItemsRequest
                      {
                          TransactItems = new List<TransactWriteItem>
                                          { // Mapped item must not exist, or exist with a count < max approval
                                              new()
                                              {
                                                  Update = new Update
                                                           {
                                                               TableName = DynItemTypeHelpers.DynamoItemMapsTableName,
                                                               Key = new Dictionary<string, AttributeValue>
                                                                     {
                                                                         {
                                                                             "Id", new AttributeValue
                                                                                   {
                                                                                       N = mapId.ToStringInvariant()
                                                                                   }
                                                                         },
                                                                         {
                                                                             "EdgeId", new AttributeValue
                                                                                       {
                                                                                           S = mapEdgeId
                                                                                       }
                                                                         }
                                                                     },
                                                               ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                                                                                           {
                                                                                               {
                                                                                                   ":maxAllowed", new AttributeValue
                                                                                                                  {
                                                                                                                      N = dynDeal.ApprovalLimit.ToStringInvariant()
                                                                                                                  }
                                                                                               },
                                                                                               {
                                                                                                   ":incrOne", new AttributeValue
                                                                                                               {
                                                                                                                   N = "1"
                                                                                                               }
                                                                                               },
                                                                                               {
                                                                                                   ":zeroVal", new AttributeValue
                                                                                                               {
                                                                                                                   N = "0"
                                                                                                               }
                                                                                               }
                                                                                           },
                                                               ConditionExpression = "attribute_not_exists(ReferenceNumber) OR ReferenceNumber < :maxAllowed",
                                                               UpdateExpression = "SET ReferenceNumber = if_not_exists(ReferenceNumber, :zeroVal) + :incrOne"
                                                           }
                                              },
                                              new()
                                              {
                                                  Put = new Put
                                                        {
                                                            TableName = table.Name,
                                                            Item = _dynamoDb.Converters.ToAttributeValues(_dynamoDb, dynDealRequest, table),
                                                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                                                                                        {
                                                                                            {
                                                                                                ":requestedStatus", new AttributeValue
                                                                                                                    {
                                                                                                                        S = DealRequestStatus.Requested.ToString()
                                                                                                                    }
                                                                                            },
                                                                                            {
                                                                                                ":invitedStatus", new AttributeValue
                                                                                                                  {
                                                                                                                      S = DealRequestStatus.Invited.ToString()
                                                                                                                  }
                                                                                            }
                                                                                        },
                                                            ConditionExpression = "attribute_exists(StatusId) AND StatusId IN(:requestedStatus, :invitedStatus)",
                                                            ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                        }
                                              }
                                          }
                      };

        // Execute
        try
        {
            _dynamoDb.DynamoDb.TransactWriteItemsAsync(request).GetAwaiter().GetResult();
        }
        catch(TransactionCanceledException tx) when(_log.LogExceptionReturnFalse(tx, $"DoApproveRequest for DealId [{dynDealRequest.DealId}], PublisherAccountId [{dynDealRequest.PublisherAccountId}], mappedEdge would be[{DynItemMap.BuildEdgeId(DynItemType.Deal, "InterlockedApproved")}]"))
        { // Unreachable code
            throw new OperationCannotBeCompletedException("DealRequest cannot be approved as it is in a invalid state, has already been approved for this request, the deal has reached it's approval limits, or otherwise is not able to be approved.");
        }
        catch(DaxTransactionCanceledException dx) when(_log.LogExceptionReturnFalse(dx, $"DoApproveRequest for DealId [{dynDealRequest.DealId}], PublisherAccountId [{dynDealRequest.PublisherAccountId}], mappedEdge would be[{DynItemMap.BuildEdgeId(DynItemType.Deal, "InterlockedApproved")}]"))
        { // Unreachable code
            throw new OperationCannotBeCompletedException("DealRequest cannot be approved as it is in a invalid state, has already been approved for this request, the deal has reached it's approval limits, or otherwise is not able to be approved.");
        }
        finally
        {
            MapItemService.DefaultMapItemService.OnMapUpdate(mapId, mapEdgeId);
        }
    }

    private async Task<bool> DoChangeRequestStatusAsync(DynDealRequest dynDealRequest, DealRequestStatus toStatus,
                                                        Func<DynDealRequest, Task<bool>> canPerformPredicate)
    {
        if (!(await CanChangeRequestStatusToAsync(dynDealRequest, toStatus, canPerformPredicate)))
        {
            return false;
        }

        dynDealRequest.RequestStatus = toStatus;
        dynDealRequest.ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant();

        var oldDynDealRequest = await _dynamoDb.PutItemTrackedAsync(dynDealRequest, returnOld: true);

        if (toStatus == DealRequestStatus.Invited || toStatus == DealRequestStatus.Requested)
        { // For new requests (i.e. requested/invited), the dealRequest must not already exist, i.e. we should get back null
            if (oldDynDealRequest != null)
            { // Put back the old request
                await _dynamoDb.PutItemAsync(oldDynDealRequest);

                _log.WarnFormat("Not updating DealRequest Status - race condition met, invite/request already processed. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

                return false;
            }
        }
        else if (oldDynDealRequest == null || oldDynDealRequest.RequestStatus == toStatus)
        { // For all others, we should get back a non-null object that is NOT in the same status we're trying to update to currently

            // Put back the old request, or delete the one we just put in
            if (oldDynDealRequest == null)
            {
                await _dynamoDb.DeleteItemAsync<DynDealRequest>(dynDealRequest.Id, dynDealRequest.EdgeId);
            }
            else
            {
                await _dynamoDb.PutItemAsync(oldDynDealRequest);
            }

            _log.WarnFormat("Not updating DealRequest Status - race condition met, non-invite/request already processed. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return false;
        }

        return true;
    }

    private async Task<bool> CanChangeRequestStatusToAsync(DynDealRequest dynDealRequest, DealRequestStatus toStatus, Func<DynDealRequest, Task<bool>> canPerformPredicate)
    {
        if (dynDealRequest.RequestStatus == toStatus)
        {
            _log.WarnFormat("Not updating DealRequest Status - existing request already in requested status. Deal [{0}], PublisherAccountId [{1}], ExistingRequestStatus [{2}]", dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.RequestStatus);

            return false;
        }

        if (!(await canPerformPredicate(dynDealRequest)))
        {
            return false;
        }

        return true;
    }

    private async Task<DealRequestExtended> GetDealRequestExtendedAsync(DynDealRequest dealRequest)
    {
        var completionMedia = dealRequest.CompletionMediaIds.IsNullOrEmpty()
                                  ? null
                                  : await _dynamoDb.QueryItemsAsync<DynPublisherMedia>(dealRequest.CompletionMediaIds
                                                                                                  .Select(mi => new DynamoId(dealRequest.PublisherAccountId, mi.ToEdgeId())))
                                                   .Where(dm => dm != null && !dm.IsDeleted())
                                                   .Take(500)
                                                   .ToList();

        var requestStatusChanges = await _dynamoDb.QueryItemsAsync<DynDealRequestStatusChange>(_allDealRequestStatusStrings.Select(drs => new DynamoId(dealRequest.DealId,
                                                                                                                                                       DynDealRequestStatusChange.BuildEdgeId(drs,
                                                                                                                                                                                              dealRequest.PublisherAccountId))))
                                                  .Take(500)
                                                  .ToList();

        var lifetimeStats = completionMedia.IsNullOrEmpty()
                                ? null
                                : await _dynamoDb.QueryItemsAsync<DynPublisherMediaStat>(completionMedia.Select(cm => new DynamoId(cm.PublisherMediaId,
                                                                                                                                   DynPublisherMediaStat.BuildEdgeId(FbIgInsights.LifetimePeriod,
                                                                                                                                                                     FbIgInsights.LifetimeEndTime))))
                                                 .ToDictionarySafe(k => k.PublisherMediaId);

        var dealRequestDialog = await dealRequest.TryGetDealRequestDialogAsync();

        var lastMessage = dealRequestDialog == null
                              ? null
                              : await _dialogMessageService.GetLastMessageAsync(dealRequestDialog.DialogId);

        return new DealRequestExtended
               {
                   DealRequest = dealRequest,
                   CompletionMedia = completionMedia.NullIfEmpty(),
                   StatusChanges = requestStatusChanges.NullIfEmpty(),
                   LifetimeStats = lifetimeStats,
                   LastMessage = lastMessage == null
                                     ? null
                                     : await lastMessage.ToDialogMessageAsync()
               };
    }
}
