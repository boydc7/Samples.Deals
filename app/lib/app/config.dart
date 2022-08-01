import 'dart:async';

import 'package:firebase_remote_config/firebase_remote_config.dart';
import 'package:flutter/foundation.dart';

enum AppFlavor {
  local,
  development,
  production,
}

class AppConfig {
  static const bool isReleaseBuild =
      const bool.fromEnvironment('dart.vm.product');

  final AppFlavor environment;
  final String apiHost;

  /// where the web/workspaces app is hosted at
  final String appHost;

  final String facebookAppClientId;
  final String facebookAppSecret;
  final String googleMapsApiKey;

  /// sentry.io account, where we send errors to
  final String sentryDsn;

  final bool enableDebug;

  int httpConnectTimeoutInMilliSeconds;
  int httpReceiveTimeoutInMilliSeconds;
  bool noCache;

  bool _initialized = false;
  RemoteConfig _remoteConfig;
  static AppConfig _instance;

  factory AppConfig({
    @required AppFlavor environment,
    @required apiHost,
    @required appHost,
    @required fbAppClientId,
    @required fbAppSecret,
    @required googleMapsKey,
    @required sentryDsn,
    int httpConnectTimeoutInMilliSeconds,
    int httpReceiveTimeoutInMilliSeconds,
    bool enableDebug = false,
    bool noCache = false,
  }) {
    _instance ??= AppConfig._internal(
      environment,
      apiHost,
      appHost,
      fbAppClientId,
      fbAppSecret,
      googleMapsKey,
      sentryDsn,
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
    this.appHost,
    this.facebookAppClientId,
    this.facebookAppSecret,
    this.googleMapsApiKey,
    this.sentryDsn,
    this.httpConnectTimeoutInMilliSeconds,
    this.httpReceiveTimeoutInMilliSeconds,
    this.enableDebug,
    this.noCache,
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
      /// NOTE: Printing instead of logging here as the Logger is likely not yet initialized
      print(
          'Could not fetch RemoteConfig - default values will be used - error [$x]');

      return;
    }

    var remoteNoCache = enableDebug
        ? _remoteConfig.getBool('debug_api_no_cache')
        : _remoteConfig.getBool('api_no_cache');

    noCache = noCache || remoteNoCache;

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
  static bool disableCache() => _instance.noCache ?? false;
}
