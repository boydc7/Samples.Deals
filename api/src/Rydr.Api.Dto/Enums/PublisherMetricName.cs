namespace Rydr.Api.Dto.Enums
{
    public static class PublisherMetricName
    {
        public const string Media = "media";
        public const string Follows = "follows";
        public const string FollowedBy = "followedby";
        public const string RecentComments = "recentcomments";
        public const string RecentLikes = "recentlikes";
        public const string StoryEngagementRating = "storyengagementrating";
        public const string RecentStoryCount = "recentstorycount";
        public const string RecentStoryActions = "recentstoryactions";
        public const string RecentStoryImpressions = "recentstoryimpressions";
        public const string RecentStoryReach = "recentstoryreach";
        public const string RecentEngagementRating = "recentengagementrating";
        public const string RecentTrueEngagementRating = "recenttrueengagementrating";
        public const string RecentMediaCount = "recentmediacount";
        public const string RecentMediaActions = "recentmediaactions";
        public const string RecentMediaImpressions = "recentmediaimpressions";
        public const string RecentMediaReach = "recentmediareach";
        public const string RecentMediaSaves = "recentmediasaves";
        public const string RecentMediaViews = "recentmediaviews";
    }

    public static class PublisherMediaValues
    {
        public const int DaysBackToKeepMedia = -950;
    }
}
