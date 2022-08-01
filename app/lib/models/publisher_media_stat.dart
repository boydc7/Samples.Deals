import 'package:rydr_app/app/utils.dart';

class PublisherMediaStatValueName {
  static const String Engagement = "engagement";
  static const String Impressions = "impressions";
  static const String Reach = "reach";
  static const String CarouselAlbumImpressions = "carousel_album_impressions";
  static const String CarouselAlbumReach = "carousel_album_reach";
  static const String CarouselAlbumEngagement = "carousel_album_engagement";
  static const String CarouselAlbumSaved = "carousel_album_saved";
  static const String Comments = "comments";
  static const String Actions = "actions";
  static const String VideoViews = "video_views";
  static const String Exits = "exits";
  static const String Replies = "replies";
  static const String TapsForward = "taps_forward";
  static const String TapsBack = "taps_back";
  static const String Saved = "saved";
}

class PublisherMediaStat {
  int mediaId;
  String period;
  int endTime;
  DateTime lastSyncedOn;
  List<PublisherStatValue> stats;

  PublisherMediaStat.fromJson(Map<String, dynamic> json) {
    this.mediaId = json['publisherMediaId'];
    this.period = json['period'];
    this.endTime = json['endTime'];
    this.stats = json['stats'] != null ? jsonToStats(json['stats']) : null;
    this.lastSyncedOn = json['lastSyncedOn'] != null
        ? DateTime.parse(json['lastSyncedOn'])
        : null;
  }

  List<PublisherStatValue> jsonToStats(List<dynamic> json) {
    List<PublisherStatValue> stats = [];
    json.forEach((stat) {
      stats.add(PublisherStatValue.fromJson(stat));
    });

    return stats;
  }

  String get lastSyncedOnDisplayAgo =>
      this.lastSyncedOn == null ? 'recently' : Utils.formatAgo(lastSyncedOn);
}

class PublisherStatValue {
  final String name;
  final int value;

  PublisherStatValue(
    this.name,
    this.value,
  );

  PublisherStatValue.fromJson(Map<String, dynamic> json)
      : name = json['name'],
        value = json['value'];
}

class PublisherMediaStatValues {
  int engagement;
  int impressions;
  int reach;
  int carouselAlbumImpressions;
  int carouselAlbumReach;
  int carouselAlbumEngagement;
  int carouselAlbumSaved;
  int comments;
  int actions;
  int videoViews;
  int exits;
  int replies;
  int tapsForward;
  int tapsBack;
  int saved;

  List<PublisherStatValue> _values;

  PublisherMediaStatValues(List<PublisherStatValue> values) {
    _values = values;

    if (_values != null) {
      engagement = _get(PublisherMediaStatValueName.Engagement);
      impressions = _get(PublisherMediaStatValueName.Impressions);
      reach = _get(PublisherMediaStatValueName.Reach);
      carouselAlbumImpressions =
          _get(PublisherMediaStatValueName.CarouselAlbumImpressions);
      carouselAlbumReach = _get(PublisherMediaStatValueName.CarouselAlbumReach);
      carouselAlbumEngagement =
          _get(PublisherMediaStatValueName.CarouselAlbumEngagement);
      carouselAlbumSaved = _get(PublisherMediaStatValueName.CarouselAlbumSaved);
      comments = _get(PublisherMediaStatValueName.Comments);
      actions = _get(PublisherMediaStatValueName.Actions);
      videoViews = _get(PublisherMediaStatValueName.VideoViews);
      exits = _get(PublisherMediaStatValueName.Exits);
      replies = _get(PublisherMediaStatValueName.Replies);
      tapsForward = _get(PublisherMediaStatValueName.TapsForward);
      tapsBack = _get(PublisherMediaStatValueName.TapsBack);
      saved = _get(PublisherMediaStatValueName.Saved);
    }
  }

  int _get(String mediaStatValueName) {
    if (_values == null) {
      return null;
    }

    final PublisherStatValue stat = _values.firstWhere(
        (PublisherStatValue val) => val.name == mediaStatValueName, orElse: () {
      return null;
    });

    return stat == null ? null : stat.value;
  }
}
