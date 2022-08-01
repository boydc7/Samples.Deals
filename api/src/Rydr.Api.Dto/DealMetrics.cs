using System;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto
{
    [Route("/dealmetrics/completion", "GET")]
    public class GetDealCompletionMediaMetrics : RequestBase, IGet, IReturn<OnlyResultResponse<DealCompletionMediaMetrics>>, IHasPublisherAccountId
    {
        public long DealId { get; set; }
        public long PublisherAccountId { get; set; }
        public DateTime? CompletedOnStart { get; set; }
        public DateTime? CompletedOnEnd { get; set; }
    }

    [Route("/dealmetrics/{publisheridentifier}/delinquent", "GET")]
    public class GetDelinquentDealRequests : RequestBase, IGet, IReturn<OnlyResultResponse<DelinquentDealRequestResult>>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
    }

    [Route("/dealmetrics/{dealid}/{metrictype}", "POST")]
    public class PostDealMetric : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
    {
        public long DealId { get; set; }
        public DealTrackMetricType MetricType { get; set; }
        public long PublisherAccountId { get; set; }
    }

    public class DelinquentDealRequestResult
    {
        public int DelinquentCount { get; set; }
    }

    public class DealCompletionMediaMetrics
    {
        public long PublisherAccountId { get; set; }

        public long PostImpressions { get; set; }
        public long PostReach { get; set; }
        public long PostReachAvg { get; set; }
        public long PostActions { get; set; }
        public long PostReplies { get; set; }
        public long PostSaves { get; set; }
        public long PostViews { get; set; }
        public long PostComments { get; set; }

        public long StoryImpressions { get; set; }
        public long StoryReach { get; set; }
        public long StoryReachAvg { get; set; }
        public long StoryActions { get; set; }
        public long StoryReplies { get; set; }
        public long StorySaves { get; set; }
        public long StoryViews { get; set; }
        public long StoryComments { get; set; }

        public long Posts { get; set; }
        public long Stories { get; set; }
        public long Images { get; set; }
        public long Videos { get; set; }
        public long Carousels { get; set; }

        public long PostEngagements { get; set; }
        public long StoryEngagements { get; set; }

        public double TotalCompletionCost { get; set; }
        public long CompletedRequests { get; set; }
        public long CompletedRequestDeals { get; set; }
        public long CompletedPostMedias { get; set; }
        public long CompletedStoryMedias { get; set; }

        public double AvgCpmPerCompletion { get; set; }
        public double AvgCpmPerStory { get; set; }
        public double AvgCpmPerPost { get; set; }

        public double AvgCpePerCompletion { get; set; }
        public double AvgCogPerCompletedDeal { get; set; }
        public double AvgCpePerStory { get; set; }
        public double AvgCpePerPost { get; set; }
    }
}
