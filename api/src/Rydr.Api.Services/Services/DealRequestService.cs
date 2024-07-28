using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.QueryDto;
using Rydr.Api.QueryDto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

public class DealRequestPublicService : BaseApiService
{
    private readonly IDealService _dealService;
    private readonly IDealRequestService _dealRequestService;

    public DealRequestPublicService(IDealService dealService,
                                    IDealRequestService dealRequestService)
    {
        _dealService = dealService;
        _dealRequestService = dealRequestService;
    }

    [RydrForcedSimpleCacheResponse(300)]
    public async Task<OnlyResultResponse<DealResponse>> Get(GetDealRequestReportExternal request)
    {
        var dealMap = await MapItemService.DefaultMapItemService
                                          .TryGetMapByHashedEdgeAsync(DynItemType.DealRequest, request.DealRequestReportId);

        var dynDeal = await _dealService.GetDealAsync(dealMap.ReferenceNumber.Value);
        var dynDealRequest = await _dealRequestService.GetDealRequestAsync(dynDeal.DealId, dealMap.MappedItemEdgeId.ToLong(0));
        var dealResponse = await dynDeal.ToDealResponseAsync(requestedBy: dynDealRequest.PublisherAccountId);

        var dealRequestExtended = await _dealRequestService.GetDealRequestExtendedAsync(dynDealRequest.DealId, dynDealRequest.PublisherAccountId);

        // NOTE: Purposely scrub before setting the deal request...
        dealResponse.ScrubDeal();

        dealResponse.DealRequest = await dealRequestExtended.ToDealRequestAsync();

        dealResponse.Stats = (await _dealService.GetDealStatsAsync(dealResponse.Deal.Id)).Select(ds => ds.ToDealStat())
                                                                                         .AsList();

        return dealResponse.AsOnlyResultResponse();
    }
}

[RydrNeverCacheResponse("publisheracct", "places", "query")]
public class DealRequestService : BaseAuthenticatedApiService
{
    private readonly IAutoQueryService _autoQueryService;
    private readonly IDealRequestService _dealRequestService;
    private readonly IDealService _dealService;
    private readonly IDecorateResponsesService _decorateResponsesService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IDeferRequestsService _deferRequestsService;

    public DealRequestService(IDealRequestService dealRequestService,
                              IDeferRequestsService deferRequestsService,
                              IAutoQueryService autoQueryService,
                              IDealService dealService,
                              IDecorateResponsesService decorateResponsesService,
                              IRydrDataService rydrDataService,
                              IPublisherAccountService publisherAccountService,
                              IWorkspaceService workspaceService)
    {
        _dealRequestService = dealRequestService;
        _deferRequestsService = deferRequestsService;
        _autoQueryService = autoQueryService;
        _dealService = dealService;
        _decorateResponsesService = decorateResponsesService;
        _rydrDataService = rydrDataService;
        _publisherAccountService = publisherAccountService;
        _workspaceService = workspaceService;
    }

    public async Task<OnlyResultResponse<DealRequest>> Get(GetDealRequest request)
    {
        var publisherAccountId = request.ToPublisherAccountId();

        var dealRequestExtended = await _dealRequestService.GetDealRequestExtendedAsync(request.DealId, publisherAccountId);

        var dealRequest = await dealRequestExtended.ToDealRequestAsync();

        return dealRequest.AsOnlyResultResponse();
    }

    public async Task<OnlyResultResponse<StringIdResponse>> Get(GetDealRequestReportExternalLink request)
    {
        var publisherAccountId = request.ToPublisherAccountId();
        var nowUtc = _dateTimeProvider.UtcNowTs;
        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        var linkId = dynDeal.ToDealPublicLinkId(string.Concat(Guid.NewGuid().ToString(), "|", publisherAccountId, "|", nowUtc, "|", request.UserId, "|"));

        var linkIdHashCode = linkId.ToLongHashCode();

        await MapItemService.DefaultMapItemService
                            .PutMapAsync(new DynItemMap
                                         {
                                             Id = linkIdHashCode,
                                             EdgeId = DynItemMap.BuildEdgeId(DynItemType.DealRequest, linkId),
                                             MappedItemEdgeId = publisherAccountId.ToStringInvariant(),
                                             ReferenceNumber = dynDeal.DealId,
                                             ExpiresAt = nowUtc + request.Duration.Gz(100_000) // Defaults to about 30 hours from now
                                         });

        return new StringIdResponse
               {
                   Id = linkId
               }.AsOnlyResultResponse();
    }

    [RydrCacheResponse(90)]
    public async Task<RydrQueryResponse<DealResponse>> Get(QueryDealRequests request)
    { // Requests for deals the user has created/owns (i.e. typically a business making this call)
        // NOTE however that this endpoint returns a record PER REQUEST for each deal - i.e. a DEAL can be repeated multiple times for multiple requests
        RydrQueryResponse<DealResponse> response = null;

        var dealId = request.Id.Gz(0);

        var publisherAccountId = request.OwnerId.Gz(request.RequestPublisherAccountId);
        var workspaceId = request.WorkspaceId;

        var myContextWorkspaceId = (await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId)).GetContextWorkspaceId(workspaceId);

        // Deal requests are displayed on a per-workspace basis, showing requests for deals that originated in the given workspace only
        request.IncludeWorkspace = myContextWorkspaceId > 0;
        request.WorkspaceId = myContextWorkspaceId;

        if ((dealId <= 0 && publisherAccountId <= 0) || request.Search.HasValue())
        { // Full workspace or total query or includes a user-prefix search...use sql...
            // Admins and workspace owners can query the entire workspace, otherwise limit to publisherAccounts associated with the user in question
            var canQueryEntireWorkspace = request.IsSystemRequest || await _workspaceService.IsWorkspaceAdmin(workspaceId, request.UserId);

            var sql = string.Concat(@"
SELECT    dr.DealId, dr.PublisherAccountId
FROM      Deals d
JOIN      DealRequests dr
ON        d.Id = dr.DealId
WHERE     d.DeletedOn IS NULL",
                                    dealId <= 0
                                        ? string.Empty
                                        : @"
          AND d.Id = @DealId
          AND dr.DealId = @DealId",
                                    workspaceId <= 0 || (request.IsSystemRequest && workspaceId < GlobalItemIds.MinUserDefinedObjectId)
                                        ? string.Empty
                                        : @"
          AND d.WorkspaceId = @WorkspaceId",
                                    request.EdgeId.IsNullOrEmpty()
                                        ? string.Empty
                                        : @"
          AND dr.PublisherAccountId = @DealRequestPublisherAccountId",
                                    request.Status.IsNullOrEmpty()
                                        ? string.Empty
                                        : string.Concat(@"
          AND dr.Status IN(", string.Join(",", request.Status.Select(s => (int)s)), ")"),
                                    canQueryEntireWorkspace
                                        ? string.Empty
                                        : @"
          AND EXISTS
          (
          SELECT   NULL
          FROM     WorkspaceUserPublisherAccounts wupa
          WHERE    wupa.UserId = @UserId
                   AND wupa.WorkspaceId = @WorkspaceId
                   AND wupa.DeletedOn IS NULL
                   AND wupa.PublisherAccountId = d.PublisherAccountId
          )",
                                    request.Search.IsNullOrEmpty()
                                        ? string.Empty
                                        : @"
          AND EXISTS
          (
          SELECT  NULL
          FROM    PublisherAccounts dpa
          WHERE   dpa.Id = d.PublisherAccountId
                  AND dpa.UserName LIKE (@UserSearch)
          UNION ALL
          SELECT  NULL
          FROM    PublisherAccounts dra
          WHERE   dra.Id = dr.PublisherAccountId
                  AND dra.UserName LIKE (@UserSearch)
          )", @"
ORDER BY  dr.StatusUpdatedOn DESC, dr.DealId DESC
LIMIT     @Take
OFFSET    @Skip;
");

            var dealRequestIds = await _rydrDataService.QueryAdHocAsync(db => db.SelectAsync<(long, long)>(sql, new
                                                                                                                {
                                                                                                                    Take = request.Take.Value,
                                                                                                                    Skip = request.Skip.Value,
                                                                                                                    WorkspaceId = workspaceId,
                                                                                                                    DealRequestPublisherAccountId = request.EdgeId.ToLong(0),
                                                                                                                    request.UserId,
                                                                                                                    UserSearch = string.Concat(request.Search, "%")
                                                                                                                }));

            var dealRequests = await _dynamoDb.QueryItemsAsync<DynDealRequest>(dealRequestIds.Select(t => new DynamoId(t.Item1, t.Item2.ToEdgeId())))
                                              .ToList(dealRequestIds.Count);

            var results = await _dealRequestService.GetDealResponseRequestExtendedAsync(dealRequests)
                                                   .OrderByDescending(dr => dr.DealRequest.StatusLastChangedOn)
                                                   .ToListReadOnly(dealRequests.Count);

            response = new RydrQueryResponse<DealResponse>
                       {
                           Offset = request.Skip.Value,
                           Total = dealRequests.Count,
                           Results = results
                       };

            await _decorateResponsesService.DecorateAsync(request, response);
        }
        else if (dealId > 0)
        { // Have a specific dealId, use it
            var indexRequest = request.ConvertTo<QueryRequestedDeals>();

            indexRequest.Id = dealId;
            indexRequest.EdgeId = request.EdgeId;
            indexRequest.RequestedOnBefore = null;

            response = await QueryRequestsByDealIdAsync(indexRequest);
        }
        else
        { // Standard query to get dealRequests sent to a given business (i.e. get all deal requests for a given business)
            request.TypeOwnerSpace = DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequest, publisherAccountId);

            request.ReferenceIdBetween = new[]
                                         {
                                             "1500000000", _dateTimeProvider.UtcNowTs.ToStringInvariant()
                                         };

            response = await QueryRequestsByDealOwnerAsync(request);
        }

        return response;
    }

    public async Task<RydrQueryResponse<DealResponse>> Get(QueryRequestedDeals request)
    { // Deals (and request info) the user has requested (i.e. typically an influencer making this call)
        var queryResponse = new RydrQueryResponse<DealResponse>
                            {
                                Offset = request.Skip.Value
                            };

        if (request.Id.GetValueOrDefault() > 0)
        { // Deal included, query by the hash
            if (request.TypeReferenceBetween.IsNullOrEmpty())
            {
                request.RequestedOnBefore = null;
            }

            queryResponse = await QueryRequestsByDealIdAsync(request);
        }
        else
        { // Deal not included, query by edge index (publisher account id)
            var indexRequest = request.ConvertTo<QueryRequestedDealsByPublisherId>();

            indexRequest.WorkspaceId = (await _publisherAccountService.GetPublisherAccountAsync(request.RequestPublisherAccountId)
                                       ).GetContextWorkspaceId(request.WorkspaceId);

            indexRequest.RequestedBefore = request.RequestedOnBefore;

            indexRequest.StatusId = request.Status.IsNullOrEmpty()
                                        ? null
                                        : request.Status.Select(s => s.ToString()).ToArray();

            queryResponse = await QueryRequestedDealsByPublisherIdAsync(indexRequest);
        }

        return queryResponse;
    }

    public async Task Post(PostDealRequest request)
    {
        var publisherAccountId = request.ToPublisherAccountId();

        // Shim until we remove the DaysUntilDelinquent for backward compat...
#pragma warning disable 618
        if (request.DaysUntilDelinquent > 0 && request.HoursAllowedRedeemed <= 0)
        {
            request.HoursAllowedRedeemed = request.DaysUntilDelinquent * 24;
        }
#pragma warning restore 618

        var dynDeal = await _dealService.GetDealAsync(request.DealId);

        await _dealRequestService.RequestDealAsync(request.DealId, publisherAccountId, false,
                                                   request.HoursAllowedInProgress.Gz(dynDeal.HoursAllowedInProgress),
                                                   request.HoursAllowedRedeemed.Gz(dynDeal.HoursAllowedRedeemed));

        _deferRequestsService.DeferFifoRequest(new DealStatIncrement
                                               {
                                                   DealId = request.DealId,
                                                   StatType = DealStatType.TotalRequests,
                                                   FromPublisherAccountId = publisherAccountId
                                               });

        if (dynDeal.AutoApproveRequests)
        { // Auto approved requests now have to SYNCHRONOUSLY move from requested -> inProgress -> redeemed
            await _adminServiceGatewayFactory().SendAsync(new UpdateDealRequest
                                                          {
                                                              DealId = dynDeal.DealId,
                                                              Reason = "Auto approved",
                                                              UpdatedByPublisherAccountId = dynDeal.PublisherAccountId,
                                                              Model = new DealRequest
                                                                      {
                                                                          DealId = dynDeal.DealId,
                                                                          PublisherAccountId = publisherAccountId,
                                                                          Status = DealRequestStatus.InProgress
                                                                      },
                                                              WorkspaceId = dynDeal.WorkspaceId,
                                                              RequestPublisherAccountId = dynDeal.PublisherAccountId
                                                          });
        }

        // NOTE: keep this after the syncronous auto approval above, as
        _deferRequestsService.DeferPrimaryDealRequest(new DealRequested
                                                      {
                                                          DealId = request.DealId,
                                                          RequestedByPublisherAccountId = publisherAccountId
                                                      });
    }

    public async Task Put(PutDealRequest request)
    {
        var updateRequest = request.ConvertTo<UpdateDealRequest>();

        updateRequest.UpdatedByPublisherAccountId = request.RequestPublisherAccountId;

        await _adminServiceGatewayFactory().SendAsync(updateRequest);

        if (!request.CompletionMediaIds.IsNullOrEmpty() || !request.CompletionRydrMediaIds.IsNullOrEmpty())
        {
            var publisherAccountId = request.Model.PublisherAccountId.Gz(request.RequestPublisherAccountId);

            _deferRequestsService.DeferDealRequest(new DealRequestCompletionMediaSubmitted
                                                   {
                                                       DealId = request.DealId,
                                                       PublisherAccountId = publisherAccountId,
                                                       CompletionMediaPublisherMediaIds = request.CompletionMediaIds,
                                                       CompletionRydrMediaIds = request.CompletionRydrMediaIds
                                                   });
        }
    }

    public void Put(PutDealRequestCompletionMedia request)
        => _deferRequestsService.DeferDealRequest(new DealRequestCompletionMediaSubmitted
                                                  {
                                                      DealId = request.DealId,
                                                      PublisherAccountId = request.RequestPublisherAccountId,
                                                      CompletionMediaPublisherMediaIds = request.CompletionMediaIds,
                                                      CompletionRydrMediaIds = request.CompletionRydrMediaIds
                                                  });

    public Task Delete(DeleteDealRequest request)
        => _adminServiceGatewayFactory().SendAsync(new DeleteDealRequestInternal
                                                   {
                                                       DealId = request.DealId,
                                                       PublisherAccountId = request.ToPublisherAccountId(),
                                                       Reason = request.Reason
                                                   }.PopulateWithRequestInfo(request));

    private Task<RydrQueryResponse<DealResponse>> QueryRequestedDealsByPublisherIdAsync(QueryRequestedDealsByPublisherId request)
    {
        var publisherAccountId = request.EdgeId.ToLong(0).Gz(request.RequestPublisherAccountId);

        request.EdgeId = publisherAccountId.ToEdgeId();

        return AutoQueryIndexIntoAsync<
            QueryRequestedDealsByPublisherId,
            DynItemEdgeIdGlobalIndex,
            DealResponse>(request,
                          q => _autoQueryService.QueryDataAsync<QueryRequestedDealsByPublisherId, DynItemEdgeIdGlobalIndex>(q, Request),
                          async (q, r) =>
                          {
                              // NOTE: Have to order the batch of items as the GetItems<> call will not guarantee sort...opting for this
                              // at the moment over making multiple GetItem<> calls to keep order...
                              var dynDealRequests = (await _dealRequestService.GetDealRequestsAsync(q.Results.Select(i => i.Id), publisherAccountId)).Where(dr => dr.DealWorkspaceId == request.WorkspaceId ||
                                                                                                                                                                  dr.DealContextWorkspaceId == request.WorkspaceId ||
                                                                                                                                                                  dr.PublisherAccountId == publisherAccountId)
                                                                                                                                                     .OrderByDescending(ddr => ddr.ReferenceId.ToLong(0))
                                                                                                                                                     .AsList();

                              await foreach (var dealResponse in _dealRequestService.GetDealResponseRequestExtendedAsync(dynDealRequests))
                              {
                                  r.Add(dealResponse);
                              }
                          });
    }

    private Task<RydrQueryResponse<DealResponse>> QueryRequestsByDealOwnerAsync(QueryDealRequests request)
        => AutoQueryIndexIntoAsync<
            QueryDealRequests,
            DynItemTypeOwnerSpaceReferenceGlobalIndex,
            DealResponse>(request,
                          q => _autoQueryService.QueryDataAsync<QueryDealRequests, DynItemTypeOwnerSpaceReferenceGlobalIndex>(q, Request),
                          async (q, r) =>
                          {
                              // NOTE: Have to order the batch of items as the GetItems<> call will not guarantee sort...opting for this
                              // at the moment over making multiple GetItem<> calls to keep order...
                              var dealRequests = await _dynamoDb.QueryItemsAsync<DynDealRequest>(q.Results.Select(qr => qr.GetDynamoId()))
                                                                .Where(dr => dr.DealWorkspaceId == request.WorkspaceId ||
                                                                             dr.DealContextWorkspaceId == request.WorkspaceId ||
                                                                             dr.PublisherAccountId == request.DealRequestPublisherAccountId)
                                                                .OrderByDescending(ddr => ddr.ReferenceId.ToLong(0))
                                                                .ToList(q.Results.Count);

                              await foreach (var dealResponse in _dealRequestService.GetDealResponseRequestExtendedAsync(dealRequests))
                              {
                                  r.Add(dealResponse);
                              }
                          });

    private Task<RydrQueryResponse<DealResponse>> QueryRequestsByDealIdAsync(QueryRequestedDeals request)
        => AutoQueryIndexIntoAsync<
            QueryRequestedDeals,
            DynItemIdTypeReferenceGlobalIndex,
            DealResponse>(request,
                          q => _autoQueryService.QueryDataAsync<QueryRequestedDeals, DynItemIdTypeReferenceGlobalIndex>(q, Request),
                          async (q, r) =>
                          {
                              // NOTE: Have to order the batch of items as the GetItems<> call will not guarantee sort...opting for this
                              // at the moment over making multiple GetItem<> calls to keep order...
                              var dealRequests = await _dynamoDb.QueryItemsAsync<DynDealRequest>(q.Results.Select(qr => qr.GetDynamoId()))
                                                                .OrderByDescending(ddr => ddr.ReferenceId.ToLong(0))
                                                                .ToList(q.Results.Count);

                              await foreach (var dealResponse in _dealRequestService.GetDealResponseRequestExtendedAsync(dealRequests))
                              {
                                  r.Add(dealResponse);
                              }
                          });

    private async Task<RydrQueryResponse<TResponse>> AutoQueryIndexIntoAsync<TRequest, TRequestModel, TResponse>(TRequest request,
                                                                                                                 Func<TRequest, Task<QueryResponse<TRequestModel>>> query,
                                                                                                                 Func<QueryResponse<TRequestModel>, List<TResponse>, Task> processResults)
        where TRequestModel : class, ICanBeRecordLookup
        where TRequest : BaseQueryDataRequest<TRequestModel>
        where TResponse : class
    {
        var response = new RydrQueryResponse<TResponse>
                       {
                           Offset = request.Skip.Value
                       };

        var totalRecordsRetrieved = 0;

        var results = new List<TResponse>(request.Take.Value);

        do
        {
            var indexResponse = await query(request);

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

            await processResults(indexResponse, results);

            if (indexResponse.Results.Count < request.Take.Value)
            {
                break;
            }

            request.Skip += request.Take;
        } while (results.Count < request.Take.Value && totalRecordsRetrieved <= 15000);

        response.Results = results;

        if (response.Results.IsNullOrEmptyReadOnly())
        {
            response.Total = 0;
        }
        else
        {
            if (response.Total < response.Results.Count ||
                (request.Skip.Value <= 0 && response.Results.Count < request.Take.Value))
            {
                response.Total = response.Results.Count;
            }

            await _decorateResponsesService.DecorateAsync(request, response);
        }

        return response;
    }
}
