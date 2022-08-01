import 'package:rydr_app/models/hashtag_stat.dart';

class Hashtag {
  int id;
  String publisherId;
  String name;
  List<HashtagStat> stats;

  //int mediaCount;

  Hashtag({
    this.id,
    this.publisherId,
    this.name,
  });

  /// get a stats' value based on the type, returns empty string instead of null
  /// because in most cases we put this into displays
  String getStat(hashtagStatType type) {
    HashtagStat stat = this.stats.firstWhere(
          (stat) => stat.type == type,
          orElse: () => null,
        );

    if (stat != null) {
      return stat.value.toString();
    }

    return "";
  }

  String get subtitle {
    return getStat(hashtagStatType.mediaCount);
  }

  /// response from instagram are in this format
  Hashtag.fromInstaJson(Map<String, dynamic> json) {
    this.publisherId = json['hashtag']['id'].toString();
    this.name = json['hashtag']['name'];

    this.stats = [
      HashtagStat(
          hashtagStatType.mediaCount, json['hashtag']['media_count'].toString())
    ];
  }

  /// converts json coming from server into hashtag
  Hashtag.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.publisherId = json['publisherId'];
    this.name = json['name'];

    this.stats = jsonToStats(json['stats']);
  }

  List<HashtagStat> jsonToStats(List<dynamic> json) {
    List<HashtagStat> stats = [];

    json.forEach((stat) {
      stats.add(HashtagStat(hashtagStatType.mediaCount, stat['value']));
    });

    return stats;
  }
}
