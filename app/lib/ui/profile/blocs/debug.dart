import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/models/device_settings.dart';
import 'package:rydr_app/services/device_settings.dart';

class ProfileDebugBloc {
  final _iOSPushNotifiactionSettings = BehaviorSubject<String>();
  final _onboardSettings = BehaviorSubject<String>();
  final _profileSettings = BehaviorSubject<String>();
  final _usageInfo = BehaviorSubject<String>();
  final _deviceInfo = BehaviorSubject<String>();

  ProfileDebugBloc() {
    this._loadIOSPushNotificationSettings();
    this._loadOnboadSettings();
    this._loadUsageInfo();
    this._loadDeviceInfo();
  }

  dispose() {
    _iOSPushNotifiactionSettings.close();
    _onboardSettings.close();
    _profileSettings.close();
    _usageInfo.close();
    _deviceInfo.close();
  }

  Stream<String> get iOSPushNotificationSettings =>
      _iOSPushNotifiactionSettings.stream;

  Stream<String> get onboardSettings => _onboardSettings.stream;
  Stream<String> get profileSettings => _profileSettings.stream;
  Stream<String> get usageInfo => _usageInfo.stream;
  Stream<String> get deviceInfo => _deviceInfo.stream;

  void _loadIOSPushNotificationSettings() async {
    final IOsPushNotificationSettings settings =
        await DeviceSettings.getIOsSettings();

    _iOSPushNotifiactionSettings.sink.add(settings.toString());
  }

  void _loadOnboadSettings() async {
    final OnboardSettings settings = await DeviceSettings.getOnboardSettings();

    _onboardSettings.sink.add(settings.toString());
  }

  void _loadUsageInfo() async {
    final UsageInfo settings = await DeviceSettings.getUsageInfo();

    _usageInfo.sink.add(settings.toString());
  }

  void _loadDeviceInfo() async {
    final DeviceInfo settings = await DeviceSettings.getDeviceInfo();

    _deviceInfo.sink.add(settings.toString());
  }

  void clearOnboardingSettings() async {
    await DeviceSettings.clearOnboarSettings();

    _loadOnboadSettings();
  }

  void clearIOsSettings() async {
    await DeviceSettings.clearIOsSettings();

    _loadIOSPushNotificationSettings();
  }

  void clearUsageInfo() async {
    await DeviceSettings.clearUsageInfo();

    _loadUsageInfo();
  }

  void clearDeviceInfo() async {
    await DeviceSettings.clearDeviceInfo();

    _loadDeviceInfo();
  }
}
