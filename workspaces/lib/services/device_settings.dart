import 'dart:convert';
import 'dart:async';

import 'package:rydrworkspaces/models/device_settings.dart';
import 'package:rydrworkspaces/services/device_storage.dart';

class DeviceSettings {
  static final String _kUsageInfo = "usage_info";
  static final String _kDeviceInfo = "device_settings";

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
