using Nest;

// ReSharper disable InconsistentNaming

namespace Rydr.Api.Core.Models.Supporting
{
    public class EsCreatorStatAggregate
    {
        public long Creators { get; set; }
        public ExtendedStatsAggregate MediaCount { get; set; }
        public ExtendedStatsAggregate FollowedBy { get; set; }
        public ExtendedStatsAggregate Following { get; set; }
        public ExtendedStatsAggregate StoryEngagementRating { get; set; }
        public ExtendedStatsAggregate StoryImpressions { get; set; }
        public ExtendedStatsAggregate StoryReach { get; set; }
        public ExtendedStatsAggregate StoryActions { get; set; }
        public ExtendedStatsAggregate Stories { get; set; }
        public ExtendedStatsAggregate MediaEngagementRating { get; set; }
        public ExtendedStatsAggregate MediaTrueEngagementRating { get; set; }
        public ExtendedStatsAggregate MediaImpressions { get; set; }
        public ExtendedStatsAggregate MediaReach { get; set; }
        public ExtendedStatsAggregate MediaActions { get; set; }
        public ExtendedStatsAggregate Medias { get; set; }
        public ExtendedStatsAggregate AvgStoryImpressions { get; set; }
        public ExtendedStatsAggregate AvgMediaImpressions { get; set; }
        public ExtendedStatsAggregate AvgStoryReach { get; set; }
        public ExtendedStatsAggregate AvgMediaReach { get; set; }
        public ExtendedStatsAggregate AvgStoryActions { get; set; }
        public ExtendedStatsAggregate AvgMediaActions { get; set; }
        public ExtendedStatsAggregate Follower7DayJitter { get; set; }
        public ExtendedStatsAggregate Follower14DayJitter { get; set; }
        public ExtendedStatsAggregate Follower30DayJitter { get; set; }
        public ExtendedStatsAggregate AudienceUsa { get; set; }
        public ExtendedStatsAggregate AudienceEnglish { get; set; }
        public ExtendedStatsAggregate AudienceSpanish { get; set; }
        public ExtendedStatsAggregate AudienceMale { get; set; }
        public ExtendedStatsAggregate AudienceFemale { get; set; }
        public ExtendedStatsAggregate AudienceAge1317 { get; set; }
        public ExtendedStatsAggregate AudienceAge18Up { get; set; }
        public ExtendedStatsAggregate AudienceAge1824 { get; set; }
        public ExtendedStatsAggregate AudienceAge25Up { get; set; }
        public ExtendedStatsAggregate AudienceAge2534 { get; set; }
        public ExtendedStatsAggregate AudienceAge3544 { get; set; }
        public ExtendedStatsAggregate AudienceAge4554 { get; set; }
        public ExtendedStatsAggregate AudienceAge5564 { get; set; }
        public ExtendedStatsAggregate AudienceAge65Up { get; set; }
        public ExtendedStatsAggregate ImagesAvgAge { get; set; }
        public ExtendedStatsAggregate SuggestiveRating { get; set; }
        public ExtendedStatsAggregate ViolenceRating { get; set; }
        public ExtendedStatsAggregate Rydr7DayActivityRating { get; set; }
        public ExtendedStatsAggregate Rydr14DayActivityRating { get; set; }
        public ExtendedStatsAggregate Rydr30DayActivityRating { get; set; }
        public ExtendedStatsAggregate Requests { get; set; }
        public ExtendedStatsAggregate CompletedRequests { get; set; }
        public ExtendedStatsAggregate AvgCPMr { get; set; }
        public ExtendedStatsAggregate AvgCPMi { get; set; }
        public ExtendedStatsAggregate AvgCPE { get; set; }
        public ExtendedStatsAggregate Requests1 { get; set; }
        public ExtendedStatsAggregate CompletedRequests1 { get; set; }
        public ExtendedStatsAggregate AvgCPMr1 { get; set; }
        public ExtendedStatsAggregate AvgCPMi1 { get; set; }
        public ExtendedStatsAggregate AvgCPE1 { get; set; }
        public ExtendedStatsAggregate Requests2 { get; set; }
        public ExtendedStatsAggregate CompletedRequests2 { get; set; }
        public ExtendedStatsAggregate AvgCPMr2 { get; set; }
        public ExtendedStatsAggregate AvgCPMi2 { get; set; }
        public ExtendedStatsAggregate AvgCPE2 { get; set; }
        public ExtendedStatsAggregate Requests3 { get; set; }
        public ExtendedStatsAggregate CompletedRequests3 { get; set; }
        public ExtendedStatsAggregate AvgCPMr3 { get; set; }
        public ExtendedStatsAggregate AvgCPMi3 { get; set; }
        public ExtendedStatsAggregate AvgCPE3 { get; set; }
        public ExtendedStatsAggregate Requests4 { get; set; }
        public ExtendedStatsAggregate CompletedRequests4 { get; set; }
        public ExtendedStatsAggregate AvgCPMr4 { get; set; }
        public ExtendedStatsAggregate AvgCPMi4 { get; set; }
        public ExtendedStatsAggregate AvgCPE4 { get; set; }
        public ExtendedStatsAggregate Requests5 { get; set; }
        public ExtendedStatsAggregate CompletedRequests5 { get; set; }
        public ExtendedStatsAggregate AvgCPMr5 { get; set; }
        public ExtendedStatsAggregate AvgCPMi5 { get; set; }
        public ExtendedStatsAggregate AvgCPE5 { get; set; }
    }
}
