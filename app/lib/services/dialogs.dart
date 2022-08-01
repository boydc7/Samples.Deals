import 'dart:async';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/dialog.dart';
import 'package:rydr_app/models/responses/dialog_messages.dart';
import 'package:rydr_app/models/responses/message.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/models/record_type.dart';

class MessageService {
  /// start a new dialog between two parties
  static Future<MessageResponse> addMessage(
    RecordType from,
    RecordType to,
    String message,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance.post(
      'messages',
      body: {
        "from": from.toJson(),
        "to": to.toJson(),
        "message": message,
      },
    );

    return MessageResponse.fromApiResponse(apiResponse);
  }

  static Future<DialogResponse> fetchDialog(int dialogId) async {
    final ApiResponse apiResponse =
        await AppApi.instance.get('dialogs/$dialogId');

    return DialogResponse.fromApiResponse(apiResponse);
  }

  static Future<DialogMessagesResponse> fetchDialogMessages({
    int dialogId,
    int sentAfterId,
    int sentBeforeId,
    int skip,
    int take,
  }) async {
    final Map<String, dynamic> paramsMap = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        paramsMap[fieldName] = value.toString();
      }
    }

    addIfNonNull('take', take);
    addIfNonNull('sentAfterId', sentAfterId);
    addIfNonNull('sentBeforeId', sentBeforeId);

    final ApiResponse apiResponse = await AppApi.instance.get(
      'dialogs/$dialogId/messages',
      queryParams: paramsMap,
    );

    return DialogMessagesResponse.fromApiResponse(apiResponse);
  }

  /// mark an entire dialog as read
  static void markDialogRead(int dialogId) =>
      AppApi.instance.put('dialogs/$dialogId/read');
}
