import 'package:rydr_app/models/publisher_insights_media.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherInsightsMediaResponse
    extends BaseResponses<PublisherInsightsMedia> {
  PublisherInsightsMediaResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherInsightsMedia.fromJson(d))
                    .cast<PublisherInsightsMedia>()
                    .toList()
                : []);
}
