import 'dart:async';
import 'package:firebase_remote_config/firebase_remote_config.dart';
import 'package:flutter/foundation.dart';

enum AppFlavor {
  local,
  development,
  production,
}

class AppConfig {
  final AppFlavor environment;
  final String apiHost;
  final String facebookAppClientId;
  final String facebookAppSecret;
  final String googleMapsApiKey;
  final bool enableDebug;

  int httpConnectTimeoutInMilliSeconds;
  int httpReceiveTimeoutInMilliSeconds;
  bool _noCache;

  bool _initialized = false;
  RemoteConfig _remoteConfig;
  static AppConfig _instance;

  factory AppConfig({
    @required AppFlavor environment,
    @required apiHost,
    @required fbAppClientId,
    @required fbAppSecret,
    @required googleMapsKey,
    int httpConnectTimeoutInMilliSeconds,
    int httpReceiveTimeoutInMilliSeconds,
    bool enableDebug = false,
    bool noCache = false,
  }) {
    _instance ??= AppConfig._internal(
      environment,
      apiHost,
      fbAppClientId,
      fbAppSecret,
      googleMapsKey,
      httpConnectTimeoutInMilliSeconds ?? environment == AppFlavor.production
          ? 20000
          : 45000,
      httpReceiveTimeoutInMilliSeconds ?? environment == AppFlavor.production
          ? 60000
          : 120000,
      enableDebug,
      noCache,
    );

    return _instance;
  }

  AppConfig._internal(
    this.environment,
    this.apiHost,
    this.facebookAppClientId,
    this.facebookAppSecret,
    this.googleMapsApiKey,
    this.httpConnectTimeoutInMilliSeconds,
    this.httpReceiveTimeoutInMilliSeconds,
    this.enableDebug,
    this._noCache,
  );

  init() async {
    if (_initialized) {
      return;
    }

    _remoteConfig = await RemoteConfig.instance;

    _remoteConfig.setConfigSettings(
      RemoteConfigSettings(
        debugMode: debugEnabled(),
      ),
    );

    try {
      await _remoteConfig.fetch(
        expiration: const Duration(days: 30),
      );

      await _remoteConfig.activateFetched();
    } catch (x) {
      // NOTE: Printing instead of logging here as the Logger is likely not yet initialized
      print(
          'Could not fetch RemoteConfig - default values will be used - error [$x]');

      return;
    }

    _initialized = true;
  }

  static AppConfig get instance => _instance;

  Future<RemoteConfig> get remoteConfig async {
    if (!_initialized) {
      await init();
    }

    return _remoteConfig;
  }

  static bool isProduction() => _instance.environment == AppFlavor.production;
  static bool debugEnabled() => _instance.enableDebug ?? false;
  static bool noCache() => _instance._noCache ?? false;
}
