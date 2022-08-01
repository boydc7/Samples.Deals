import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherApprovedMediaResponse
    extends BaseResponse<PublisherApprovedMedia> {
  PublisherApprovedMediaResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherApprovedMedia.fromJson(j),
        );
}

class PublisherApprovedMediasResponse
    extends BaseResponses<PublisherApprovedMedia> {
  PublisherApprovedMediasResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherApprovedMedia.fromJson(d))
                    .cast<PublisherApprovedMedia>()
                    .toList()
                : []);

  PublisherApprovedMediasResponse.fromModels(
      List<PublisherApprovedMedia> models)
      : super.fromModels(
          models,
        );
}
