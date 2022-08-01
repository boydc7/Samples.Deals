import 'dart:async';

import 'package:shared_preferences/shared_preferences.dart';

class DeviceStorage {
  static Future<bool> deleteKey(String key) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    _storage.remove(key);
    return true;
  }

  static Future<String> getValue(String key) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    return _storage.getString(key);
  }

  static Future<int> getInt(String key) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    return _storage.getInt(key);
  }

  static Future<bool> getBool(String key) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    final val = _storage.getBool(key);

    return val == null ? false : val;
  }

  static Future<bool> setInt(String key, int value) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    _storage.setInt(key, value);
    return true;
  }

  static Future<bool> setBool(String key, bool value) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    _storage.setBool(key, value);
    return true;
  }

  static Future<bool> setValue(String key, String value) async {
    final SharedPreferences _storage = await SharedPreferences.getInstance();
    _storage.setString(key, value);
    return true;
  }

  static Future<String> getMessagingTokenHash() async =>
      await getValue("messaging_token_hash");

  static Future<bool> setMessagingTokenHash(String hash) async =>
      await setValue("messaging_token_hash", hash);

  static Future<bool> deleteMessagingTokenHash() async =>
      await deleteKey("messaging_token_hash");
}
