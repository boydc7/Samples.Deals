import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/models/record_type.dart';
import 'package:rydr_app/models/responses/dialog_messages.dart';
import 'package:rydr_app/models/responses/message.dart';
import 'package:rydr_app/services/deal.dart';
import 'package:rydr_app/services/dialogs.dart';

class RequestDialogBloc {
  final _messages = BehaviorSubject<List<DialogMessage>>();
  final _deal = BehaviorSubject<Deal>();

  dispose() {
    _messages.close();
    _deal.close();
  }

  BehaviorSubject<List<DialogMessage>> get messages => _messages.stream;
  BehaviorSubject<Deal> get deal => _deal.stream;

  int _dialogId;
  bool _loading = false;
  bool _nomore = false;

  void loadMessages(
    Deal dealFromParent, {
    int dealId,
    int publisherAccountId,
    int sentAfterId,
    int sentBeforeId,
  }) async {
    _loading = true;

    _deal.sink.add(dealFromParent);

    /// skip if we've loaded the deal already
    /// as we call this on each load of messages, prior or new
    if (deal.value == null) {
      final DealResponse dealResponse = await DealService.getDeal(
        dealId,
        requestedPublisherAccountId: publisherAccountId,
      );

      _deal.sink.add(dealResponse.model);
    }

    /// if for some reason we don't have a deal then return out
    if (deal.value == null) {
      return null;
    }

    /// if we have a last message on the request then grab the dialog id from there
    /// otherwise this would be starting a new dialog and we'd skip loading the existing messages
    _dialogId = deal.value.request.lastMessage != null
        ? deal.value.request.lastMessage.dialogId
        : null;

    /// what would happen if dialogId is null? would throw exception
    /// NOTE!: seems that even when there is no dialog created from the original request
    /// we still are able to make this call with a NULL as dialogId
    final DialogMessagesResponse res = await MessageService.fetchDialogMessages(
      dialogId: _dialogId,
      sentAfterId: sentAfterId,
      sentBeforeId: sentBeforeId,
    );

    if (res.models != null) {
      if (res.models.isEmpty == false) {
        List<DialogMessage> items = [];
        for (int x = 0; x < res.models.length; x++) {
          items.add(res.models[x]);
        }

        _messages.sink.add(items);
      } else {
        _nomore = true;
      }
    }

    _loading = false;
  }

  void loadPrevious() {
    if (_loading || _nomore) {
      return;
    }

    loadMessages(
      deal.value,
      sentBeforeId: messages.value[messages.value.length - 1].id,
    );
  }

  void sendMessage(String message) async {
    List<DialogMessage> existingMessages = messages.value ?? [];
    RecordType from;
    RecordType to;

    /// set the to/from based on who is sending the message
    if (appState.currentProfile.isBusiness) {
      from = RecordType(
        _deal.value.id,
        RecordOfType.Deal,
      );
      to = RecordType(
        _deal.value.request.publisherAccount.id,
        RecordOfType.PublisherAccount,
      );
    } else {
      from = RecordType(
        appState.currentProfile.id,
        RecordOfType.PublisherAccount,
      );
      to = RecordType(
        _deal.value.id,
        RecordOfType.Deal,
      );
    }

    /// add the message immediately before saving, then update it once save completes
    final DialogMessage m = DialogMessage(
      id: 0,
      message: message,
      sentOn: DateTime.now(),
      isDelivered: false,
      isDelivering: true,
      from: appState.currentProfile,
    );

    existingMessages.insert(0, m);

    _messages.sink.add(existingMessages);

    final MessageResponse res =
        await MessageService.addMessage(from, to, message);

    if (res.error == null) {
      existingMessages[0].isDelivered = true;
      existingMessages[0].isDelivering = false;
      existingMessages[0].id = res.model.id;
      existingMessages[0].dialogId = res.model.dialogId;

      /// update the deal.request lastMessage with this new message
      _deal.value.request.lastMessage = m;

      /// now we'll have the dialog id from the message
      _dialogId = res.model.dialogId;

      loadMessages(
        _deal.value,
        sentAfterId: existingMessages[0].id,
      );
    } else {
      existingMessages[0].isDelivering = false;
      existingMessages[0].isDelivered = false;
    }

    _messages.sink.add(existingMessages);
  }

  void processMessage(AppNotification message) {
    List<DialogMessage> existingMessages = messages.value ?? [];

    /// check if the message is intended for the current user
    /// has the right type, and matches the current deal we're on
    if (message.toPublisherAccount != null &&
        message.toPublisherAccount.id == appState.currentProfile.id &&
        message.type == AppNotificationType.message &&
        message.forRecord != null &&
        message.forRecord.type == RecordOfType.Deal &&
        message.forRecord.id == _deal.value.id) {
      /// create a new dialog message from the push notification
      final DialogMessage dialogMessage = DialogMessage(
        from: message.fromPublisherAccount,
        isDelivered: true,
        isDelivering: false,
        sentOn: message.occurredOn,
        message: message.body,
      );

      /// insert it at the bottom of the current messages
      existingMessages.insert(0, dialogMessage);

      /// update the deal.request lastMessage with this new message
      _deal.value.request.lastMessage = dialogMessage;

      _messages.sink.add(existingMessages);
    }
  }
}
