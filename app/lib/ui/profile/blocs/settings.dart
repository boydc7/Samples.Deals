import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/notifications.dart';

import 'package:rydr_app/models/device_settings.dart';

import 'package:rydr_app/app/events.dart';

class SettingsBloc {
  final showNotice = BehaviorSubject<bool>.seeded(false);

  StreamSubscription _subLocationPermission;

  SettingsBloc() {
    /// listen for current settings of push notifications on iOS device
    /// by listening to appEvents global singleton (eventbus) emitting IOsSettingsRegisteredEvent
    _subLocationPermission = AppEvents.instance.eventBus
        .on<IOsSettingsRegisteredEvent>()
        .listen((event) {
      onIosSettingsRegistered(event.settings);
    });

    /// initiate a check on first load
    appNotifications.iOSPermissionsPrompt(true);
  }

  dispose() {
    showNotice.close();
    _subLocationPermission?.cancel();
  }

  void onIosSettingsRegistered(IOsPushNotificationSettings settings) async {
    /// if push notifications are OFF then
    /// we show an indicator next to the notifications tile
    if (!settings.alert) {
      showNotice.add(true);
    } else if (showNotice.value == true) {
      showNotice.add(false);
    }
  }
}
