import 'package:rydr_app/models/publisher_insights_growth.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherInsightsGrowthResponse
    extends BaseResponses<PublisherInsightsGrowth> {
  PublisherInsightsGrowthResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherInsightsGrowth.fromJson(d))
                    .cast<PublisherInsightsGrowth>()
                    .toList()
                : []);
}

class PublisherInsightsGrowthResponseWithData {
  final List<PublisherInsightsGrowth> growth;
  final PublisherAccount profile;

  int followedBy;
  int follows;
  double followerRatio;
  bool hasResults = false;

  PublisherInsightsGrowthResponseWithData(this.growth, this.profile) {
    if (growth != null && growth.isEmpty == false) {
      followedBy = profile.publisherMetrics.followedBy.toInt();
      follows = profile.publisherMetrics.follows.toInt();
      followerRatio = follows > 0 ? followedBy / follows : 0;
      hasResults = true;
    }
  }
}
