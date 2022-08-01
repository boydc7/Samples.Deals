import 'package:intl/intl.dart';
import 'package:rydrworkspaces/models/enums/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_insights_day.dart';

class PublisherInsightsMedia {
  int publisherMediaId;
  DateTime mediaCreatedOn;
  String period;
  String mediaUrl;
  String publisherUrl;
  int endTime;
  int engagements;
  int impressions;
  int views;
  int reach;
  int actions;
  int comments;
  int saves;
  int replies;
  double engagementRating;
  double trueEngagementRating;

  PublisherInsightsMedia.fromJson(Map<String, dynamic> json) {
    publisherMediaId = json['publisherMediaId'];
    mediaCreatedOn = DateTime.parse(json['mediaCreatedOn']);
    mediaUrl = json['mediaUrl'];
    publisherUrl = json['publisherUrl'];
    period = json['period'];
    endTime = json['endTime'];
    engagements = json['engagements'];
    impressions = json['impressions'];
    views = json['views'];
    reach = json['reach'];
    actions = json['actions'];
    comments = json['comments'];
    saves = json['saves'];
    replies = json['replies'];
    engagementRating = json['engagementRating'] != null
        ? json['engagementRating'].toDouble()
        : null;
    trueEngagementRating = json['trueEngagementRating'] != null
        ? json['trueEngagementRating'].toDouble()
        : null;
  }
}

class PublisherInsightsMediaSummary {
  int totalImpressions;
  PublisherInsightsDay maxImpressions;
  PublisherInsightsDay minImpressions;
  double avgImpressions;

  int totalReach;
  PublisherInsightsDay maxReach;
  PublisherInsightsDay minReach;
  double avgReach;

  int totalEngagement;
  PublisherInsightsDay maxEngagement;
  PublisherInsightsDay minEngagement;
  double avgEngagement;

  int totalLikes;
  PublisherInsightsDay maxLikes;
  PublisherInsightsDay minLikes;
  double avgLikes;

  int totalComments;
  PublisherInsightsDay maxComments;
  PublisherInsightsDay minComments;
  double avgComments;

  int totalSaves;
  PublisherInsightsDay maxSaves;
  PublisherInsightsDay minSaves;
  double avgSaves;

  int totalReplies;
  PublisherInsightsDay maxReplies;
  PublisherInsightsDay minReplies;
  double avgReplies;

  double engagementRate;
  double engagementRateTrue;

  // List<FlSpot> flSpotsReach = [];
  // List<FlSpot> flSpotsImpressions = [];
  // List<FlSpot> flSpotsEngagements = [];

  PublisherInsightsMediaSummary(
    List<PublisherInsightsMedia> items,
    int followers,
    PublisherContentType contentType,
  ) {
    /// we're guaranteed to have items.length > 0 here...

    final PublisherInsightsMedia _impMaxDay = items.reduce((curr, next) =>
        (curr.impressions ?? 0) > (next.impressions ?? 0) ? curr : next);

    final PublisherInsightsMedia _impMinDay = items.reduce((curr, next) =>
        (curr.impressions ?? 0) < (next.impressions ?? 0) ? curr : next);

    final PublisherInsightsMedia _reachMaxDay = items.reduce(
        (curr, next) => (curr.reach ?? 0) > (next.reach ?? 0) ? curr : next);

    final PublisherInsightsMedia _reachMinDay = items.reduce(
        (curr, next) => (curr.reach ?? 0) < (next.reach ?? 0) ? curr : next);

    final PublisherInsightsMedia _likesMaxDay = items.reduce((curr, next) =>
        (curr.actions ?? 0) > (next.actions ?? 0) ? curr : next);

    final PublisherInsightsMedia _likesMinDay = items.reduce((curr, next) =>
        (curr.actions ?? 0) < (next.actions ?? 0) ? curr : next);

    final PublisherInsightsMedia _commentsMaxDay = items.reduce((curr, next) =>
        (curr.comments ?? 0) > (next.comments ?? 0) ? curr : next);

    final PublisherInsightsMedia _commentsMinDay = items.reduce((curr, next) =>
        (curr.comments ?? 0) < (next.comments ?? 0) ? curr : next);

    final PublisherInsightsMedia _savesMaxDay = items.reduce(
        (curr, next) => (curr.saves ?? 0) > (next.saves ?? 0) ? curr : next);

    final PublisherInsightsMedia _savesMinDay = items.reduce(
        (curr, next) => (curr.saves ?? 0) < (next.saves ?? 0) ? curr : next);

    final PublisherInsightsMedia _repliesMaxDay = items.reduce((curr, next) =>
        (curr.replies ?? 0) > (next.replies ?? 0) ? curr : next);

    final PublisherInsightsMedia _repliesMinDay = items.reduce((curr, next) =>
        (curr.replies ?? 0) < (next.replies ?? 0) ? curr : next);

    final PublisherInsightsMedia _engagementsMaxDay = items.reduce(
        (curr, next) =>
            (curr.engagementRating ?? 0) > (next.engagementRating ?? 0)
                ? curr
                : next);

    final PublisherInsightsMedia _engagementsMinDay = items.reduce(
        (curr, next) =>
            (curr.engagementRating ?? 0) < (next.engagementRating ?? 0)
                ? curr
                : next);

    /// Impressions
    totalImpressions =
        items.map<int>((m) => m.impressions ?? 0).reduce((a, b) => a + b);
    avgImpressions = totalImpressions / items.length;

    maxImpressions = PublisherInsightsDay(
      _impMaxDay.impressions,
      _impMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _impMaxDay.publisherUrl
          : _impMaxDay.mediaUrl,
    );

    minImpressions = PublisherInsightsDay(
      _impMinDay.impressions,
      _impMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _impMinDay.publisherUrl
          : _impMinDay.mediaUrl,
    );

    /// Reach
    totalReach = items.map<int>((m) => m.reach ?? 0).reduce((a, b) => a + b);
    avgReach = totalReach / items.length;

    maxReach = PublisherInsightsDay(
      _reachMaxDay.reach,
      _reachMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _reachMaxDay.publisherUrl
          : _reachMaxDay.mediaUrl,
    );

    minReach = PublisherInsightsDay(
      _reachMinDay.reach,
      _reachMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _reachMinDay.publisherUrl
          : _reachMinDay.mediaUrl,
    );

    /// Likes (actions)
    totalLikes = items.map<int>((m) => m.actions ?? 0).reduce((a, b) => a + b);
    avgLikes = totalLikes / items.length;

    maxLikes = PublisherInsightsDay(
      _likesMaxDay.actions,
      _likesMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _likesMaxDay.publisherUrl
          : _likesMaxDay.mediaUrl,
    );

    minLikes = PublisherInsightsDay(
      _likesMinDay.actions,
      _likesMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _likesMinDay.publisherUrl
          : _likesMinDay.mediaUrl,
    );

    /// Comments
    totalComments =
        items.map<int>((m) => m.comments ?? 0).reduce((a, b) => a + b);
    avgComments = totalComments / items.length;

    maxComments = PublisherInsightsDay(
      _commentsMaxDay.comments,
      _commentsMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _commentsMaxDay.publisherUrl
          : _commentsMaxDay.mediaUrl,
    );

    minComments = PublisherInsightsDay(
      _commentsMinDay.comments,
      _commentsMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _commentsMinDay.publisherUrl
          : _commentsMinDay.mediaUrl,
    );

    /// Saves
    totalSaves = items.map<int>((m) => m.saves ?? 0).reduce((a, b) => a + b);
    avgSaves = totalSaves / items.length;

    maxSaves = PublisherInsightsDay(
      _savesMaxDay.saves,
      _savesMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _savesMaxDay.publisherUrl
          : _savesMaxDay.mediaUrl,
    );

    minSaves = PublisherInsightsDay(
      _savesMinDay.saves,
      _savesMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _savesMinDay.publisherUrl
          : _savesMinDay.mediaUrl,
    );

    /// Replies
    totalReplies =
        items.map<int>((m) => m.replies ?? 0).reduce((a, b) => a + b);
    avgReplies = totalReplies / items.length;

    maxReplies = PublisherInsightsDay(
      _repliesMaxDay.replies,
      _repliesMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _repliesMaxDay.publisherUrl
          : _repliesMaxDay.mediaUrl,
    );

    minReplies = PublisherInsightsDay(
      _repliesMinDay.replies,
      _repliesMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _repliesMinDay.publisherUrl
          : _repliesMinDay.mediaUrl,
    );

    /// Engagement
    totalEngagement = totalLikes + totalComments + totalSaves + totalReplies;
    avgEngagement = totalEngagement / items.length;

    maxEngagement = PublisherInsightsDay(
      _engagementsMaxDay.engagementRating?.ceil() ?? 0,
      _engagementsMaxDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _engagementsMaxDay.publisherUrl
          : _engagementsMaxDay.mediaUrl,
    );

    minEngagement = PublisherInsightsDay(
      _engagementsMinDay.engagementRating?.floor() ?? 0,
      _engagementsMinDay.mediaCreatedOn,
      contentType == PublisherContentType.post
          ? _engagementsMinDay.publisherUrl
          : _engagementsMinDay.mediaUrl,
    );

    engagementRateTrue =
        (avgLikes + avgComments + avgSaves) / avgImpressions * 100;
    engagementRate = (avgLikes + avgComments) / followers * 100;
  }

  String get totalImpressionsDisplay =>
      NumberFormat.decimalPattern().format(this.totalImpressions);

  String get totalReachDisplay =>
      NumberFormat.decimalPattern().format(this.totalReach);
}
