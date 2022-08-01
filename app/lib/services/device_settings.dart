import 'dart:convert';
import 'dart:async';

import 'package:rydr_app/models/device_settings.dart';

import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/services/device_storage.dart';

class DeviceSettings {
  static final String _kIOsPushNotificationSettings = "ios_push";
  static final String _kOnboardSettings = "onboard_settings";
  static final String _kProfileSettings = "profile_settings_";
  static final String _kUsageInfo = "usage_info";
  static final String _kDeviceInfo = "device_settings";

  static Future<IOsPushNotificationSettings> getIOsSettings() async {
    final String settings =
        await DeviceStorage.getValue(_kIOsPushNotificationSettings);

    return settings == null
        ? null
        : IOsPushNotificationSettings.fromJson(json.decode(settings));
  }

  static void saveIOsSettings(IOsPushNotificationSettings settings) {
    settings.lastChecked = DateTime.now().toUtc();

    DeviceStorage.setValue(
        _kIOsPushNotificationSettings, json.encode(settings.toJson()));
  }

  static void saveIOsSettingsDismissed(bool dismiss) async {
    final String val =
        await DeviceStorage.getValue(_kIOsPushNotificationSettings);

    if (val != null) {
      IOsPushNotificationSettings settings =
          IOsPushNotificationSettings.fromJson(json.decode(val));
      settings.dismissed = dismiss;

      saveIOsSettings(settings);
    }
  }

  static Future<bool> clearIOsSettings() =>
      DeviceStorage.deleteKey(_kIOsPushNotificationSettings);

  static Future<OnboardSettings> getOnboardSettings() async {
    final String settings = await DeviceStorage.getValue(_kOnboardSettings);

    return settings == null
        ? OnboardSettings()
        : OnboardSettings.fromJson(json.decode(settings));
  }

  static void saveOnboardSettings(OnboardSettings settings) {
    DeviceStorage.setValue(_kOnboardSettings, json.encode(settings.toJson()));

    /// update state with settings
    appState.onboardSettings = settings;
  }

  static Future<bool> clearOnboarSettings() =>
      DeviceStorage.deleteKey(_kOnboardSettings);

  static Future<bool> clearProfileSettings(int profileId) =>
      DeviceStorage.deleteKey('$_kProfileSettings$profileId');

  static Future<void> incrementAppOpens() async {
    final UsageInfo settings = await getUsageInfo();

    settings.opened += 1;

    _saveUsageInfo(settings);
  }

  static Future<DeviceInfo> getDeviceInfo() async {
    final String settings = await DeviceStorage.getValue(_kDeviceInfo);

    return settings == null
        ? DeviceInfo()
        : DeviceInfo.fromJson(json.decode(settings));
  }

  static Future<void> saveDeviceInfoActiveProfile(
      int activeWorkspaceId, int activeProfileId) async {
    final DeviceInfo currentInfo = await getDeviceInfo();

    currentInfo.activeWorkspaceId = activeWorkspaceId;
    currentInfo.activeProfileId = activeProfileId;

    await saveDeviceInfo(currentInfo);
  }

  static Future<void> saveDeviceInfo(DeviceInfo settings) async {
    await DeviceStorage.setValue(_kDeviceInfo, json.encode(settings.toJson()));
  }

  static Future<bool> clearDeviceInfo() =>
      DeviceStorage.deleteKey(_kDeviceInfo);

  static Future<bool> clearUsageInfo() => DeviceStorage.deleteKey(_kUsageInfo);

  static Future<UsageInfo> getUsageInfo() async {
    final String settings = await DeviceStorage.getValue(_kUsageInfo);
    return settings == null
        ? UsageInfo()
        : UsageInfo.fromJson(json.decode(settings));
  }

  static void _saveUsageInfo(UsageInfo settings) =>
      DeviceStorage.setValue(_kUsageInfo, json.encode(settings.toJson()));
}
