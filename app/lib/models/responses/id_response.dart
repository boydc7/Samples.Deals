import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class StringIdResponse extends BaseResponse<String> {
  StringIdResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(apiResponse, (j) => j['id']);

  get id => super.model;
}

class IntIdResponse extends BaseIntResponse {
  IntIdResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(apiResponse);

  get id => super.model;
}
