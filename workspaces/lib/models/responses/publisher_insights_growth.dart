import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_insights_growth.dart';

class PublisherInsightsGrowthResponse {
  List<PublisherInsightsGrowth> growth;
  PublisherAccount profile;
  DioError error;

  int followedBy;
  int follows;
  double followerRatio;
  bool hasResults = false;

  PublisherInsightsGrowthResponse(this.growth, this.profile, this.error);

  PublisherInsightsGrowthResponse.fromResponse(
    PublisherAccount profile,
    Map<String, dynamic> json,
  ) {
    growth = json['results'] != null
        ? json['results']
            .map((dynamic d) => PublisherInsightsGrowth.fromJson(d))
            .cast<PublisherInsightsGrowth>()
            .toList()
        : [];

    if (growth != null && growth.isEmpty == false) {
      followedBy = profile.publisherMetrics.followedBy.toInt();
      follows = profile.publisherMetrics.follows.toInt();
      followerRatio = follows > 0 ? followedBy / follows : 0;
      hasResults = true;
    }
  }

  PublisherInsightsGrowthResponse.withError(DioError error)
      : profile = null,
        growth = null,
        error = error;
}
