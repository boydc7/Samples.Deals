import 'package:rydr_app/models/enums/notification.dart';

class NotificationSetting {
  AppNotificationType notificationType;
  bool isSubscribed;

  NotificationSetting.fromJson(Map<String, dynamic> json) {
    this.notificationType =
        appNotificationTypeFromString(json['notificationType']);
    this.isSubscribed = json['isSubscribed'];
  }
}
