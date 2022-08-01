using Rydr.Api.Dto;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Search;
using Rydr.Api.Dto.Shared;
using ServiceStack;

// ReSharper disable InconsistentNaming

namespace Rydr.Api.QueryDto
{
    [Route("/query/creatorstats")]
    public class QueryCreatorStats : BaseCreatorSearch, IGet, IReturn<OnlyResultResponse<CreatorStatsResponse>>, IHasLatitudeLongitude, IHasPublisherAccountId { }

    public class CreatorStatsResponse
    {
        public long Creators { get; set; }

        public LongRange EstimatedStoryImpressions { get; set; }
        public LongRange EstimatedStoryReach { get; set; }
        public LongRange EstimatedStoryEngagements { get; set; }
        public LongRange EstimatedPostImpressions { get; set; }
        public LongRange EstimatedPostReach { get; set; }
        public LongRange EstimatedPostEngagements { get; set; }

        public LongRange StoryApprovalsForTargetImpressions { get; set; }
        public LongRange StoryApprovalsForTargetReach { get; set; }
        public LongRange StoryApprovalsForTargetEngagements { get; set; }
        public LongRange PostApprovalsForTargetImpressions { get; set; }
        public LongRange PostApprovalsForTargetReach { get; set; }
        public LongRange PostApprovalsForTargetEngagements { get; set; }

        public CreatorStat Followers { get; set; }
        public CreatorStat StoryEngagementRating { get; set; }
        public CreatorStat StoryImpressions { get; set; }
        public CreatorStat StoryReach { get; set; }
        public CreatorStat StoryActions { get; set; }
        public CreatorStat Stories { get; set; }
        public CreatorStat MediaEngagementRating { get; set; }
        public CreatorStat MediaTrueEngagementRating { get; set; }
        public CreatorStat MediaImpressions { get; set; }
        public CreatorStat MediaReach { get; set; }
        public CreatorStat MediaActions { get; set; }
        public CreatorStat Medias { get; set; }
        public CreatorStat AvgStoryImpressions { get; set; }
        public CreatorStat AvgMediaImpressions { get; set; }
        public CreatorStat AvgStoryReach { get; set; }
        public CreatorStat AvgMediaReach { get; set; }
        public CreatorStat AvgStoryActions { get; set; }
        public CreatorStat AvgMediaActions { get; set; }
        public CreatorStat Follower7DayJitter { get; set; }
        public CreatorStat Follower14DayJitter { get; set; }
        public CreatorStat Follower30DayJitter { get; set; }
        public CreatorStat AudienceUsa { get; set; }
        public CreatorStat AudienceEnglish { get; set; }
        public CreatorStat AudienceSpanish { get; set; }
        public CreatorStat AudienceMale { get; set; }
        public CreatorStat AudienceFemale { get; set; }
        public CreatorStat AudienceAge1317 { get; set; }
        public CreatorStat AudienceAge18Up { get; set; }
        public CreatorStat AudienceAge1824 { get; set; }
        public CreatorStat AudienceAge25Up { get; set; }
        public CreatorStat AudienceAge2534 { get; set; }
        public CreatorStat AudienceAge3544 { get; set; }
        public CreatorStat AudienceAge4554 { get; set; }
        public CreatorStat AudienceAge5564 { get; set; }
        public CreatorStat AudienceAge65Up { get; set; }
        public CreatorStat ImagesAvgAge { get; set; }
        public CreatorStat SuggestiveRating { get; set; }
        public CreatorStat ViolenceRating { get; set; }
        public CreatorStat Rydr7DayActivityRating { get; set; }
        public CreatorStat Rydr14DayActivityRating { get; set; }
        public CreatorStat Rydr30DayActivityRating { get; set; }
        public CreatorStat Requests { get; set; }
        public CreatorStat CompletedRequests { get; set; }
        public CreatorStat AvgCPMr { get; set; }
        public CreatorStat AvgCPMi { get; set; }
        public CreatorStat AvgCPE { get; set; }
        public long Creators1 { get; set; }
        public CreatorStat Requests1 { get; set; }
        public CreatorStat CompletedRequests1 { get; set; }
        public CreatorStat AvgCPMr1 { get; set; }
        public CreatorStat AvgCPMi1 { get; set; }
        public CreatorStat AvgCPE1 { get; set; }
        public long Creators2 { get; set; }
        public CreatorStat Requests2 { get; set; }
        public CreatorStat CompletedRequests2 { get; set; }
        public CreatorStat AvgCPMr2 { get; set; }
        public CreatorStat AvgCPMi2 { get; set; }
        public CreatorStat AvgCPE2 { get; set; }
        public long Creators3 { get; set; }
        public CreatorStat Requests3 { get; set; }
        public CreatorStat CompletedRequests3 { get; set; }
        public CreatorStat AvgCPMr3 { get; set; }
        public CreatorStat AvgCPMi3 { get; set; }
        public CreatorStat AvgCPE3 { get; set; }
        public long Creators4 { get; set; }
        public CreatorStat Requests4 { get; set; }
        public CreatorStat CompletedRequests4 { get; set; }
        public CreatorStat AvgCPMr4 { get; set; }
        public CreatorStat AvgCPMi4 { get; set; }
        public CreatorStat AvgCPE4 { get; set; }
        public long Creators5 { get; set; }
        public CreatorStat Requests5 { get; set; }
        public CreatorStat CompletedRequests5 { get; set; }
        public CreatorStat AvgCPMr5 { get; set; }
        public CreatorStat AvgCPMi5 { get; set; }
        public CreatorStat AvgCPE5 { get; set; }
    }

    public class CreatorStat
    {
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Avg { get; set; }
        public double Sum { get; set; }
        public double? StdDev { get; set; }
        public long? Count { get; set; }
    }
}
