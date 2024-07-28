namespace Rydr.FbSdk.Enums;

public static class FbIgInsights
{
    private const string _onlyStoryFields = "exits,replies,taps_forward,taps_back";
    private const string _storyFields = _onlyStoryFields + ",impressions,reach";
    private const string _photoFields = "engagement,impressions,reach,saved";

    private const string _videoFields = _photoFields + ",video_views";

    // Albums can take everything...
    private const string _albumFields = _videoFields + ",carousel_album_impressions,carousel_album_reach,carousel_album_engagement,carousel_album_saved," + _onlyStoryFields;

    public const string LifetimePeriod = "lifetime";
    public const long LifetimeEndTime = int.MaxValue;
    public const string CommentsName = "comments";
    public const string ActionsName = "actions";
    public const string RepliesName = "replies";
    public const string ImpressionsName = "impressions";
    public const string EngagementsName = "engagement";
    public const string ReachName = "reach";
    public const string SaveName = "saved";
    public const string AudienceCity = "audience_city";
    public const string AudienceCountry = "audience_country";
    public const string AudienceAgeGender = "audience_gender_age";
    public const string DailyFollowerCountName = "follower_count";
    public const string DailyOnlineFollowersName = "online_followers";
    public const string DailyProfileViewsName = "profile_views";
    public const string DailyWebsiteClicksName = "website_clicks";
    public const string DailyEmailContactsName = "email_contacts";
    public const string DailyGetDirectionsClicksName = "get_directions_clicks";
    public const string DailyPhoneCallClicksName = "phone_call_clicks";
    public const string DailyTextMessageClicksName = "text_message_clicks";

    public static readonly HashSet<string> EngageStatNames = new(StringComparer.OrdinalIgnoreCase)
                                                             {
                                                                 "engagement",
                                                                 "carousel_album_engagement",
                                                                 "replies"
                                                             };

    public static readonly HashSet<string> SaveStatNames = new(StringComparer.OrdinalIgnoreCase)
                                                           {
                                                               "saved",
                                                               "carousel_album_saved"
                                                           };

    public static readonly HashSet<string> ViewStatNames = new(StringComparer.OrdinalIgnoreCase)
                                                           {
                                                               "video_views",
                                                               "carousel_album_video_views"
                                                           };

    public static readonly HashSet<string> ReachStatNames = new(StringComparer.OrdinalIgnoreCase)
                                                            {
                                                                "reach",
                                                                "carousel_album_reach"
                                                            };

    public static readonly HashSet<string> ImpressionStatNames = new(StringComparer.OrdinalIgnoreCase)
                                                                 {
                                                                     "impressions",
                                                                     "carousel_album_impressions"
                                                                 };

    public static string GetInsightFieldsStringForMediaType(string mediaType, bool isStory)
    {
        if (mediaType.IndexOf("album", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return _albumFields;
        }

        if (isStory)
        {
            return _storyFields;
        }

        if (mediaType.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return _videoFields;
        }

        return _photoFields;
    }

    public static string GetUserDailyInsightFieldsString()
        => "email_contacts,follower_count,get_directions_clicks,impressions,phone_call_clicks,profile_views,reach,text_message_clicks,website_clicks";

    public static string GetUserLifetimeInsightFieldsString()
        => "audience_city,audience_country,audience_gender_age,audience_locale,online_followers";
}
