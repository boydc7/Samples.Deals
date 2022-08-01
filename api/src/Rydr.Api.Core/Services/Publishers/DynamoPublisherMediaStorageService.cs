using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

#pragma warning disable 1998

namespace Rydr.Api.Core.Services.Publishers
{
    public class DynamoPublisherMediaStorageService : IPublisherMediaStorageService, IPublisherMediaSingleStorageService
    {
        private readonly IPocoDynamo _dynamoDb;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly IRydrDataService _rydrDataService;
        private readonly List<IPublisherMediaStatDecorator> _decorators;

        public DynamoPublisherMediaStorageService(IPocoDynamo dynamoDb,
                                                  IEnumerable<IPublisherMediaStatDecorator> decorators,
                                                  IServiceCacheInvalidator serviceCacheInvalidator,
                                                  IRydrDataService rydrDataService)
        {
            _dynamoDb = dynamoDb;
            _serviceCacheInvalidator = serviceCacheInvalidator;
            _rydrDataService = rydrDataService;
            _decorators = (decorators ?? Enumerable.Empty<IPublisherMediaStatDecorator>()).AsList();
        }

        public async Task StoreAsync(DynPublisherMediaStat stat)
        {
            async IAsyncEnumerable<DynPublisherMediaStat> statWrapper()
            {
                yield return stat;
            }

            var results = statWrapper();

            foreach (var decorator in _decorators)
            {
                results = decorator.DecorateAsync(results);
            }

            var decoratedStat = await results.SingleAsync();

            if (decoratedStat.ExpiresAt.HasValue && decoratedStat.ExpiresAt > 0 && decoratedStat.ExpiresAt.Value <= DateTimeHelper.UtcNow.Date.ToUnixTimestamp())
            { // Expired...don't store it...
                return;
            }

            await _dynamoDb.PutItemDeferAsync(decoratedStat, RecordType.PublisherMediaStat);

            if (!decoratedStat.IsCompletionMediaStat)
            {
                return;
            }

            var dealPublisherAccountIds = await GetCompletionAffectedPublisherAccountIdsAsync(new[]
                                                                                              {
                                                                                                  decoratedStat.PublisherMediaId
                                                                                              });

            if (!dealPublisherAccountIds.IsNullOrEmpty())
            {
                await InvalidateCacheForAsync(dealPublisherAccountIds);
            }
        }

        public async Task StoreAsync(IEnumerable<DynPublisherMediaStat> stats)
        {
            var todayUtc = DateTimeHelper.UtcNow.Date.ToUnixTimestamp();
            var publisherAccountIdsAffected = new HashSet<long>();
            var completionMediaIds = new HashSet<long>();

            async IAsyncEnumerable<DynPublisherMediaStat> statsWrapper()
            {
                foreach (var stat in stats)
                {
                    if (stat.IsCompletionMediaStat)
                    {
                        completionMediaIds.Add(stat.PublisherMediaId);
                    }

                    publisherAccountIdsAffected.Add(stat.PublisherAccountId);

                    yield return stat;
                }
            }

            var results = statsWrapper();

            foreach (var decorator in _decorators)
            {
                results = decorator.DecorateAsync(results);
            }

            var batchSize = 100.ToDynamoBatchCeilingTake();
            var putItems = new List<DynPublisherMediaStat>(batchSize * 2);

            // Reads are cheap...writes expensive...in heavy-write loops like this, only write if needed (i.e. if the stat has changed)
            await foreach (var resultBatch in results.Where(r => r.ExpiresAt.HasValue &&
                                                                 r.ExpiresAt.Value >= todayUtc)
                                                     .ToBatchesOfAsync(batchSize, true))
            {
                var existingStatMap = await _dynamoDb.GetItemsAsync<DynPublisherMediaStat>(resultBatch.Select(r => r.ToDynamoId()))
                                                     .ToDictionarySafe(s => s.ToDynamoItemIdEdgeCompositeStringId(),
                                                                       StringComparer.OrdinalIgnoreCase);

                if (existingStatMap.IsNullOrEmptyRydr())
                { // None exist, put the whole batch and move along
                    await _dynamoDb.PutItemsDeferAsync(resultBatch, RecordType.PublisherMediaStat);

                    continue;
                }

                // Find any stats that haven't changed at all and remove them from the batch so they don't put
                foreach (var resultStat in resultBatch)
                {
                    var dynItemIdEdgeKey = resultStat.ToDynamoItemIdEdgeCompositeStringId();

                    if (!existingStatMap.ContainsKey(dynItemIdEdgeKey))
                    { // Doesn't exist yet, put...
                        putItems.Add(resultStat);

                        continue;
                    }

                    var existing = existingStatMap[dynItemIdEdgeKey];

                    // If any of the basic data doesn't match, put...
                    if (resultStat.ContentType != existing.ContentType ||
                        resultStat.PublisherAccountId != existing.PublisherAccountId ||
                        Math.Abs(resultStat.EngagementRating - existing.EngagementRating) >= 0.0001 ||
                        Math.Abs(resultStat.TrueEngagementRating - existing.TrueEngagementRating) >= 0.0001)
                    {
                        putItems.Add(resultStat);

                        continue;
                    }

                    if (resultStat.Stats.IsNullOrEmpty())
                    { // No new stats, nothing to put, move along
                        continue;
                    }

                    if (existing.Stats.IsNullOrEmpty() || existing.Stats.Count < resultStat.Stats.Count)
                    { // Only new stats, or have more new stats than existing, put
                        putItems.Add(resultStat);

                        continue;
                    }

                    // Have to check each individual one...
                    foreach (var newStatValue in resultStat.Stats)
                    {
                        // If no existing match, or existing value isn't the same, put and done
                        var existingStatValue = existing.Stats.FirstOrDefault(x => x.Name.EqualsOrdinalCi(newStatValue.Name));

                        if (existingStatValue == null || existingStatValue.Value != newStatValue.Value)
                        {
                            putItems.Add(resultStat);

                            break;
                        }
                    }
                }

                if (putItems.Count >= batchSize)
                {
                    await _dynamoDb.PutItemsDeferAsync(putItems, RecordType.PublisherMediaStat);

                    putItems.Clear();
                }
            }

            if (putItems.Count > 0)
            {
                await _dynamoDb.PutItemsDeferAsync(putItems, RecordType.PublisherMediaStat);
            }

            var dealPublisherAccountIds = await GetCompletionAffectedPublisherAccountIdsAsync(completionMediaIds);

            if (!dealPublisherAccountIds.IsNullOrEmpty())
            {
                publisherAccountIdsAffected.UnionWith(dealPublisherAccountIds);
            }

            await InvalidateCacheForAsync(publisherAccountIdsAffected);
        }

        private async Task InvalidateCacheForAsync(IEnumerable<long> publisherAccountIdsAffected)
        {
            if (publisherAccountIdsAffected == null)
            {
                return;
            }

            foreach (var publisherAccountId in publisherAccountIdsAffected)
            {
                await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(publisherAccountId, "deals", "query", "dealmetrics", "publisheracct");
            }
        }

        private async Task<HashSet<long>> GetCompletionAffectedPublisherAccountIdsAsync(ICollection<long> completionMediaIds)
        {
            // For completion medias, have to flush caches for owners of deals that have the given media as completion data associated
            if (completionMediaIds == null || completionMediaIds.Count <= 0)
            {
                return null;
            }

            var dealPublisherAccountIds = await _rydrDataService.QueryAdHocAsync(db => db.ColumnDistinctAsync<long>(@"
SELECT	DISTINCT d.PublisherAccountId
FROM	Deals d
WHERE	EXISTS
		(
        SELECT	NULL
        FROM	DealRequestMedia drm
        WHERE	drm.DealId = d.Id
				AND drm.MediaId IN(@CompletionMediaIds)
        );
",
                                                                                                                    new
                                                                                                                    {
                                                                                                                        CompletionMediaIds = completionMediaIds
                                                                                                                    }));

            return dealPublisherAccountIds;
        }
    }
}
