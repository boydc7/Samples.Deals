import 'dart:convert';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/publisher_account.dart';

import 'package:rydr_app/models/record_type.dart';
import 'package:rydr_app/app/routing.dart';

class AppNotification {
  AppNotificationType type;
  String notificationId;
  String title;
  String body;
  String forRecordName;
  String route;

  int workspaceId;
  PublisherAccount fromPublisherAccount;
  PublisherAccount toPublisherAccount;
  RecordType forRecord;
  DateTime occurredOn;
  bool isRead;

  Map<String, dynamic> _dataObj;

  /// this will tell us if we had to switch the user in order
  /// to process an action on this message
  bool switchedUser = false;

  AppNotification.fromJson(Map<String, dynamic> notification) {
    this.notificationId = notification['notificationId'];
    this.title = notification['title'];
    this.body = notification['body'];
    this.forRecordName = notification['forRecordName'];
    this.isRead = notification['isRead'];

    /// get additional data from the notification
    this.parseAdditional(notification);
  }

  AppNotification.fromMessage(Map<String, dynamic> message) {
    /// On Android, the message contains an additional field data containing the data.
    /// On iOS, the data is directly appended to the message and the additional data-field is omitted.
    /// https://github.com/FirebaseExtended/flutterfire/tree/master/packages/firebase_messaging#notification-messages-with-additional-data
    final dynamic data = message['data'] ?? message;

    /// firebase sends different notifications for ios vs android
    /// here we'll parse them into a generic model used for both

    if (message.containsKey('notification')) {
      this.title = message['notification']['title'];
      this.body = message['notification']['body'];
    } else if (message.containsKey('aps')) {
      this.title = message['aps']['alert']['title'];
      this.body = message['aps']['alert']['body'];
    }

    this._dataObj =
        data['RydrObject'] != null ? json.decode(data['RydrObject']) : null;

    /// if we have a data object then that's our custom data encoded
    /// as a json object sent along with the notification and likley contains
    /// things like the from/to publisher accounts, as well as forRecord and type of notification
    if (_dataObj != null) {
      this.parseAdditional(_dataObj);
    }
  }

  parseAdditional(Map<String, dynamic> data) {
    /// get workspaceid, default to zero if null
    this.workspaceId = data['workspaceId'] ?? 0;

    if (data['fromPublisherAccount'] != null) {
      this.fromPublisherAccount =
          PublisherAccount.fromJson(data['fromPublisherAccount']);
    }

    if (data['toPublisherAccount'] != null) {
      this.toPublisherAccount =
          PublisherAccount.fromJson(data['toPublisherAccount']);
    }

    if (data['forRecord'] != null) {
      this.forRecord = RecordType.fromJson(data['forRecord']);
    }

    /// parse the notification type and timestamp
    this.type = appNotificationTypeFromString(data['notificationType']);
    this.occurredOn = DateTime.parse(data['occurredOn']);

    /// next, let's try to identify the "route" to which we'd want to navigate
    /// were the user to click on the notification (either local or native)
    /// we can then compare that to the current route they are on and decide
    /// whether or not the notification should be supressed or shown

    if ((this.type == AppNotificationType.dealRequested ||
            this.type == AppNotificationType.dealRequestApproved ||
            this.type == AppNotificationType.dealInvited ||
            this.type == AppNotificationType.dealRequestCancelled ||
            this.type == AppNotificationType.dealRequestRedeemed ||
            this.type == AppNotificationType.dealRequestCompleted ||
            this.type == AppNotificationType.dealRequestDelinquent ||
            this.type == AppNotificationType.dealRequestDenied) &&
        this.forRecord != null) {
      route = AppRouting.getRequestRoute(
        this.forRecord.id,
        this.fromPublisherAccount.id,
      );
    } else if (this.type == AppNotificationType.dealCompletionMediaDetected &&
        this.forRecord != null) {
      route = AppRouting.getRequestCompleteRoute(
          this.forRecord.id, this.toPublisherAccount.id);
    } else if (this.type == AppNotificationType.message &&
        this.forRecord != null) {
      route = AppRouting.getRequestDialogRoute(
        this.forRecord.id,
        this.fromPublisherAccount.id,
      );
    }
  }

  String get occurredOnDisplay => Utils.formatAgo(occurredOn);
}
