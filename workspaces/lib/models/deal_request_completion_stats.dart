import 'package:rydrworkspaces/models/deal_request.dart';

/// generates and calculates various stats for a given deal that's completed
class DealRequestCompletionStats {
  bool hasPosts;
  bool hasStories;
  int completedMedia;
  int completedStories;
  int completedPosts;
  int replies;
  int reach;
  int likes;
  int saves;
  int videoViews;
  int actions;
  int comments;
  int storyImpressions;
  int postImpressions;
  int impressions;
  int storyReach;
  double avgStoryReach;
  int postReach;
  int postEngagement;
  int storyEngagement;

  double cost;
  double oneKImpressions;
  double oneKPostImpressions;
  double oneKStoryImpressions;
  double costImpressions;
  double costPostImpressions;
  double costStoryImpressions;
  double costEngagement;
  double costEngagementPost;
  double costEngagementStory;

  DealRequestCompletionStats(double dealCost, DealRequest request) {
    DealRequest req = request;

    /// copy from request model for easy access here
    completedMedia = req.completionMedia.length;
    completedStories = req.completedStories;
    completedPosts = req.completedPosts;
    hasPosts = req.completedPosts > 0;
    hasStories = req.completedStories > 0;

    /// return 0's for these if null
    replies = req.completionMediaStatValues.replies ?? 0;
    actions = req.completionMediaStatValues.actions ?? 0;
    reach = req.completionMediaStatValues.reach ?? 0;
    likes = req.completionMediaStatValues.actions ?? 0;
    saves = req.completionMediaStatValues.saved ?? 0;
    videoViews = req.completionMediaStatValues.videoViews ?? 0;
    comments = req.completionMediaStatValues.comments ?? 0;

    storyImpressions =
        hasStories ? req.completionStoryMediaStatValues.impressions ?? 0 : 0;
    postImpressions =
        hasPosts ? req.completionPostMediaStatValues.impressions ?? 0 : 0;
    impressions = storyImpressions + postImpressions;
    storyReach = hasStories ? req.completionStoryMediaStatValues.reach ?? 0 : 0;
    avgStoryReach = storyReach > 0 && req.completedStories > 0
        ? storyReach / req.completedStories
        : 0;
    postReach = hasPosts ? req.completionPostMediaStatValues.reach ?? 0 : 0;
    postEngagement = hasPosts ? likes + comments + saves : 0;
    storyEngagement = hasStories ? replies + storyImpressions : 0;

    cost = dealCost ?? 0;
    oneKImpressions = impressions / 1000;
    oneKPostImpressions = postImpressions / 1000;
    oneKStoryImpressions = storyImpressions / 1000;
    costImpressions = oneKImpressions > 0 ? cost / oneKImpressions : 0;
    costPostImpressions =
        oneKPostImpressions > 0 ? cost / oneKPostImpressions : 0;
    costStoryImpressions =
        oneKStoryImpressions > 0 ? cost / oneKStoryImpressions : 0;
    costEngagement = postEngagement > 0 || storyEngagement > 0
        ? cost / (postEngagement + storyEngagement)
        : 0;
    costEngagementPost = postEngagement > 0 ? cost / postEngagement : 0;
    costEngagementStory = storyEngagement > 0 ? cost / storyEngagement : 0;
  }
}
