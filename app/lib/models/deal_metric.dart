class DealCompletionMediaMetrics {
  int postImpressions;
  int postReach;
  int postReachAvg;
  int postActions;
  int postReplies;
  int postSaves;
  int postViews;
  int postComments;

  int storyImpressions;
  int storyReach;
  int storyReachAvg;
  int storyActions;
  int storyReplies;
  int storySaves;
  int storyViews;
  int storyComments;

  int posts;
  int stories;
  int images;
  int videos;
  int carousels;

  int completedPostMedias;
  int completedStoryMedias;

  int postEngagements;
  int storyEngagements;

  double totalCompletionCost;
  int completedRequests;
  int completedRequestDeals;

  double avgCpmPerCompletion;
  double avgCpePerCompletion;
  double avgCogPerCompletedDeal;
  double avgCpmPerStory;
  double avgCpmPerPost;
  double avgCpePerStory;
  double avgCpePerPost;

  DealCompletionMediaMetrics.fromJson(Map<String, dynamic> json) {
    postImpressions = json['postImpressions'];
    postReach = json['postReach'];
    postReachAvg = json['postReachAvg'];
    postActions = json['postActions'];
    postReplies = json['postReplies'];
    postSaves = json['postSaves'];
    postViews = json['postViews'];
    postComments = json['postComments'];

    storyImpressions = json['storyImpressions'];
    storyReach = json['storyReach'];
    storyReachAvg = json['storyReachAvg'];
    storyActions = json['storyActions'];
    storyReplies = json['storyReplies'];
    storySaves = json['storySaves'];
    storyViews = json['storyViews'];
    storyComments = json['storyComments'];

    posts = json['posts'];
    stories = json['stories'];
    images = json['images'];
    videos = json['videos'];
    carousels = json['carousels'];

    postEngagements = json['postEngagements'];
    storyEngagements = json['storyEngagements'];

    completedPostMedias = json['completedPostMedias'];
    completedStoryMedias = json['completedStoryMedias'];

    totalCompletionCost = json['totalCompletionCost'].toDouble();
    completedRequests = json['completedRequests'];
    completedRequestDeals = json['completedRequestDeals'] ?? 0;

    avgCpmPerCompletion = json['avgCpmPerCompletion'].toDouble();
    avgCpePerCompletion = json['avgCpePerCompletion'].toDouble();
    avgCogPerCompletedDeal = json['avgCogPerCompletedDeal'].toDouble();
    avgCpmPerStory = json['avgCpmPerStory'].toDouble();
    avgCpmPerPost = json['avgCpmPerPost'].toDouble();
    avgCpePerStory = json['avgCpePerStory'].toDouble();
    avgCpePerPost = json['avgCpePerPost'].toDouble();
  }

  int get totalMedia => (posts ?? 0) + (stories ?? 0);
  int get totalEngagements => (storyEngagements ?? 0) + (postEngagements ?? 0);

  double get avgMediaPerDeal =>
      completedRequests != null && completedRequests > 0
          ? totalMedia / completedRequests
          : 0;

  double percentageOfTotal(int value, int total) => (value / total) * 100;

  String percentageForDisplay(int value, int total) =>
      ((value / total) * 100).toStringAsFixed(1) + '%';
}
