import 'package:connectivity/connectivity.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:location_permissions/location_permissions.dart';

import 'package:rydr_app/services/event_bus.dart';

import 'package:rydr_app/models/device_settings.dart';

class AppEvents {
  static final AppEvents _singleton = AppEvents._internal();
  static AppEvents get instance => _singleton;

  EventBus eventBus;

  AppEvents._internal() {
    eventBus = EventBus();

    /// Listener for changes to the apps' lifecycle
    /// NOTE: we can only attache one of these, so this is the one and it will emit
    /// event change that others can listen to
    SystemChannels.lifecycle.setMessageHandler((msg) {
      if (msg == AppLifecycleState.resumed.toString()) {
        eventBus.fire(
          AppResumedEvent(true),
        );
      }

      return;
    });
  }
}

class AppResumedEvent {
  final bool resumed;

  AppResumedEvent(this.resumed);
}

class IOsSettingsRegisteredEvent {
  final IOsPushNotificationSettings settings;

  IOsSettingsRegisteredEvent(this.settings);
}

class LocationPermissionStatusChangeEvent {
  final PermissionStatus status;

  LocationPermissionStatusChangeEvent(this.status);
}

class ConnectivityChangeEvent {
  final ConnectivityResult result;

  ConnectivityChangeEvent(this.result);
}
