import 'package:rxdart/rxdart.dart';

import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/notification_settings.dart';
import 'package:rydr_app/models/responses/notifications.dart';
import 'package:rydr_app/services/notifications.dart';

class NotificationsBloc {
  final updatingType = BehaviorSubject<AppNotificationType>();
  final notificationSubscriptionResponse =
      BehaviorSubject<NotificationSubscriptionResponse>();

  dispose() {
    notificationSubscriptionResponse.close();
  }

  bool getSetting(AppNotificationType type) =>
      notificationSubscriptionResponse.value.models.firstWhere(
        (NotificationSetting s) => s.notificationType == type,
        orElse: () {
          return null;
        },
      )?.isSubscribed ??
      false;

  void saveSettings(bool isSubscribed, AppNotificationType type) {
    updatingType.add(type);

    if (isSubscribed) {
      NotificationService.setSubscriptionTo(
        type: type,
      );
    } else {
      NotificationService.deleteSubscriptionTo(
        type: type,
      );
    }

    AppAnalytics.instance.logScreen('profile/settings/notifications/updated');

    notificationSubscriptionResponse.value.models
        .firstWhere(
          (NotificationSetting setting) => setting.notificationType == type,
        )
        .isSubscribed = isSubscribed;

    updatingType.add(null);
  }

  void loadData() async {
    notificationSubscriptionResponse.sink.add(
      await NotificationService.getSubscriptions(),
    );
  }
}
