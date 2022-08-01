import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherMediasResponse extends BaseResponses<PublisherMedia> {
  PublisherMediasResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherMedia.fromJson(d))
                    .cast<PublisherMedia>()
                    .toList()
                : []);
}

class PublisherMediaResponse extends BaseResponse<PublisherMedia> {
  PublisherMediaResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherMedia.fromJson(j),
        );
}
