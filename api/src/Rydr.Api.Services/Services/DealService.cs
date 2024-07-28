using System.Globalization;
using System.Text.RegularExpressions;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.Api.QueryDto;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;
using ServiceStack.Web;
using LongRange = Rydr.Api.Dto.Shared.LongRange;

namespace Rydr.Api.Services.Services;

public class DealPublicService : BaseApiService
{
    private static readonly string _xDealHtml = FileHelper.ReadAllTextAsync("xdealcontent.txt")
                                                          .GetAwaiter().GetResult()
                                                          .ReplaceFirst("||rydrdeal.fbappid||", RydrEnvironment.GetAppSetting("Facebook.DefaultAppId", "286022225400402"));

    private readonly IAssociationService _associationService;
    private readonly IDealMetricService _dealMetricService;

    public DealPublicService(IAssociationService associationService, IDealMetricService dealMetricService)
    {
        _associationService = associationService;
        _dealMetricService = dealMetricService;
    }

    public static async Task<(Deal Deal, string Html, string externalLink)> GetDealExternalHtmlAsync(long dealId)
    {
        var dynDeal = await DealExtensions.DefaultDealService.GetDealAsync(dealId);

        var dealCreatorPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService.GetPublisherAccountAsync(dynDeal.PublisherAccountId);

        var deal = await dynDeal.ToDealAsync();

        var imageUrl = deal.PublisherMedias.IsNullOrEmpty()
                           ? null
                           : deal.PublisherMedias
                                 .FirstOrDefault(m => m.MediaUrl.HasValue())?
                                 .MediaUrl
                             ??
                             deal.PublisherMedias
                                 .FirstOrDefault(m => m.ThumbnailUrl.HasValue())?
                                 .ThumbnailUrl;

        var storiesRequired = deal.ReceiveType?
                                  .Where(t => t.Type == PublisherContentType.Story)
                                  .Sum(t => t.Quantity) ?? 0;

        var postsRequired = deal.ReceiveType?
                                .Where(t => t.Type == PublisherContentType.Post)
                                .Sum(t => t.Quantity) ?? 0;

        string placeName = null;
        Address dealAddress = null;

        if (deal.ReceivePlace?.Address?.IsValidLatLon() ?? false)
        {
            placeName = deal.ReceivePlace.Name.Coalesce(deal.ReceivePlace.Address.Name);
            dealAddress = deal.ReceivePlace.Address;
        }
        else if (deal.Place?.Address?.IsValidLatLon() ?? false)
        {
            placeName = deal.Place.Name.Coalesce(deal.Place.Address.Name);
            dealAddress = deal.Place.Address;
        }

        var dealLink = dynDeal.ToDealPublicLinkId();

        var dealHtml = _xDealHtml.Replace("||rydrdeal.url||", string.Concat("https://cdn.getrydr.com/x/", dealLink))
                                 .Replace("||rydrdeal.xguid||", dealLink)
                                 .Replace("||rydrdeal.description||", deal.Description)
                                 .Replace("||rydrdeal.image||", imageUrl.Coalesce(string.Empty))
                                 .Replace("||rydrdeal.title||", deal.Title)
                                 .Replace("||rydrdeal.profileimage||", dealCreatorPublisherAccount.ProfilePicture)
                                 .Replace("||rydrdeal.username||", dealCreatorPublisherAccount.UserName)
                                 .Replace("||rydrdeal.stories||", storiesRequired.ToStringInvariant())
                                 .Replace("||rydrdeal.posts||", postsRequired.ToStringInvariant())
                                 .Replace("||rydrdeal.placename||", placeName.Coalesce("Unnamed location"))
                                 .Replace("||rydrdeal.placelatitude||", (dealAddress?.Latitude ?? 0).ToString(CultureInfo.InvariantCulture))
                                 .Replace("||rydrdeal.placelongitude||", (dealAddress?.Longitude ?? 0).ToString(CultureInfo.InvariantCulture))
                                 .Replace("||rydrdeal.storytext||", storiesRequired == 1
                                                                        ? "Story"
                                                                        : "Stories")
                                 .Replace("||rydrdeal.posttext||", postsRequired == 1
                                                                       ? "Post"
                                                                       : "Posts");

        return (deal, dealHtml, dealLink);
    }

    [RydrForcedSimpleCacheResponse(300)]
    public async Task<IHttpResult> Get(GetDealExternalHtml request)
    {
        var dealLinkAssociation = await _associationService.GetAssociationsToAsync(request.DealLink, RecordType.Deal, RecordType.DealLink)
                                                           .FirstOrDefaultAsync();

        var (deal, dealHtml, _) = await GetDealExternalHtmlAsync(dealLinkAssociation.FromRecordId);

        _dealMetricService.Measure(DealTrackMetricType.XClicked, new DealResponse
                                                                 {
                                                                     Deal = deal
                                                                 });

        return new HttpResult(dealHtml, MimeTypes.HtmlUtf8);
    }
}

[RydrNeverCacheResponse("publisheracct", "places", "query")]
public class DealService : BaseAuthenticatedApiService
{
    private readonly IAutoQueryService _autoQueryService;
    private readonly ICacheClient _cacheClient;
    private readonly IDealMetricService _dealMetricService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAssociationService _associationService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IDealRequestService _dealRequestService;
    private readonly IDealService _dealService;
    private readonly IDecorateResponsesService _decorateResponsesService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IElasticSearchService _elasticSearchService;

    public DealService(IAutoQueryService autoQueryService,
                       IDealService dealService,
                       IDealRequestService dealRequestService,
                       IDecorateResponsesService decorateResponsesService,
                       IDeferRequestsService deferRequestsService,
                       IElasticSearchService elasticSearchService,
                       ICacheClient cacheClient,
                       IDealMetricService dealMetricService,
                       IPublisherAccountService publisherAccountService,
                       IWorkspaceService workspaceService,
                       IAssociationService associationService,
                       IRydrDataService rydrDataService)
    {
        _autoQueryService = autoQueryService;
        _dealService = dealService;
        _dealRequestService = dealRequestService;
        _decorateResponsesService = decorateResponsesService;
        _deferRequestsService = deferRequestsService;
        _elasticSearchService = elasticSearchService;
        _cacheClient = cacheClient;
        _dealMetricService = dealMetricService;
        _publisherAccountService = publisherAccountService;
        _workspaceService = workspaceService;
        _associationService = associationService;
        _rydrDataService = rydrDataService;
    }

    public async Task<OnlyResultResponse<DealResponse>> Get(GetDealByLink request)
    {
        var dealLinkAssociation = await _associationService.GetAssociationsToAsync(request.DealLink, RecordType.Deal, RecordType.DealLink)
                                                           .FirstOrDefaultAsync();

        var (dealResponse, dealRequestPublisherAccountId) = await DoGetDealResponseAsync(dealLinkAssociation.FromRecordId,
                                                                                         request.RequestedPublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                                                         request);

        dealResponse.CanBeRequested = await _dealRequestService.CanBeRequestedAsync(dealResponse.Deal.Id, dealRequestPublisherAccountId, request);

        return dealResponse.AsOnlyResultResponse();
    }

    public async Task<OnlyResultResponse<DealResponse>> Get(GetDeal request)
    {
        var (dealResponse, _) = await DoGetDealResponseAsync(request.Id,
                                                             request.RequestedPublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                             request);

        return dealResponse.AsOnlyResultResponse();
    }

    public async Task<OnlyResultsResponse<PublisherAccountProfile>> Get(GetDealInvites request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.Id);

        if ((dynDeal?.InvitedPublisherAccountIds).IsNullOrEmpty())
        {
            return new OnlyResultsResponse<PublisherAccountProfile>();
        }

        // More efficient here to get the sorted page of ids we want and then only fetch those
        // If the first page is being requested and the take is larger than the invite count, no need to sort and page
        var publisherAccountIds = request.Skip <= 0 && request.Take >= dynDeal.InvitedPublisherAccountIds.Count
                                      ? dynDeal.InvitedPublisherAccountIds
                                      : dynDeal.InvitedPublisherAccountIds
                                               .OrderBy(i => i)
                                               .Skip(request.Skip)
                                               .Take(request.Take);

        var results = await _publisherAccountService.GetPublisherAccountsAsync(publisherAccountIds)
                                                    .Select(p => p.ToPublisherAccountProfile())
                                                    .ToList();

        return results.AsOnlyResultsResponse();
    }

    public Task<OnlyResultResponse<StringIdResponse>> Get(GetDealExternalLink request)
        => _dealService.GetDealAsync(request.Id)
                       .Then(d => d.ToDealPublicLinkId()
                                   .Transform(l => new StringIdResponse
                                                   {
                                                       Id = l
                                                   }))
                       .AsOnlyResultResponseAsync();

    [RydrCacheResponse(90)]
    public async Task<RydrQueryResponse<DealResponse>> Get(QueryPublisherDeals request)
    {
        RydrQueryResponse<DealResponse> response = null;

        var publisherAccountId = request.Id.Gz(request.RequestPublisherAccountId);

        var myContextWorkspaceId = (await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId)).GetContextWorkspaceId(request.WorkspaceId);

        request.IsPrivateDeal = null;

        // If a system request, we do nothing really with the context and we only include the workspace filter if one is passed...
        if (request.IsSystemRequest)
        {
            request.IncludeWorkspace = request.WorkspaceId > GlobalItemIds.MinUserDefinedObjectId;
        }
        else if (myContextWorkspaceId > 0)
        { // For all other normal requests, we include workspace context filtering always if the context is non-zero
            request.IncludeWorkspace = true;
            request.WorkspaceId = myContextWorkspaceId;
        }

        if (publisherAccountId > 0 && request.CanQueryDynamo())
        {
            var originalTake = request.Take.Value;

            if (!request.IncludeWorkspace)
            {
                request.Take = (request.Take.Value * 3).MaxGz(100);
            }

            request.Id = publisherAccountId;

            var totalRecordsRetrieved = 0;
            var results = new List<DealResponse>(request.Take.Value);

            response = new RydrQueryResponse<DealResponse>
                       {
                           Offset = request.Skip.Value
                       };

            do
            {
                var indexResponse = await _autoQueryService.QueryDataAsync<QueryPublisherDeals, DynItemIdTypeReferenceGlobalIndex>(request, Request);

                if ((indexResponse?.Results).IsNullOrEmpty())
                {
                    break;
                }

                if (response.Total <= 0 && indexResponse.Total > 0)
                {
                    response.Total = indexResponse.Total;
                    response.ResponseStatus = indexResponse.ResponseStatus;
                }

                totalRecordsRetrieved += indexResponse.Results.Count;

                var dynDeals = await _dynamoDb.QueryItemsAsync<DynDeal>(indexResponse.Results.Select(r => r.GetDynamoId()))
                                              .Where(d => d.WorkspaceId == myContextWorkspaceId ||
                                                          d.DealContextWorkspaceId == myContextWorkspaceId)
                                              .OrderByDescending(d => d.ReferenceId.ToLong(0))
                                              .ToList(indexResponse.Results.Count);

                var maps = await _dealService.GetDealMapsForTransformAsync(dynDeals);

                results.AddRange(dynDeals.Select(w => w.ToDealResponse(maps.PublisherMediaMap, maps.PlaceMap, maps.HashtagMap, maps.PublisherMap)));

                if (indexResponse.Results.Count < request.Take.Value)
                {
                    break;
                }

                request.Skip += request.Take;
            } while (results.Count < request.Take.Value && totalRecordsRetrieved <= 15000);

            response.Results = results.Count > originalTake
                                   ? results.Take(originalTake).AsList()
                                   : results;
        }
        else
        {
            response = await QueryDealsByEsAsync(new EsDealSearch
                                                 {
                                                     DealId = 0,
                                                     WorkspaceId = request.IncludeWorkspace
                                                                       ? request.WorkspaceId
                                                                       : 0,
                                                     ContextWorkspaceId = myContextWorkspaceId,
                                                     PublisherAccountId = publisherAccountId,
                                                     DealPublisherAccountId = publisherAccountId,
                                                     UserLatitude = request.UserLatitude,
                                                     UserLongitude = request.UserLongitude,
                                                     Latitude = request.Latitude,
                                                     Longitude = request.Longitude,
                                                     Miles = request.Miles,
                                                     BoundingBox = request.BoundingBox,
                                                     Search = request.Search.ToNullIfEmpty(),
                                                     PlaceId = request.PlaceId.GetValueOrDefault(),
                                                     IncludeInactive = request.IncludeDeleted,
                                                     IncludeExpired = request.IncludeExpired ?? true,
                                                     Skip = request.Skip.Value,
                                                     Take = request.Take.Value,
                                                     IdsOnly = true,
                                                     Sort = request.Sort,
                                                     PrivateDealOption = PrivateDealOption.All,
                                                     DealStatuses = request.Status,
                                                     ExcludeGroupIds = null,
                                                     Grouping = DealSearchGroupOption.None, // Do not group deals for purposes of the business viewing them themselves
                                                     DealTypes = request.DealTypes,
                                                     Tags = request.Tags,
                                                     AgeRange = null,
                                                     FollowerCount = request.FollowerCount,
                                                     EngagementRating = request.EngagementRating,
                                                     Value = null,
                                                     RequestCount = request.RequestCount,
                                                     RemainingQuantity = request.RemainingQuantity,
                                                     CreatedBetween = request.CreatedBetween,
                                                     PublishedBetween = request.PublishedBetween
                                                 },
                                                 (dd, m) => dd.ToDealResponse(m.PublisherMediaMap, m.PlaceMap, m.HashtagMap, m.PublisherMap));
        }

        var dealStatsMap = await _dealService.GetDealStatsAsync(response.Results.Select(r => r.Deal));

        var publisherAccountMap = await _publisherAccountService.GetPublisherAccountsAsync(response.Results.Select(r => r.PublisherAccountId))
                                                                .ToDictionarySafe(p => p.PublisherAccountId, p =>
                                                                                                             {
                                                                                                                 var info = p.ToPublisherAccountInfo();
                                                                                                                 info.Metrics = null;

                                                                                                                 return info;
                                                                                                             });

        foreach (var result in response.Results)
        {
            if (result.Deal.Status == DealStatus.Published || result.Deal.Status == DealStatus.Paused)
            {
                result.PendingRecentRequesters = await _cacheClient.TryGetAsync(string.Concat("RecentPendingRequests|", result.Deal.Id),
                                                                                () => _publisherAccountService.GetPublisherAccountsAsync(_dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == result.Deal.Id &&
                                                                                                                                                                                                          Dynamo.Between(i.TypeReference,
                                                                                                                                                                                                                         DynamoDealRequestService.DealRequestTypeRefBetweenMinMax[0],
                                                                                                                                                                                                                         DynamoDealRequestService.DealRequestTypeRefBetweenMinMax[1]))
                                                                                                                                                  .Filter(i => i.DeletedOnUtc == null &&
                                                                                                                                                               i.TypeId == (int)DynItemType.DealRequest &&
                                                                                                                                                               Dynamo.In(i.StatusId, DealEnumHelpers.PendingDealRequestStatuses))
                                                                                                                                                  .PagingLimit(15.ToDynamoBatchCeilingTake())
                                                                                                                                                  .QueryAsync(_dynamoDb)
                                                                                                                                                  .OrderByDescending(i => i.TypeReference)
                                                                                                                                                  .Take(5)
                                                                                                                                                  .Select(i => i.EdgeId.ToLong(0))) // This is the PublisherAccountId
                                                                                                              .Select(p => p.ToPublisherAccountProfile())
                                                                                                              .ToList(),
                                                                                CacheConfig.FromHours(6));
            }

            result.Stats = dealStatsMap.ContainsKey(result.Deal.Id)
                               ? dealStatsMap[result.Deal.Id].Select(s => s.ToDealStat())
                                                             .AsList()
                               : null;

            result.PopulateApprovalsRemaining();

            result.PublisherAccount = publisherAccountMap.GetValueOrDefaultSafe(result.PublisherAccountId);
        }

        return response;
    }

    // [RydrCacheResponse(120)] - not caching this now, as it is nearly always queried with a lat/long to the 12th decimal...so it varies significantly
    public async Task<RydrQueryResponse<DealResponse>> Get(QueryPublishedDeals request)
    {
        DealResponse toQueryDealResponse(DynDeal dynDeal,
                                         (Dictionary<long, PublisherMedia> PublisherMediaMap,
                                             Dictionary<long, Place> PlaceMap,
                                             Dictionary<long, Hashtag> HashtagMap,
                                             Dictionary<long, PublisherAccount> PublisherMap) maps)
        {
            var dealResponse = dynDeal.ToDealResponse(maps.PublisherMediaMap, maps.PlaceMap, maps.HashtagMap, maps.PublisherMap,
                                                      request.UserLatitude, request.UserLongitude,
                                                      request.PublisherAccountId.Gz(request.RequestPublisherAccountId));

            // Just model cleanup basically that is not used in this context/front-end wants to see a bit differently
            dealResponse.UnreadMessages = null;

            dealResponse.ScrubDeal();

            return dealResponse;
        }

        var queryResponse = await QueryPublishedDealsByEsAsync(request, toQueryDealResponse);

        if (queryResponse.Results.IsNullOrEmptyReadOnly())
        {
            queryResponse.Total = 0;
        }
        else
        {
            if (queryResponse.Total < queryResponse.Results.Count ||
                (request.Skip <= 0 && queryResponse.Results.Count < request.Take))
            {
                queryResponse.Total = queryResponse.Results.Count;
            }
            else if (queryResponse.Results.Count < request.Take)
            {
                queryResponse.Total = request.Skip + queryResponse.Results.Count;
            }

            await _decorateResponsesService.DecorateAsync(request, queryResponse);
        }

        if (!request.IsSystemRequest && request.RequestPublisherAccountId > 0)
        {
            _dealMetricService.Measure(DealTrackMetricType.Impressed, queryResponse.Results, request.RequestPublisherAccountId, request.WorkspaceId, request, request.UserId);
        }

        return queryResponse;
    }

    public async Task<LongIdResponse> Post(PostDeal request)
    {
        await PreProcessSetDealAsync(request);

        var dynDeal = request.ToDynDeal();

        // On a new deal, if the deal does not have any associated media, we go get the last image we have synced from the account...
        if (dynDeal.PublisherMediaIds.IsNullOrEmpty())
        {
            var mostRecentPublisherMediaId = (await _dynamoDb.FromQuery<DynPublisherMedia>(pm => pm.Id == dynDeal.PublisherAccountId &&
                                                                                                 Dynamo.BeginsWith(pm.EdgeId, "00"))
                                                             .Filter(pm => pm.DeletedOnUtc == null &&
                                                                           pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                           pm.ContentType == PublisherContentType.Post &&
                                                                           Dynamo.Contains(pm.MediaType, "IMAGE") &&
                                                                           Dynamo.AttributeExists(pm.MediaUrl))
                                                             .QueryColumnAsync(pm => pm.EdgeId, _dynamoDb)
                                                             .FirstOrDefaultAsync()
                                             ).ToLong(0);

            if (mostRecentPublisherMediaId > 0)
            {
                dynDeal.PublisherMediaIds = new HashSet<long>
                                            {
                                                mostRecentPublisherMediaId
                                            };
            }
        }

        await _dynamoDb.PutItemAsync(dynDeal);

        _deferRequestsService.DeferPrimaryDealRequest(new DealPosted
                                                      {
                                                          DealId = dynDeal.DealId
                                                      });

        return dynDeal.DealId.ToLongIdResponse();
    }

    public async Task<LongIdResponse> Put(PutDeal request)
    {
        await PreProcessSetDealAsync(request);

        var existingDeal = await _dealService.GetDealAsync(request.Id);

        var dealUpdatedModel = new DealUpdated
                               {
                                   DealId = request.Model.Id,
                                   OccurredOn = _dateTimeProvider.UtcNowTs,
                                   FromStatus = existingDeal.DealStatus,
                                   Reason = request.Reason
                               };

        if (!request.Model.InvitedPublisherAccounts.IsNullOrEmpty())
        {
            dealUpdatedModel.NewlyInvitedPublisherAccountIds = request.Model.InvitedPublisherAccounts
                                                                      .Select(i => i.Id)
                                                                      .Except(existingDeal.InvitedPublisherAccountIds ?? new HashSet<long>())
                                                                      .AsHashSet();
        }

        // New status update - if the request includes a status change that is different from the current status, it's new
        dealUpdatedModel.ToStatus = request.Model.Status == DealStatus.Unknown ||
                                    request.Model.Status == existingDeal.DealStatus
                                        ? DealStatus.Unknown
                                        : request.Model.Status;

        if (dealUpdatedModel.ToStatus != DealStatus.Unknown && dealUpdatedModel.ToStatus != DealStatus.Published && dealUpdatedModel.ToStatus != DealStatus.Paused)
        { // Need to explicitly unset the reference here, as it will be treated as the default and ignored in the UpdateFromExisting method
            request.Unset = request.Unset.CreateOrAdd("ReferenceId");
        }

        if (request.Model.AutoApproveRequests.HasValue && !request.Model.AutoApproveRequests.Value)
        { // Same here, need to unset AutoApprove if it is set, as false will be the default on the dyn model
            request.Unset = request.Unset.CreateOrAdd("AutoApproveRequests");
        }

        var updatedDeal = await _dynamoDb.UpdateFromExistingAsync(existingDeal, request.ToDynDeal, request);

        dealUpdatedModel.DealId = updatedDeal.DealId;

        _deferRequestsService.DeferPrimaryDealRequest(dealUpdatedModel);

        return updatedDeal.DealId.ToLongIdResponse();
    }

    public async Task Put(PutDealInvites request)
    {
        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        await GetOrPostPublisherAccounts(request.PublisherAccounts, request);

        var invitedIds = request.PublisherAccounts
                                .Select(p => p.Id)
                                .Where(i => i > 0)
                                .AsHashSet();

        if (dynDeal.InvitedPublisherAccountIds == null)
        {
            dynDeal.InvitedPublisherAccountIds = invitedIds;
        }
        else
        {
            dynDeal.InvitedPublisherAccountIds.UnionWith(invitedIds);
        }

        await _dynamoDb.PutItemTrackedAsync(dynDeal);

        _deferRequestsService.DeferPrimaryDealRequest(new DealUpdated
                                                      {
                                                          DealId = request.DealId,
                                                          NewlyInvitedPublisherAccountIds = invitedIds
                                                      });
    }

    public Task Delete(DeleteDeal request)
        => _adminServiceGatewayFactory().SendAsync(new DeleteDealInternal
                                                   {
                                                       DealId = request.Id,
                                                       Reason = request.Reason
                                                   }.PopulateWithRequestInfo(request));

    private async Task<(DealResponse DealResponse, long DealRequestPublisherAccountId)> DoGetDealResponseAsync<T>(long dealId, long publisherAccountId, T request)
        where T : GetDealBase
    {
        var deal = await _dealService.GetDealAsync(dealId, true);

        var myPublisher = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

        publisherAccountId = myPublisher?.PublisherAccountId ?? 0;

        var dealResponse = await deal.ToDealResponseAsync(request.UserLatitude, request.UserLongitude,
                                                          publisherAccountId.Gz(request.RequestedPublisherAccountId)
                                                                            .Gz(request.RequestPublisherAccountId));

        var dealRequestExtended = publisherAccountId > 0
                                      ? await _dealRequestService.GetDealRequestExtendedAsync(dealResponse.Deal.Id, publisherAccountId)
                                      : null;

        dealResponse.DealRequest = await dealRequestExtended.ToDealRequestAsync();

        dealResponse.ScrubDeal();

        dealResponse.Stats = (await _dealService.GetDealStatsAsync(dealResponse.Deal.Id)).Select(ds => ds.ToDealStat())
                                                                                         .AsList();

        dealResponse.PopulateApprovalsRemaining();

        // Want completed and redeemed...so get from start of completed to end of redeemed, then filter out others
        dealResponse.RecentCompleters = await _publisherAccountService.GetPublisherAccountsAsync(_dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == dealId &&
                                                                                                                                                                  Dynamo.Between(i.TypeReference,
                                                                                                                                                                                 DynDealRequestStatusChange.CompletedStatusChangeTypeReferenceBetweenMinMax[0],
                                                                                                                                                                                 DynDealRequestStatusChange.RedeemedStatusChangeTypeReferenceBetweenMinMax[1]))
                                                                                                          .Filter(i => i.DeletedOnUtc == null &&
                                                                                                                       i.TypeId == (int)DynItemType.DealRequestStatusChange &&
                                                                                                                       Dynamo.In(i.StatusId, DealEnumHelpers.CompletedRedeemedDealRequestStatuses))
                                                                                                          .PagingLimit(15.ToDynamoBatchCeilingTake())
                                                                                                          .QueryAsync(_dynamoDb)
                                                                                                          .OrderByDescending(i => DynItem.GetFinalEdgeSegment(i.TypeReference).ToLong(0))
                                                                                                          .Select(i => DynItem.GetFirstEdgeSegment(i.EdgeId).ToLong(0)) // This is the PublisherAccountId
                                                                                                          .Distinct()
                                                                                                          .Take(10))
                                                                      .Select(p => p.ToPublisherAccountProfile())
                                                                      .ToList(10);

        // Counts as a deal click if this is being viewed by an influencer
        if (!request.IsSystemRequest && myPublisher != null && myPublisher.RydrAccountType.IsInfluencer())
        {
            _dealMetricService.Measure(DealTrackMetricType.Clicked, dealResponse, myPublisher.Id, request.WorkspaceId, request, request.UserId);
        }

        return (dealResponse, publisherAccountId);
    }

    private async Task PreProcessSetDealAsync(BaseSetRequest<Deal> request)
    {
        if (request.Model.ReceiveNotes.HasValue())
        {
            var hashTagMatches = Regex.Matches(request.Model.ReceiveNotes, @"[#@].+?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!hashTagMatches.IsNullOrEmpty())
            {
                request.Model.ReceiveHashtags = (request.Model.ReceiveHashtags
                                                 ??
                                                 Enumerable.Empty<Hashtag>()).Concat(hashTagMatches.Select(m => new Hashtag
                                                                                                                {
                                                                                                                    PublisherType = PublisherType.Facebook,
                                                                                                                    Name = m.Value,
                                                                                                                    HashtagType = m.Value.StartsWithOrdinalCi("@")
                                                                                                                                      ? HashtagType.Mention
                                                                                                                                      : HashtagType.Hashtag
                                                                                                                }))
                                                                             .Select(ht =>
                                                                                     {
                                                                                         ht.Name = ht.Name.TrimStart('#', '@').Trim();

                                                                                         return ht;
                                                                                     })
                                                                             .Distinct()
                                                                             .AsList();
            }
        }

        if (request.Model.ReceivePublisherAccounts != null)
        {
            await GetOrPostPublisherAccounts(request.Model.ReceivePublisherAccounts, request);
        }

        if (request.Model.InvitedPublisherAccounts != null)
        {
            await GetOrPostPublisherAccounts(request.Model.InvitedPublisherAccounts, request);
        }

        if (!request.Model.ReceiveHashtags.IsNullOrEmpty())
        {
            await PostUpsertModelsAsync<PostHashtagUpsert, Hashtag>(request.Model.ReceiveHashtags, request);
        }

        if (request.Model.Place != null && (request.Model.Place.HasUpsertData() || request.Model.Place.Id <= 0))
        {
            await PutOrPostModelAsync<PutPlace, PostPlace, Place>(request.Model.Place, request);
        }

        if (request.Model.ReceivePlace != null && (request.Model.ReceivePlace.HasUpsertData() || request.Model.ReceivePlace.Id <= 0))
        {
            await PutOrPostModelAsync<PutPlace, PostPlace, Place>(request.Model.ReceivePlace, request);
        }

        if (!request.Model.PublisherMedias.IsNullOrEmpty())
        { // Only upsert anything that is ID-less...
            foreach (var publisherMedia in request.Model.PublisherMedias.Where(pm => pm.HasUpsertData()))
            {
                await PostUpsertModelAsync<PostPublisherMediaUpsert, PublisherMedia>(publisherMedia, request, true);
            }

            if (request.Model.PublisherMedias.Any(pm => pm.Id <= 0))
            {
                request.Model.PublisherMedias = request.Model.PublisherMedias.Where(pm => pm.Id > 0).AsList();
            }
        }
    }

    private async Task GetOrPostPublisherAccounts(List<PublisherAccount> publisherAccounts, IRequestBase request)
    {
        // If the account exists, use the ID from it, otherwise POST/PUT it to create and get
        foreach (var publisherAccount in publisherAccounts.Where(p => p.Id <= 0))
        {
            var writableAlternateAccountType = publisherAccount.Type.WritableAlternateAccountType();

            // If the publisher info sent is from a non-writable publisher, see if we have a matching publisher account in the system already from the
            // writable alternative (i.e. if instagram account info is sent up, see if we have a matching facebook account for it in the system, if so that's the pub acct)
            if (writableAlternateAccountType != PublisherType.Unknown && !publisherAccount.Type.IsWritablePublisherType() &&
                publisherAccount.AccountType != PublisherAccountType.Unknown && publisherAccount.RydrAccountType != RydrAccountType.None &&
                publisherAccount.AccountId.HasValue() && publisherAccount.UserName.HasValue())
            {
                var writablePublisherAccount = await _rydrDataService.TrySingleAsync<RydrPublisherAccount>(p => p.AlternateAccountId == publisherAccount.AccountId &&
                                                                                                                p.UserName == publisherAccount.UserName &&
                                                                                                                p.PublisherType == writableAlternateAccountType &&
                                                                                                                p.AccountType == publisherAccount.AccountType &&
                                                                                                                p.RydrAccountType == publisherAccount.RydrAccountType);

                if (writablePublisherAccount != null)
                { // Found a matching writable alternate account, inject that and use it
                    var dynWritablePublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(writablePublisherAccount.Id);

                    if (dynWritablePublisherAccount != null)
                    {
                        publisherAccount.PopulateWith(dynWritablePublisherAccount.ToPublisherAccount());

                        publisherAccount.Id = dynWritablePublisherAccount.PublisherAccountId;

                        continue;
                    }
                }
            }

            // See if there is an existing account by type/accountid combination
            var existingAccountByType = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccount.Type, publisherAccount.AccountId);

            if (existingAccountByType != null)
            {
                publisherAccount.Id = existingAccountByType.PublisherAccountId;

                continue;
            }

            // Couldn't find it, or doesn't have an existing id...upsert this thing...
            await PostUpsertModelAsync<PostPublisherAccountUpsert, PublisherAccount>(publisherAccount, request);
        }
    }

    private async Task<RydrQueryResponse<DealResponse>> QueryPublishedDealsByEsAsync(QueryPublishedDeals request,
                                                                                     Func<DynDeal, (Dictionary<long, PublisherMedia> PublisherMediaMap,
                                                                                         Dictionary<long, Place> PlaceMap,
                                                                                         Dictionary<long, Hashtag> HashtagMap,
                                                                                         Dictionary<long, PublisherAccount> PublisherMap), DealResponse> transform)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.RequestPublisherAccountId);

        var workspacePublisherAccount = await _workspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId);

        double getMaxMetricForQuery(string metricName, double defaultValue)
        {
            if (request.IsSystemRequest)
            {
                return int.MaxValue;
            }

            var metricValue = publisherAccount?.Metrics?.GetValueOrDefault(metricName);

            return metricValue.HasValue && metricValue > 0
                       ? metricValue.Value
                       : defaultValue;
        }

        // Filter by follower count, engagement rating...
        var publisherFollowerCount = (long)getMaxMetricForQuery(PublisherMetricName.FollowedBy, 1.1);

        var followerCount = request.FollowerCount ?? new LongRange(0, publisherFollowerCount);

        if (followerCount.Max > publisherFollowerCount)
        {
            if (publisherFollowerCount < followerCount.Min)
            {
                return new RydrQueryResponse<DealResponse>();
            }

            followerCount.Max = publisherFollowerCount;
        }

        // At the moment, use the largest eng rating related metric the user has...
        var publisherEngRating = getMaxMetricForQuery(PublisherMetricName.RecentEngagementRating,
                                                      0.001).MaxGz(getMaxMetricForQuery(PublisherMetricName.StoryEngagementRating,
                                                                                        0.001))
                                                            .MaxGz(getMaxMetricForQuery(PublisherMetricName.RecentTrueEngagementRating,
                                                                                        0.001));

        var engRating = request.EngagementRating ?? new DoubleRange(0, publisherEngRating);

        if (engRating.Max > publisherEngRating)
        {
            if (publisherEngRating < engRating.Min)
            {
                return new RydrQueryResponse<DealResponse>();
            }

            engRating.Max = publisherEngRating;
        }

        var ageRangeMax = (publisherAccount?.AgeRangeMax ?? 0).MaxGz(workspacePublisherAccount?.AgeRangeMax ?? 0).Gz(request.IsSystemRequest
                                                                                                                         ? 100
                                                                                                                         : 20);

        var publishedAfter = request.PublishedAfter.ToUnixTimestamp() ?? 0;

        var esQuery = new EsDealSearch
                      {
                          DealId = request.DealId.GetValueOrDefault(),
                          WorkspaceId = 0,
                          ContextWorkspaceId = 0,
                          PublisherAccountId = request.RequestPublisherAccountId,
                          DealPublisherAccountId = request.Id,
                          UserLatitude = request.UserLatitude,
                          UserLongitude = request.UserLongitude,
                          Latitude = request.Latitude,
                          Longitude = request.Longitude,
                          Miles = request.Miles.GetValueOrDefault(),
                          BoundingBox = request.BoundingBox,
                          Search = null,
                          PlaceId = request.PlaceId.GetValueOrDefault(),
                          IncludeInactive = request.IncludeDeleted,
                          IncludeExpired = false,
                          Skip = request.Skip,
                          Take = request.Take,
                          IdsOnly = true,
                          Sort = request.Sort,
                          PrivateDealOption = request.IsPrivateDeal.HasValue
                                                  ? request.IsPrivateDeal.Value
                                                        ? PrivateDealOption.PrivateOnly
                                                        : PrivateDealOption.PublicAndInvited //PrivateDealOption.PublicOnly
                                                  : PrivateDealOption.PublicAndInvited,
                          DealStatuses = new[]
                                         {
                                             DealStatus.Published
                                         },
                          ExcludeGroupIds = request.RequestPublisherAccountId > 0
                                                ? await _dealRequestService.GetActiveDealGroupIdsAsync(request.RequestPublisherAccountId)
                                                : null,
                          Grouping = request.Grouping == DealSearchGroupOption.None
                                         ? DealSearchGroupOption.Default
                                         : request.Grouping,
                          DealTypes = request.DealTypes,
                          Tags = request.Tags.NullIfEmpty(),
                          AgeRange = new IntRange(request.MinAge.Gz(0), ageRangeMax),
                          FollowerCount = followerCount,
                          EngagementRating = engRating,
                          Value = request.Value,
                          RequestCount = null,
                          RemainingQuantity = IntRange.FromMin(1), // For published queries, has to be some remaining to display
                          CreatedBetween = null,
                          PublishedBetween = publishedAfter > 0
                                                 ? LongRange.FromMin(publishedAfter)
                                                 : null
                      };

        var result = await QueryDealsByEsAsync(esQuery, transform,
                                               d => _dealRequestService.CanBeRequestedAsync(d, request.RequestPublisherAccountId, readOnlyIntent: true, withState: request));

        return result;
    }

    private async Task<RydrQueryResponse<DealResponse>> QueryDealsByEsAsync(EsDealSearch esQuery,
                                                                            Func<DynDeal, (Dictionary<long, PublisherMedia> PublisherMediaMap,
                                                                                Dictionary<long, Place> PlaceMap,
                                                                                Dictionary<long, Hashtag> HashtagMap,
                                                                                Dictionary<long, PublisherAccount> PublisherMap), DealResponse> transform,
                                                                            Func<DynDeal, Task<bool>> searchIdPredicate = null)
    {
        var queryResponse = new RydrQueryResponse<DealResponse>
                            {
                                Offset = esQuery.Skip
                            };

        var dynDealResults = new List<DynDeal>(esQuery.Take);
        var loops = 0;

        do
        {
            var esResults = await _elasticSearchService.SearchDealsAsync(esQuery);

            if (queryResponse.Total <= 0 && esResults != null && esResults.TotalHits > 0)
            {
                queryResponse.Total = (int)esResults.TotalHits;
            }

            var searchIds = esResults?.Results?
                                     .Where(r => r.DealId > 0)
                                     .GroupBy(r => r.DealId)
                                     .Select(g => (DealId: g.Key, DynamoId: new DynamoId(g.First().PublisherAccountId, g.Key.ToEdgeId())))
                                     .AsList();

            if (searchIds.IsNullOrEmpty())
            {
                break;
            }

            var dynDeals = await _dynamoDb.QueryItemsAsync<DynDeal>(searchIds.Select(i => i.DynamoId))
                                          .ToList(searchIds.Count);

            // Simple optimization here to avoid running the searchIdFilter more times than needed when possible
            var rangeTake = dynDealResults.Count <= 0
                                ? dynDeals.Count
                                : esQuery.Take - dynDealResults.Count;

            if (searchIdPredicate == null)
            {
                dynDealResults.AddRange(dynDeals);
            }
            else
            {
                foreach (var dynDeal in dynDeals)
                {
                    var include = await searchIdPredicate(dynDeal);

                    if (include)
                    {
                        dynDealResults.Add(dynDeal);

                        if (dynDealResults.Count >= rangeTake)
                        {
                            break;
                        }
                    }
                }
            }

            if (searchIds.Count < esQuery.Take)
            {
                break;
            }

            esQuery.Skip += esQuery.Take;
            loops++;
        } while (dynDealResults.Count < esQuery.Take && loops <= 50);

        var maps = await _dealService.GetDealMapsForTransformAsync(dynDealResults);

        queryResponse.Results = OrderDeals(dynDealResults, esQuery.Sort, d => transform(d, maps)).AsListReadOnly();

        return queryResponse;
    }

    private static IEnumerable<DealResponse> OrderDeals(IEnumerable<DynDeal> deals, DealSort bySort, Func<DynDeal, DealResponse> transform)
    {
        if (deals == null)
        {
            return Enumerable.Empty<DealResponse>();
        }

        return bySort switch
               {
                   DealSort.Closest => deals.Select(transform).OrderBy(d => d.DistanceInMiles ?? d.Deal.Id).ThenByDescending(d => d.Deal.Id),
                   DealSort.Expiring => deals.OrderBy(d => d.ExpirationDate.ToUnixTimestamp().Gz(d.DealId)).ThenByDescending(d => d.DealId).Select(transform),
                   DealSort.FollowerValue => deals.OrderByDescending(d => d.Restrictions?.FirstOrDefault(r => r.Type == DealRestrictionType.MinFollowerCount)?.Value.ToLong(0) ?? d.Value).ThenByDescending(d => d.DealId).Select(transform),
                   _ => deals.OrderByDescending(d => d.PublishedOn.ToUnixTimestamp() ?? d.DealId).ThenByDescending(d => d.DealId).Select(transform)
               };
    }
}
