import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherAccountMediaVisionResponse
    extends BaseResponse<PublisherAccountMediaVision> {
  PublisherAccountMediaVisionResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherAccountMediaVision.fromJson(j),
        );
}
