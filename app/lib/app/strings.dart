import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/publisher_account.dart';

class AppStrings {
  final bool _isMe;
  final String _userName;

  AppStrings(PublisherAccount profile)
      : _isMe = profile?.id == appState?.currentProfile?.id,
        _userName = profile?.userName;

  final String sevenDays = '7 days';
  final String thirtyDays = '30 days';
  final String totalMale = "Total Male";
  final String totalFemale = "Total Female";
  final String totalUnknown = "Total Unknown";

  final String averageImpressions = 'Avg Impressions';
  final String averageImpressionsStory = 'Impressions per Story';
  final String averageImpressionsPost = 'Impressions per Post';
  final String averageReach = 'Avg Accounts Reached';
  final String averageReachStory = 'Reach per Story';
  final String averageReachPost = 'Reach per Post';
  final String emailTaps = 'Email Taps';
  final String discovery = 'Discovery';
  final String engagement = 'Engagement';
  final String followGrowthTitle = 'Growth';
  final String followGrowthTotalFollowers = 'Total Followers';
  final String followGrowthTotalFollowing = 'Total Following';
  final String followGrowthSheetTitle = 'What is overall growth?';
  final String followGrowthRate = 'Growth Rate';
  final String followGrowthRatio = 'Followers/\nFollowing Ratio';
  final String followLocationTitle = 'Top Locations';
  final String followAgeGenderTitle = 'Age and Gender';
  final String impressions = 'Impressions';
  final String phoneTaps = 'Phone Taps';
  final String postAvgLikes = 'Average Likes per Post';
  final String postAvgComments = 'Average Comments per Post';
  final String postAvgSaves = 'Average Saves per Post';
  final String postEngagementSheetTitle = 'What is post engagement?';
  final String postEngagementPerPost = "Engagement Rate per Post";
  final String postEngagementShortDescription =
      "The total number of likes, comments, and saves.";
  final String postEngagementDescription =
      "The total number of likes, comments, and saves, divided by the total number of followers.";
  final String postFollowersSeeingPost = "Followers Seeing a Post";
  final String postFollowersSeeingPostDescription =
      "The total number of impressions, divided by the total number of followers.";
  final String postImpReachSheetTitle = 'What is post discovery?';
  final String postImpressionsDescription =
      "Total number of times a post has been seen.";
  final String postReachDescription =
      "Number of unique accounts that have seen a post.";
  final String postEngagement = "Engagement Rate";
  final String postTrueEngagement = "True Engagement Rate";
  final String postTrueEngagementDescription =
      "The total number of likes, comments, and saves, divided by the total number of impressions.";
  final String profileImpReachTitle = 'Profile Discovery';
  final String profileImpReachSheetTitle = 'What is profile discovery?';
  final String profileInteractionsTapsTitle = 'Interactions: Taps';
  final String profileInteractionsTapSheetTitle = 'What are tap interactions?';
  final String profileInteractionsViewsTitle = 'Interactions: Views';
  final String profileInteractionsViewsSheetTitle =
      'What are view interactions?';
  final String profileReachDescription =
      'The number of unique accounts that have seen a post or story. This metric is an estimate and may not be exact.';
  final String profileViews = 'Profile Views';
  final String reach = 'Reach';
  final String replies = 'Replies';
  final String storyImpReachSheetTitle = 'What is story discovery?';
  final String storyImpressionsDescription =
      "Total number of times a story has been seen.";
  final String storyReachDescription =
      'Number of unique accounts that have seen a story.';
  final String storyEngagement = "Story Engagement Rate";
  final String storyEngagementRateDescription =
      "Total number of impressions plus the total number of replies, divided by the total number of followers.";
  final String storyEngagementPerStory = "Engagement Rate per Story";
  final String storyEngagementFormula =
      "Story Engagement\nImpressions + replies / followers";
  final String storyEngagementSheetTitle = 'What is story engagement?';
  final String storyReplies = "Replies per Story";
  final String textTaps = 'Text Taps';
  final String totalImpressions = 'Total Impressions';
  final String totalReach = 'Total Accounts Reached';
  final String totalProfileViews = 'Total Profile Views';
  final String totalWebsiteTaps = 'Total Website Taps';
  final String totalPhoneTaps = 'Total Phone Taps';
  final String totalEmailTaps = 'Total Email Taps';
  final String totalTextTaps = 'Total Text Taps';
  final String websiteTaps = 'Website Taps';

  /// Follower Growth
  String get followGrowthSubtitle => _isMe
      ? 'How your follower count has changed.'
      : 'How $_userName\'s follower count has changed.';

  String get followGrowthSheetSubtitle => _isMe
      ? 'How your audience has grown over the last 7 or 30 days.'
      : 'How $_userName\'s audience has grown over the last 7 or 30 days.';

  String get followGrowthRatioDescription => _isMe
      ? 'The number of your followers divided by the number of people you are following.'
      : 'The number of $_userName\'s followers divided by the number of people they are following.';

  String get followGrowthRateDescription => _isMe
      ? 'Your current follower count minus your previous follower count (7 days or 30 days ago), divided by the previous follower count, multiplied by 100.'
      : '$_userName\'s current follower count minus their previous follower count (7 days or 30 days ago), divided by the previous follower count, multiplied by 100.';

  /// Follower Locations
  String get followLocationsSubtitle => _isMe
      ? "The places where your followers are concentrated."
      : "The places where $_userName\'s followers are concentrated.";

  /// Follower Age/Gender
  String get followAgeGenerSubtitle => _isMe
      ? "The age and gender diversity of your followers."
      : "The age and gender diversity of $_userName\'s followers.";

  /// Profile: Interations (shared)
  String get profileInteractionsSubtitle => _isMe
      ? 'The actions people take when they engage with your account.'
      : 'The actions people take when they engage with $_userName\'s account.';

  /// Profile: Interactions - Views
  String get profileInteractionsViewsDescription => _isMe
      ? 'The total number of times your profile was viewed.'
      : 'The total number of times $_userName\'s profile was viewed.';

  /// Profile: Interactions - Taps
  String get profileInteractionsWebsiteTapsDescription => _isMe
      ? 'The total number of taps on the website in your profile.'
      : 'The total number of taps on the website in $_userName\'s profile.';

  String get profileInteractionsEmailTapsDescription => _isMe
      ? 'The total number of taps to email your account.'
      : 'The total number of taps to email $_userName\'s account.';

  String get profileInteractionsPhoneTapsDescription => _isMe
      ? 'The total number of taps to call your account.'
      : 'The total number of taps to call $_userName\'s account.';

  String get profileInteractionsTextTapsDescription => _isMe
      ? 'The total number of taps to text your account.'
      : 'The total number of taps to text $_userName\'s account.';

  /// Profile: Discovery (Impressions/Reach)
  String get profileImpReachSubtitle => _isMe
      ? 'How many people see your content.'
      : 'How many people see $_userName\'s content.';

  String get profileImpressionsDescription => _isMe
      ? 'The total number of times all of your posts have been seen.'
      : 'The total number of times all of $_userName\'s posts have been seen.';

  /// Stories: Discovery (Impressions/Reach)
  String get storyImpReachSubtitle => _isMe
      ? 'How many people see your stories.'
      : 'How many people see $_userName\'s stories.';

  String get storyTotalReachDescription => _isMe
      ? 'Number of non-unique accounts that have seen your stories.'
      : 'Number of non-unique accounts that have seen $_userName\'s stories.';

  String get storyAvgReachDescription => _isMe
      ? 'Total number of accounts that have seen your stories, divided by the total number of recent stories.'
      : 'Total number of accounts that have seen $_userName\'s stories, divided by the total number of recent stories.';

  /// Stories: Engagement
  String get storyEngagementSubtitle => _isMe
      ? "How people interact with your stories."
      : "How people interact with $_userName\'s stories.";

  String get storyRepliesDescription => _isMe
      ? "Number of times a user swipes up on a story and replies to you."
      : "Number of times a user swipes up on a story and replies to $_userName.";

  /// Posts: Engagement
  String get postDiscoverySubtitle => _isMe
      ? "How many people see your posts."
      : "How many people see $_userName\'s posts.";

  String get postDiscoverySheetSubtitle => _isMe
      ? "How many people have seen your latest posts."
      : "How many people have seen $_userName\'s latest posts.";

  String get postEngagementSubtitle => _isMe
      ? "How people interact with your posts."
      : "How people interact with $_userName\'s posts.";

  String get postTotalReachDescription => _isMe
      ? "Number of non-unique accounts that have seen your posts."
      : "Number of non-unique accounts that have seen $_userName/'s posts.";

  String get postAvgReachDescription => _isMe
      ? "Total number of accounts that have seen your posts, divided by the total number of recent posts."
      : "Total number of accounts that have seen $_userName/'s posts, divided by the total number of recent posts.";

  /// No Results
  String get profileImpReachNoResults => _isMe
      ? "We don't have enough data yet to analyze your profile impressions and reach."
      : "We don't have enough data yet to analyze $_userName's profile impressions and reach.";

  String get profileProfileViewsNoResults => _isMe
      ? "We don't have enough data yet to analyze your profile views."
      : "We don't have enough data yet to analyze $_userName's profile views.";

  String get profileProfileVStillSyncing => _isMe
      ? "We're still syncing your insights from Instagram..."
      : "We're still syncing $_userName's insights from Instagram...";

  String get profileProfileInteractionsNoResults => _isMe
      ? "We don't have enough data yet to analyze your profile interactions."
      : "We don't have enough data yet to analyze $_userName's profile interactions.";

  String get storyInsightNoResults => _isMe
      ? "Start Posting Stories!\nAll of the insights will show here."
      : "$_userName hasn't posted any stories since connecting to RYDR.";

  String get storyInsightStillSyncing => _isMe
      ? "We're still syncing your media and\ninsights from Instagram..."
      : "We're still syncing $_userName's media and\ninsights from Instagram...";

  String get postInsightNoResults => _isMe
      ? "No Post Insights.\n\nIf you recently switched to an Instagram\nBusiness or Creator profile, start posting!\nYour insights will show here."
      : "We don't have enough data yet\nto analyze $_userName's posts.";

  String get postInsightStillSyncing => _isMe
      ? "We're still syncing your media and\ninsights from Instagram..."
      : "We're still syncing $_userName's media and\ninsights from Instagram...";

  String get followerGrowthNoResults => _isMe
      ? "We don't have enough data yet to analyze your follower growth."
      : "We don't have enough data yet to analyze $_userName's follower growth.";

  String get followerLocationsNoResults => _isMe
      ? "We don't have enough data yet to analyze your followers locations."
      : "We don't have enough data yet to analyze $_userName\'s followers locations.";

  String get followerAgeGenderNoResults => _isMe
      ? "We don't have enough data yet to analyze your followers diversity."
      : "We don't have enough data yet to analyze $_userName\'s followers diversity.";
}
