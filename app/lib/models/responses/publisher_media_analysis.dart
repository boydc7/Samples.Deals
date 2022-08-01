import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_analysis.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherMediaAnalysisResponse
    extends BaseResponse<PublisherMediaAnalysis> {
  PublisherMediaAnalysisResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherMediaAnalysis.fromJson(j),
        );
}

class PublisherMediaAnalysisQueryResponse
    extends BaseResponses<PublisherMedia> {
  PublisherMediaAnalysisQueryResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherMedia.fromJson(d))
                    .cast<PublisherMedia>()
                    .toList()
                : []);
}
