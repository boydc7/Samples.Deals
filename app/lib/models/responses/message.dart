import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class MessageResponse extends BaseResponse<DialogMessage> {
  MessageResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => DialogMessage.fromJson(j),
        );
}
