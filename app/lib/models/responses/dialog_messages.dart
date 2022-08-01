import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class DialogMessagesResponse extends BaseResponses<DialogMessage> {
  DialogMessagesResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => j != null
              ? j
                  .map((dynamic d) => DialogMessage.fromJson(d))
                  .cast<DialogMessage>()
                  .toList()
              : [],
        );
}
