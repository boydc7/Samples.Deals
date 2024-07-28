using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

public class PublisherAccountStatsService : BaseAuthenticatedApiService
{
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IPublisherAccountStatsService _publisherAccountStatsService;

    public PublisherAccountStatsService(IPublisherAccountService publisherAccountService,
                                        IPublisherAccountStatsService publisherAccountStatsService)
    {
        _publisherAccountService = publisherAccountService;
        _publisherAccountStatsService = publisherAccountStatsService;
    }

    public async Task<OnlyResultResponse<PublisherAccountStats>> Get(GetPublisherAccountStats request)
    {
        var publisherAccountId = request.GetPublisherIdFromIdentifier();

        if (publisherAccountId > 0)
        { // Single publisher profile
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);
            var myContextWorkspaceId = publisherAccount.GetContextWorkspaceId(request.WorkspaceId);

            var dealStats = await _publisherAccountStatsService.GetPublisherAccountStats(publisherAccount.PublisherAccountId, myContextWorkspaceId);

            var publishedDeals = publisherAccount.IsBusiness()
                                     ? await _publisherAccountStatsService.GetPublishedPausedDealCountAsync(publisherAccount.PublisherAccountId, myContextWorkspaceId)
                                     : 0;

            var response = new PublisherAccountStats
                           {
                               DealRequestStats = dealStats ?? new List<DealStat>()
                           };

            response.DealRequestStats.Add(new DealStat
                                          {
                                              Type = DealStatType.PublishedDeals,
                                              Value = publishedDeals.ToStringInvariant()
                                          });

            return response.AsOnlyResultResponse();
        }
        else
        { // Entire workspace stats - get all stats for all publishers in the workspace, aggregate them by type
            var statAggregate = new Dictionary<DealStatType, long>();

            await foreach (var stat in _dynamoDb.GetItemsFromAsync<DynPublisherAccountStat,
                               DynItemTypeOwnerSpaceReferenceGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.PublisherAccountStat,
                                                                                                                                                                                                       request.WorkspaceId))
                                                                                   .QueryAsync(_dynamoDb),
                                                                          i => i.GetDynamoId()))
            {
                var statValue = stat.Value.ToLong(0).Gz(stat.Cnt.Gz(0));

                if (!statAggregate.TryAdd(stat.StatType, statValue))
                {
                    var existingValue = statAggregate[stat.StatType];
                    statAggregate[stat.StatType] = existingValue + statValue;
                }
            }

            var response = new PublisherAccountStats
                           {
                               DealRequestStats = statAggregate.Select(t => new DealStat
                                                                            {
                                                                                Type = t.Key,
                                                                                Value = t.Value.ToStringInvariant()
                                                                            })
                                                               .AsList()
                           };

            var publisherDealIndexIds = await _dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.Deal,
                                                                                                                                                                           request.WorkspaceId) &&
                                                                                                                       Dynamo.Between(i.ReferenceId, "1500000000", "3000000000"))
                                                       .Filter(i => i.DeletedOnUtc == null &&
                                                                    Dynamo.In(i.StatusId, DealEnumHelpers.PublishedPausedDealStatuses))
                                                       .Select(i => new
                                                                    {
                                                                        i.Id
                                                                    })
                                                       .QueryAsync(_dynamoDb)
                                                       .Take(500)
                                                       .ToList();

            response.DealRequestStats.Add(new DealStat
                                          {
                                              Type = DealStatType.PublishedDeals,
                                              Value = publisherDealIndexIds?.Count.ToStringInvariant() ?? "0"
                                          });

            return response.AsOnlyResultResponse();
        }
    }

    public async Task<OnlyResultResponse<PublisherAccountStatsWith>> Get(GetPublisherAccountStatsWith request)
    {
        var response = new PublisherAccountStatsWith();

        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.GetPublisherIdFromIdentifier());
        var myContextWorkspaceId = publisherAccount.GetContextWorkspaceId(request.WorkspaceId);
        var dealtWithPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.DealtWithPublisherAccountId);

        // Depending on which type of account we're looking for dealt with stats on, get requests appropriately
        // Get all the deal requests from deals owned by the publisherAccountId above that were with the influencer requested that are completed and have completion media
        // OR
        // Get deal requests from deals owned by the dealWith publisher account that were completed by the influencer from above
        var dealOwnerPublisherAccountId = dealtWithPublisherAccount.RydrAccountType.IsInfluencer()
                                              ? publisherAccount.PublisherAccountId
                                              : dealtWithPublisherAccount.PublisherAccountId;

        var dealRequesterPublisherAccountId = dealtWithPublisherAccount.RydrAccountType.IsInfluencer()
                                                  ? dealtWithPublisherAccount.PublisherAccountId
                                                  : publisherAccount.PublisherAccountId;

        var completedDealIds = await _dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == DynItem.BuildTypeOwnerSpaceHash(DynItemType.DealRequest, dealOwnerPublisherAccountId))
                                              .Filter(i => i.DeletedOnUtc == null &&
                                                           i.TypeId == (int)DynItemType.DealRequest &&
                                                           i.StatusId == DealRequestStatus.Completed.ToString() &&
                                                           i.EdgeId == dealRequesterPublisherAccountId.ToEdgeId())
                                              .Select(i => new
                                                           {
                                                               i.Id,
                                                               i.EdgeId,
                                                               i.WorkspaceId
                                                           })
                                              .QueryAsync(_dynamoDb)
                                              .Where(r => r.GetContextWorkspaceId() == myContextWorkspaceId) // Deal is from a personal workspace or the same context the user is in
                                              .Select(r => r.GetDynamoId())
                                              .Take(300)
                                              .ToList(50);

        var completionMediaIds = completedDealIds.IsNullOrEmpty()
                                     ? null
                                     : await _dynamoDb.QueryItemsAsync<DynDealRequest>(completedDealIds)
                                                      .Where(dr => !dr.CompletionMediaIds.IsNullOrEmpty() &&
                                                                   dr.PublisherAccountId == dealRequesterPublisherAccountId &&
                                                                   dr.DealPublisherAccountId == dealOwnerPublisherAccountId)
                                                      .SelectManyDistinctAsync(dr => dr.CompletionMediaIds);

        var completionMedias = 0;

        var mediaStats = completionMediaIds.IsNullOrEmpty()
                             ? null
                             : await _dynamoDb.QueryItemsAsync<DynPublisherMediaStat>(completionMediaIds.Select(cmi => new DynamoId(cmi, DynPublisherMediaStat.BuildEdgeId(FbIgInsights.LifetimePeriod, FbIgInsights.LifetimeEndTime))))
                                              .Where(dms => dms != null &&
                                                            !dms.IsDeleted() &&
                                                            !dms.Stats.IsNullOrEmpty())
                                              .ToList(completionMediaIds.Count);

        response.Stats = mediaStats.IsNullOrEmpty()
                             ? null
                             : mediaStats.SelectMany(dms =>
                                                     {
                                                         completionMedias++;

                                                         return dms.Stats;
                                                     })
                                         .Aggregate(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
                                                    (m, s) =>
                                                    {
                                                        if (m.ContainsKey(s.Name))
                                                        {
                                                            m[s.Name] += s.Value;
                                                        }
                                                        else
                                                        {
                                                            m[s.Name] = s.Value;
                                                        }

                                                        return m;
                                                    })
                                         .Select(m => new PublisherStatValue
                                                      {
                                                          Name = m.Key,
                                                          Value = m.Value
                                                      })
                                         .AsList();

        response.CompletionMediaCount = completionMedias;
        response.CompletedDealCount = completedDealIds?.Count ?? 0;

        return response.AsOnlyResultResponse();
    }
}
