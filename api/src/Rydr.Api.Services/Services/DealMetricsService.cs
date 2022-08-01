using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.FbSdk.Enums;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.Api.Services.Services
{
    public class DealMetricsService : BaseAuthenticatedApiService
    {
        private readonly IDealMetricService _dealMetricService;
        private readonly IRydrDataService _rydrDataService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IDealService _dealService;

        public DealMetricsService(IDealMetricService dealMetricService,
                                  IRydrDataService rydrDataService,
                                  IPublisherAccountService publisherAccountService,
                                  IDealService dealService)
        {
            _dealMetricService = dealMetricService;
            _rydrDataService = rydrDataService;
            _publisherAccountService = publisherAccountService;
            _dealService = dealService;
        }

        [RydrForcedSimpleCacheResponse(600)]
        public async Task<OnlyResultResponse<DelinquentDealRequestResult>> Get(GetDelinquentDealRequests request)
        {
            var data = await _rydrDataService.QueryAdHocAsync(db => db.SelectAsync<DelinquentDealRequestResult>(@"
SELECT  COUNT(*) AS DelinquentCount
FROM    DealRequests dr
WHERE   dr.PublisherAccountId = @PublisherAccountId
        AND dr.DelinquentOn IS NOT NULL
        AND dr.DelinquentOn >= @RydrNowUtc;
",
                                                                                                                new
                                                                                                                {
                                                                                                                    PublisherAccountId = request.GetPublisherIdFromIdentifier(),
                                                                                                                    RydrNowUtc = _dateTimeProvider.UtcNow.ToSqlString()
                                                                                                                }));

            var result = data?.FirstOrDefault();

            return result.AsOnlyResultResponse();
        }

        [RydrForcedSimpleCacheResponse(200)]
        public async Task<OnlyResultResponse<DealCompletionMediaMetrics>> Get(GetDealCompletionMediaMetrics request)
            => (await DoGetDealCompletionMediaMetrics(request.DealId,
                                                      request.PublisherAccountId.Gz(request.RequestPublisherAccountId),
                                                      request.WorkspaceId,
                                                      request.CompletedOnStart,
                                                      request.CompletedOnEnd)).AsOnlyResultResponse();

        public async Task Post(PostDealMetric request)
        {
            var dynDeal = await _dealService.GetDealAsync(request.DealId, true);

            if (dynDeal == null || dynDeal.IsDeleted())
            {
                return;
            }

            var otherPublisherAccountId = request.PublisherAccountId.Gz(request.RequestPublisherAccountId);

            _dealMetricService.Measure(request.MetricType, dynDeal, otherPublisherAccountId, request.WorkspaceId, request.UserId);
        }

        private async Task<DealCompletionMediaMetrics> DoGetDealCompletionMediaMetrics(long dealId, long publisherAccountId, long workspaceId,
                                                                                       DateTime? completedStartDate = null, DateTime? completedEndDate = null)
        {
            var lifetimeEnumId = _rydrDataService.GetOrCreateRydrEnumId("lifetime");

            if (lifetimeEnumId <= 0)
            {
                return new DealCompletionMediaMetrics
                       {
                           PublisherAccountId = publisherAccountId
                       };
            }

            var hasCompletedDates = (completedStartDate.HasValue && completedStartDate.Value > DateTimeHelper.MinApplicationDate) ||
                                    (completedEndDate.HasValue && completedEndDate.Value > DateTimeHelper.MinApplicationDate);

            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

            var contextWorkspaceId = publisherAccount.GetContextWorkspaceId(workspaceId);

            // NOTE: Doing the various casts and magic number addition to avoid a Dapper/ConnectionProvider issue with some db providers (i.e. sqlite, mysql) where
            //       the column meta-data is different per row if a double doesn't have a decimal portion to it, i.e. treated as an int)...annoying...
            var dataResult = await _rydrDataService.QueryMultipleAsync(string.Concat(@"
-- Metrics
SELECT	drm.ContentType,
		COUNT(DISTINCT drm.MediaId) AS MediaCount,
		SUM(CASE WHEN drm.MediaType = 'IMAGE' AND COALESCE(se.Name,'impressions') = 'impressions' THEN 1 END) AS Images,
		SUM(CASE WHEN drm.MediaType = 'VIDEO' AND COALESCE(se.Name,'impressions') = 'impressions' THEN 1 END) AS Videos,
		SUM(CASE WHEN drm.MediaType = 'CAROUSEL_ALBUM' AND COALESCE(se.Name,'impressions') = 'impressions' THEN 1 END) AS Carousels,
		SUM(CASE WHEN se.Name = 'impressions' THEN ms.Value ELSE 0 END) AS Impressions,
		SUM(CASE WHEN se.Name = 'reach' THEN ms.Value ELSE 0 END) AS Reach,
		SUM(CASE WHEN se.Name = 'replies' THEN ms.Value ELSE 0 END) AS Replies,
        SUM(CASE WHEN se.Name = 'saved' THEN ms.Value ELSE 0 END) AS Saves,
        SUM(CASE WHEN se.Name = 'video_views' THEN ms.Value ELSE 0 END) AS Views,
        SUM(CASE WHEN se.Name = 'comments' THEN ms.Value ELSE 0 END) AS Comments,
        SUM(CASE WHEN se.Name = 'actions' THEN ms.Value ELSE 0 END) AS Actions,
		SUM(CASE WHEN se.Name = 'engagement' THEN ms.Value ELSE 0 END) AS Engagements
FROM   	Deals d
JOIN	DealRequestMedia drm
ON		d.Id = drm.DealId
LEFT JOIN
        MediaStats ms
ON		drm.PublisherAccountId = ms.PublisherAccountId
		AND drm.MediaId = ms.MediaId
        AND ms.PeriodEnumId = @PeriodEnumId
        AND ms.EndTime = @LifetimeEndTime
LEFT JOIN
        Enums se
ON		ms.StatEnumId = se.Id
LEFT JOIN
        Enums sp
ON		ms.PeriodEnumId = sp.Id
WHERE	d.DealContextWorkspaceId = @ContextWorkspaceId
        AND d.PublisherAccountId = @PublisherAccountId",
                                                                                     dealId > 0
                                                                                         ? @"
        AND d.Id = @DealId
        AND drm.DealId = @DealId"
                                                                                         : string.Empty,
                                                                                     hasCompletedDates
                                                                                         ? string.Concat(@"
        AND EXISTS
        (
        SELECT  NULL
        FROM    DealRequests dr
        WHERE   dr.DealId = d.Id
                AND dr.PublisherAccountId = @PublisherAccountId
                AND dr.CompletedOn IS NOT NULL",
                                                                                                         completedStartDate.HasValue && completedStartDate.Value > DateTimeHelper.MinApplicationDate
                                                                                                             ? @"
                AND dr.CompletedOn >= @CompletedOnStart"
                                                                                                             : string.Empty,
                                                                                                         completedEndDate.HasValue && completedEndDate.Value > DateTimeHelper.MinApplicationDate
                                                                                                             ? @"
                AND dr.CompletedOn < @CompletedOnEnd"
                                                                                                             : string.Empty, @"
        )")
                                                                                         : string.Empty, @"
GROUP BY
        drm.ContentType;

-- Counts
SELECT	COUNT(DISTINCT d.Id) AS CompletedRequestDeals,
		COUNT(DISTINCT drm.DealId, drm.PublisherAccountId) AS CompletedRequests,
        SUM(CASE WHEN drm.ContentType = 1 THEN 1 END) AS CompletedPostMedias,
        SUM(CASE WHEN drm.ContentType = 2 THEN 1 END) AS CompletedStoryMedias
FROM   	Deals d
JOIN	DealRequestMedia drm
ON		d.Id = drm.DealId
WHERE	d.PublisherAccountId = @PublisherAccountId
        AND d.DealContextWorkspaceId = @ContextWorkspaceId",
                                                                                     dealId > 0
                                                                                         ? @"
        AND d.Id = @DealId
        AND drm.DealId = @DealId"
                                                                                         : string.Empty,
                                                                                     hasCompletedDates
                                                                                         ? string.Concat(@"
        AND EXISTS
        (
        SELECT  NULL
        FROM    DealRequests dr
        WHERE   dr.DealId = d.Id
                AND dr.PublisherAccountId = @PublisherAccountId
                AND dr.CompletedOn IS NOT NULL",
                                                                                                         completedStartDate.HasValue && completedStartDate.Value > DateTimeHelper.MinApplicationDate
                                                                                                             ? @"
                AND dr.CompletedOn >= @CompletedOnStart"
                                                                                                             : string.Empty,
                                                                                                         completedEndDate.HasValue && completedEndDate.Value > DateTimeHelper.MinApplicationDate
                                                                                                             ? @"
                AND dr.CompletedOn < @CompletedOnEnd"
                                                                                                             : string.Empty, @"
        )")
                                                                                         : string.Empty, @";

-- COSTS
SELECT	SUM(d.Value) AS CompletedRequestsCost
FROM	Deals d
JOIN    DealRequests dr
ON      d.Id = dr.DealId
WHERE	d.PublisherAccountId = @PublisherAccountId
        AND d.DealContextWorkspaceId = @ContextWorkspaceId
        AND EXISTS
		(
        SELECT	NULL
        FROM	DealRequestMedia drm
        WHERE	drm.DealId = dr.DealId
                AND drm.PublisherAccountId = dr.PublisherAccountId
        )",
                                                                                     dealId > 0
                                                                                         ? @"
        AND d.Id = @DealId"
                                                                                         : string.Empty,
                                                                                     hasCompletedDates
                                                                                         ? @"
        AND dr.CompletedOn IS NOT NULL"
                                                                                         : string.Empty,
                                                                                     completedStartDate.HasValue && completedStartDate.Value > DateTimeHelper.MinApplicationDate
                                                                                         ? @"
        AND dr.CompletedOn >= @CompletedOnStart"
                                                                                         : string.Empty,
                                                                                     completedEndDate.HasValue && completedEndDate.Value > DateTimeHelper.MinApplicationDate
                                                                                         ? @"
        AND dr.CompletedOn < @CompletedOnEnd"
                                                                                         : string.Empty, @";
"),
                                                                       new
                                                                       {
                                                                           DealId = dealId,
                                                                           PublisherAccountId = publisherAccountId,
                                                                           PeriodEnumId = lifetimeEnumId,
                                                                           LifetimeEndTime = FbIgInsights.LifetimeEndTime.ToDateTime(),
                                                                           ContextWorkspaceId = contextWorkspaceId,
                                                                           CompletedOnStart = completedStartDate.GetValueOrDefault(),
                                                                           CompletedOnEnd = completedEndDate.GetValueOrDefault()
                                                                       },
                                                                       data => new DataDealCompletionResults
                                                                               {
                                                                                   Metrics = data.ReadOrDefaults<DataDealCompletionMetric>().AsList(),
                                                                                   Counts = data.ReadOrDefault<DataDealCompletionCounts>(),
                                                                                   Costs = data.ReadOrDefault<DataDealCompletionCosts>()
                                                                               });

            if (dataResult == null)
            {
                return new DealCompletionMediaMetrics
                       {
                           PublisherAccountId = publisherAccountId
                       };
            }

            var postMetrics = dataResult.Metrics?.SingleOrDefault(m => m.ContentType == PublisherContentType.Post) ?? new DataDealCompletionMetric();
            var storyMetrics = dataResult.Metrics?.SingleOrDefault(m => m.ContentType == PublisherContentType.Story) ?? new DataDealCompletionMetric();

            var response = new DealCompletionMediaMetrics
                           {
                               PublisherAccountId = publisherAccountId,
                               PostImpressions = postMetrics.Impressions,
                               PostReach = postMetrics.Reach,
                               PostReachAvg = postMetrics.MediaCount <= 0
                                                  ? 0
                                                  : postMetrics.Reach / postMetrics.MediaCount,
                               PostActions = postMetrics.Actions,
                               PostReplies = postMetrics.Replies,
                               PostSaves = postMetrics.Saves,
                               PostViews = postMetrics.Views,
                               PostComments = postMetrics.Comments,
                               StoryImpressions = storyMetrics.Impressions,
                               StoryReach = storyMetrics.Reach,
                               StoryReachAvg = storyMetrics.MediaCount <= 0
                                                   ? 0
                                                   : storyMetrics.Reach / storyMetrics.MediaCount,
                               StoryActions = storyMetrics.Actions,
                               StoryReplies = storyMetrics.Replies,
                               StorySaves = storyMetrics.Saves,
                               StoryViews = storyMetrics.Views,
                               StoryComments = storyMetrics.Comments,
                               Posts = postMetrics.MediaCount,
                               Stories = storyMetrics.MediaCount,
                               Images = postMetrics.Images, // NOTE: Correctly NOT including storyMetrics.Videos here...we only want post videos to count as Videos
                               Videos = postMetrics.Videos, // NOTE: Correctly NOT including storyMetrics.Videos here...we only want post videos to count as Videos
                               Carousels = postMetrics.Carousels, // NOTE: Correctly NOT including storyMetrics.Carousels here...stories cannot have them currently, and even if they could or can someday, we do not want to include them without rethinking some things maybe
                               PostEngagements = postMetrics.Engagements,
                               StoryEngagements = storyMetrics.Impressions + storyMetrics.Replies,
                               TotalCompletionCost = dataResult.Costs?.CompletedRequestsCost ?? 0,
                               CompletedRequests = dataResult.Counts?.CompletedRequests ?? 0,
                               CompletedRequestDeals = dataResult.Counts?.CompletedRequestDeals ?? 0,
                               CompletedPostMedias = dataResult.Counts.CompletedPostMedias,
                               CompletedStoryMedias = dataResult.Counts.CompletedStoryMedias
                           };

            if ((postMetrics.Impressions > 0 || storyMetrics.Impressions > 0) && response.CompletedRequests > 0)
            {
                response.AvgCpmPerCompletion = Math.Round(((response.TotalCompletionCost / (postMetrics.Impressions + storyMetrics.Impressions)) * 1000.0) / response.CompletedRequests, 4);
            }

            if ((response.PostEngagements > 0 || response.StoryEngagements > 0) && response.CompletedRequests > 0)
            {
                response.AvgCpePerCompletion = Math.Round((response.TotalCompletionCost / (response.PostEngagements + response.StoryEngagements)) / response.CompletedRequests, 4);
            }

            if (response.CompletedRequestDeals > 0)
            {
                response.AvgCogPerCompletedDeal = Math.Round(response.TotalCompletionCost / response.CompletedRequestDeals, 4);
            }

            if (response.CompletedStoryMedias > 0 && storyMetrics.Impressions > 0)
            {
                response.AvgCpmPerStory = Math.Round(((response.TotalCompletionCost / storyMetrics.Impressions) * 1000.0) / response.CompletedStoryMedias, 4);
            }

            if (response.CompletedPostMedias > 0 && postMetrics.Impressions > 0)
            {
                response.AvgCpmPerPost = Math.Round(((response.TotalCompletionCost / postMetrics.Impressions) * 1000.0) / response.CompletedPostMedias, 4);
            }

            if (response.StoryEngagements > 0 && response.CompletedRequests > 0)
            {
                response.AvgCpePerStory = Math.Round((response.TotalCompletionCost / response.StoryEngagements) / response.CompletedStoryMedias, 4);
            }

            if (response.PostEngagements > 0 && response.CompletedRequests > 0)
            {
                response.AvgCpePerPost = Math.Round((response.TotalCompletionCost / response.PostEngagements) / response.CompletedPostMedias, 4);
            }

            return response;
        }

        private class DataDealCompletionResults
        {
            public List<DataDealCompletionMetric> Metrics { get; set; }
            public DataDealCompletionCounts Counts { get; set; }
            public DataDealCompletionCosts Costs { get; set; }
        }

        private class DataDealCompletionCosts
        {
            public double CompletedRequestsCost { get; set; }
        }

        private class DataDealCompletionCounts
        {
            public int CompletedRequestDeals { get; set; }
            public int CompletedRequests { get; set; }
            public int CompletedPostMedias { get; set; }
            public int CompletedStoryMedias { get; set; }
        }

        private class DataDealCompletionMetric
        {
            public PublisherContentType ContentType { get; set; }
            public int MediaCount { get; set; }
            public int Images { get; set; }
            public int Videos { get; set; }
            public int Carousels { get; set; }
            public long Impressions { get; set; }
            public long Reach { get; set; }
            public long Replies { get; set; }
            public long Saves { get; set; }
            public long Views { get; set; }
            public long Comments { get; set; }
            public long Actions { get; set; }
            public long Engagements { get; set; }
        }
    }
}
