using System;
using System.Collections.Generic;
using System.Linq;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Enums;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Publishers
{
    public class PublisherAccountEngagementCalcMediaStatDecorator : IPublisherMediaStatDecorator
    {
        private readonly IPocoDynamo _dynamoDb;
        private readonly IPublisherAccountService _publisherAccountService;

        public PublisherAccountEngagementCalcMediaStatDecorator(IPocoDynamo dynamoDb, IPublisherAccountService publisherAccountService)
        {
            _dynamoDb = dynamoDb;
            _publisherAccountService = publisherAccountService;
        }

        public async IAsyncEnumerable<DynPublisherMediaStat> DecorateAsync(IAsyncEnumerable<DynPublisherMediaStat> stats)
        {
            var publishersToSyncStoriesFor = new Dictionary<long, List<DynPublisherMediaStat>>();
            var publisherAggMap = new Dictionary<long, PublisherMediaStatAggregateContainer>();

            void aggValuesFromStat(DynPublisherMediaStat stat)
            {
                var ratingStats = stat.GetRatingStats();

                if (ratingStats.Engagements <= 0 && ratingStats.Impressions <= 0 && ratingStats.Views <= 0)
                {
                    return;
                }

                if (!publisherAggMap.ContainsKey(stat.PublisherAccountId))
                {
                    publisherAggMap[stat.PublisherAccountId] = new PublisherMediaStatAggregateContainer();
                }

                var aggregateObject = stat.ContentType == PublisherContentType.Story
                                          ? publisherAggMap[stat.PublisherAccountId].Story
                                          : publisherAggMap[stat.PublisherAccountId].Standard;

                aggregateObject.Count++;
                aggregateObject.EngagementRatingSum += stat.EngagementRating;
                aggregateObject.TrueEngagementRatingSum += stat.TrueEngagementRating;
                aggregateObject.Engagements += ratingStats.Engagements;
                aggregateObject.Impressions += ratingStats.Impressions;
                aggregateObject.Saves += ratingStats.Saves;
                aggregateObject.Views += ratingStats.Views;
                aggregateObject.Reach += ratingStats.Reach;

                if (stat.EngagementRating > 0)
                {
                    aggregateObject.CountWithEngagementRating++;
                }

                if (stat.TrueEngagementRating > 0)
                {
                    aggregateObject.CountWithTrueEngagementRating++;
                }
            }

            await foreach (var stat in stats)
            {
                if (!stat.Period.EqualsOrdinalCi(FbIgInsights.LifetimePeriod))
                {
                    yield return stat;

                    continue;
                }

                switch (stat.ContentType)
                {
                    case PublisherContentType.Post:
                        aggValuesFromStat(stat);

                        break;

                    case PublisherContentType.Story:
                        // Stories only last 24 hours, so we cannot use the stream of stories here to calc the "recent" story
                        // ratings, as there will usually only be 1 or 2 at the most. So we just flag that this account has some
                        // and recalc them for these accounts after...
                        if (publishersToSyncStoriesFor.ContainsKey(stat.PublisherAccountId))
                        {
                            publishersToSyncStoriesFor[stat.PublisherAccountId].Add(stat);
                        }
                        else
                        {
                            publishersToSyncStoriesFor.Add(stat.PublisherAccountId, new List<DynPublisherMediaStat>
                                                                                    {
                                                                                        stat
                                                                                    });
                        }

                        break;

                    case PublisherContentType.Unknown:
                        break;

                    default:
                        // ReSharper disable once NotResolvedInText
                        throw new ArgumentOutOfRangeException("stat.PublisherContentType", "Invalid or Unhandled PublisherContentType");
                }

                yield return stat;
            }

            // Need to recalc story rating? If so get the last x stories for this account and do so
            if (publishersToSyncStoriesFor.Count > 0)
            {
                foreach (var publisherAccountIdToSyncPair in publishersToSyncStoriesFor)
                {
                    var storyStats = await _dynamoDb.GetItemsFromAsync<DynPublisherMediaStat, DynPublisherMedia>(_dynamoDb.FromQuery<DynPublisherMedia>(m => m.Id == publisherAccountIdToSyncPair.Key &&
                                                                                                                                                             Dynamo.BeginsWith(m.EdgeId, "00"))
                                                                                                                          .Filter(pm => pm.DeletedOnUtc == null &&
                                                                                                                                        pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                                                                                        pm.ContentType == PublisherContentType.Story &&
                                                                                                                                        pm.PublisherType == PublisherType.Facebook)
                                                                                                                          .ExecAsync(),
                                                                                                                 m => new DynamoId(m.PublisherMediaId,
                                                                                                                                   DynPublisherMediaStat.BuildEdgeId(FbIgInsights.LifetimePeriod, FbIgInsights.LifetimeEndTime)))
                                                    .Take(500)
                                                    .ToHashSet()
                                     ?? new HashSet<DynPublisherMediaStat>();

                    storyStats.UnionWith(publisherAccountIdToSyncPair.Value);

                    foreach (var storyStat in storyStats)
                    {
                        aggValuesFromStat(storyStat);
                    }
                }
            }

            // Now take all of the stats data we've captured and store it inside the publisheraccount metric data
            var now = DateTimeHelper.UtcNowTs;

            foreach (var publisherStatAgg in publisherAggMap)
            {
                var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherStatAgg.Key);

                if (publisherAccount == null || publisherAccount.IsDeleted())
                {
                    continue;
                }

                await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount,
                                                                           pa =>
                                                                           {
                                                                               if (pa.Metrics == null)
                                                                               {
                                                                                   pa.Metrics = new Dictionary<string, double>();
                                                                               }

                                                                               pa.Metrics[PublisherMetricName.RecentEngagementRating] = publisherStatAgg.Value.Standard.CountWithEngagementRating <= 0
                                                                                                                                            ? 0
                                                                                                                                            : Math.Round(publisherStatAgg.Value.Standard.EngagementRatingSum / publisherStatAgg.Value.Standard.CountWithEngagementRating, 4);

                                                                               pa.Metrics[PublisherMetricName.RecentTrueEngagementRating] = publisherStatAgg.Value.Standard.CountWithTrueEngagementRating <= 0
                                                                                                                                                ? 0
                                                                                                                                                : Math.Round(publisherStatAgg.Value.Standard.TrueEngagementRatingSum / publisherStatAgg.Value.Standard.CountWithTrueEngagementRating, 4);

                                                                               pa.Metrics[PublisherMetricName.RecentMediaCount] = publisherStatAgg.Value.Standard.Count;
                                                                               pa.Metrics[PublisherMetricName.RecentMediaActions] = publisherStatAgg.Value.Standard.Engagements;
                                                                               pa.Metrics[PublisherMetricName.RecentMediaImpressions] = publisherStatAgg.Value.Standard.Impressions;
                                                                               pa.Metrics[PublisherMetricName.RecentMediaReach] = publisherStatAgg.Value.Standard.Reach;
                                                                               pa.Metrics[PublisherMetricName.RecentMediaSaves] = publisherStatAgg.Value.Standard.Saves;
                                                                               pa.Metrics[PublisherMetricName.RecentMediaViews] = publisherStatAgg.Value.Standard.Views;

                                                                               if (publishersToSyncStoriesFor.ContainsKey(publisherStatAgg.Key))
                                                                               {
                                                                                   pa.Metrics[PublisherMetricName.StoryEngagementRating] = publisherStatAgg.Value.Story.CountWithEngagementRating <= 0
                                                                                                                                               ? 0
                                                                                                                                               : Math.Round(publisherStatAgg.Value.Story.EngagementRatingSum / publisherStatAgg.Value.Story.CountWithEngagementRating, 4);

                                                                                   pa.Metrics[PublisherMetricName.RecentStoryCount] = publisherStatAgg.Value.Story.Count;
                                                                                   pa.Metrics[PublisherMetricName.RecentStoryActions] = publisherStatAgg.Value.Story.Engagements;
                                                                                   pa.Metrics[PublisherMetricName.RecentStoryImpressions] = publisherStatAgg.Value.Story.Impressions;
                                                                                   pa.Metrics[PublisherMetricName.RecentStoryReach] = publisherStatAgg.Value.Story.Reach;
                                                                               }

                                                                               pa.LastEngagementMetricsUpdatedOn = now;
                                                                           });
            }
        }

        private class PublisherMediaStatAggregateContainer
        {
            public PublisherMediaStatAggregate Standard { get; } = new PublisherMediaStatAggregate();
            public PublisherMediaStatAggregate Story { get; } = new PublisherMediaStatAggregate();
        }

        private class PublisherMediaStatAggregate
        {
            public int Count { get; set; }
            public long Engagements { get; set; }
            public long Impressions { get; set; }
            public long Saves { get; set; }
            public long Views { get; set; }
            public long Reach { get; set; }
            public double EngagementRatingSum { get; set; }
            public int CountWithEngagementRating { get; set; }
            public double TrueEngagementRatingSum { get; set; }
            public int CountWithTrueEngagementRating { get; set; }
        }
    }
}
