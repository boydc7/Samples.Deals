import 'package:rydr_app/models/dialog.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class DialogResponse extends BaseResponse<MessageDialog> {
  DialogResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => MessageDialog.fromJson(j),
        );
}
