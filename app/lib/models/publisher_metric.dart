import 'package:rydr_app/app/utils.dart';

class PublisherMetricName {
  static const String Media = "media";
  static const String Follows = "follows";
  static const String FollowedBy = "followedby";
  static const String StoryEngagementRating = "storyengagementrating";
  static const String RecentStoryCount = "recentstorycount";
  static const String RecentStoryActions = "recentstoryactions";
  static const String RecentStoryImpressions = "recentstoryimpressions";
  static const String RecentStoryReach = "recentstoryreach";
  static const String RecentEngagementRating = "recentengagementrating";
  static const String RecentMediaCount = "recentmediacount";
  static const String RecentMediaActions = "recentmediaactions";
  static const String RecentMediaImpressions = "recentmediaimpressions";
  static const String RecentMediaReach = "recentmediareach";
  static const String RecentMediaSaves = "recentmediasaves";
  static const String RecentMediaViews = "recentmediaviews";
  static const String RecentLikes = "recentlikes";
  static const String RecentComments = "recentcomments";
}

class PublisherMetric {
  String name;
  double value;

  PublisherMetric(
    this.name,
    this.value,
  );

  Map<String, dynamic> toJson() {
    return {this.name: this.value};
  }
}

class PublisherMetrics {
  double media;
  double follows;
  double followedBy;
  double storyEngagementRating;
  double recentStoryCount;
  double recentStoryActions;
  double recentStoryImpressions;
  double recentStoryReach;
  double recentEngagementRating;
  double recentMediaCount;
  double recentMediaActions;
  double recentMediaImpressions;
  double recentMediaReach;
  double recentMediaSaves;
  double recentMediaViews;
  double recentLikes;
  double recentComments;

  bool hasStats = false;
  List<PublisherMetric> _metrics;

  PublisherMetrics(List<PublisherMetric> metrics) {
    _metrics = metrics;

    hasStats = _metrics != null;
    media = _get(PublisherMetricName.Media);
    follows = _get(PublisherMetricName.Follows);
    followedBy = _get(PublisherMetricName.FollowedBy);
    storyEngagementRating = _get(PublisherMetricName.StoryEngagementRating);
    recentStoryCount = _get(PublisherMetricName.RecentStoryCount);
    recentStoryActions = _get(PublisherMetricName.RecentStoryActions);
    recentStoryImpressions = _get(PublisherMetricName.RecentStoryImpressions);
    recentStoryReach = _get(PublisherMetricName.RecentStoryReach);
    recentEngagementRating = _get(PublisherMetricName.RecentEngagementRating);
    recentMediaCount = _get(PublisherMetricName.RecentMediaCount);
    recentMediaActions = _get(PublisherMetricName.RecentMediaActions);
    recentMediaImpressions = _get(PublisherMetricName.RecentMediaImpressions);
    recentMediaReach = _get(PublisherMetricName.RecentMediaReach);
    recentMediaSaves = _get(PublisherMetricName.RecentMediaSaves);
    recentMediaViews = _get(PublisherMetricName.RecentMediaViews);
    recentLikes = _get(PublisherMetricName.RecentLikes);
    recentComments = _get(PublisherMetricName.RecentComments);
  }

  String avgStoriesPerDay(List media) {
    final firstPoint = media.first.mediaCreatedOn;
    final lastPoint = media.last.mediaCreatedOn;
    final difference = lastPoint.difference(firstPoint).inDays;
    final ending = media.length == 1
        ? ""
        : difference == 0
            ? "within 24 hours"
            : difference == 1 ? "over 1 day" : "over $difference days";
    final double average = media.length / difference;
    final String plural = media.length == 1 ? "story" : "stories";
    return media.length > 0
        ? "${media.length.toString()} $plural $ending" +
            ((difference == 1 || difference == 0)
                ? ""
                : " - ${average.toStringAsFixed(2)} stories per day")
        : "no average";
  }

  double get avgStoryImpressions =>
      recentStoryCount > 0 ? recentStoryImpressions / recentStoryCount : null;

  double get avgStoryReach =>
      recentStoryCount > 0 ? recentStoryReach / recentStoryCount : null;

  double get avgPostImpressions => recentMediaImpressions > 0
      ? recentMediaImpressions / recentMediaCount
      : null;

  double get avgPostReach =>
      recentMediaReach > 0 ? recentMediaReach / recentMediaCount : null;

  double get avgLikes =>
      recentMediaCount > 0 ? recentLikes / recentMediaCount : 0;
  double get avgComments =>
      recentMediaCount > 0 ? recentComments / recentMediaCount : 0;
  double get avgSaves =>
      recentMediaCount > 0 ? recentMediaSaves / recentMediaCount : 0;

  double storyCPM(double costOfGoods) =>
      avgStoryImpressions != null && costOfGoods > 0
          ? costOfGoods / avgStoryImpressions * 1000
          : null;

  double postCPM(double costOfGoods) =>
      avgPostImpressions != null && costOfGoods > 0
          ? costOfGoods / avgPostImpressions * 1000
          : null;

  double _get(String name) {
    PublisherMetric metric;

    if (_metrics != null) {
      metric = _metrics.firstWhere(
          (PublisherMetric metric) => metric.name == name, orElse: () {
        return null;
      });
    }

    if (metric != null) {
      return metric.value;
    }

    return 0;
  }

  String get postsDisplay => Utils.formatDoubleForDisplay(this.media);
  String get followedByDisplay => Utils.formatDoubleForDisplay(this.followedBy);
  String get followsDisplay => Utils.formatDoubleForDisplay(this.follows);
  String get avgStoryReachDisplay =>
      Utils.formatDoubleForDisplayAsInt(this.avgStoryReach);
  String get avgPostReachDisplay =>
      Utils.formatDoubleForDisplayAsInt(this.avgPostReach);
}
