import 'dart:async';
import 'dart:io';

import 'package:device_info/device_info.dart';
import 'package:flutter/foundation.dart';
import 'package:logger/logger.dart';
import 'package:package_info/package_info.dart';
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/state.dart';
import 'package:sentry/sentry.dart';

class AppLogPrinter extends LogPrinter {
  final String className;

  AppLogPrinter(this.className);

  @override
  List<String> log(LogEvent event, [bool addUser = false]) {
    var color = PrettyPrinter.levelColors[event.level];
    var emoji = PrettyPrinter.levelEmojis[event.level];

    var profile = appState.currentProfile == null
        ? null
        : {
            'profile_id': appState.currentProfile.id,
            'profile_name': appState.currentProfile.userName,
          };

    return [
      color(addUser
              ? '$emoji[$className] -> ${event.message} $profile'
              : '$emoji[$className] -> ${event.message}')
          .toString()
    ];
  }
}

Logger getLogger(String className) {
  return Logger(printer: AppLogPrinter(className));
}

class AppErrorLogger {
  static final _log = getLogger('AppErrorLogger');
  static final AppErrorLogger _instance = AppErrorLogger._internal();
  static AppErrorLogger get instance => _instance;

  Future<SentryClient> get sentryClient async => SentryClient(
        dsn: AppConfig.instance.sentryDsn,
        environmentAttributes: await _environmentEvent,
      );

  bool _initialized = false;

  AppErrorLogger._internal() {
    /// Nothing to do
  }

  init() {
    if (_initialized) {
      return;
    }

    /// Turn off logging in production, on in debug
    Logger.level = AppConfig.isReleaseBuild ? Level.nothing : Level.verbose;

    /// This captures errors reported by the Flutter framework.
    FlutterError.onError = (FlutterErrorDetails details) async {
      if (AppConfig.isReleaseBuild) {
        reportError('Flutter', details.toString(), details.stack);
      } else {
        /// In development mode simply print to console.
        FlutterError.dumpErrorToConsole(details);
      }
    };

    _initialized = true;
  }

  /// Reports [error] along with its [stackTrace] to Sentry.io.
  Future<Null> reportError(
    String loggerName,
    dynamic error,
    dynamic stackTrace,
  ) async {
    _log.e('Caught error', error, stackTrace);

    /// Errors thrown in development mode are not interesting
    if (AppConfig.instance.environment != AppFlavor.production) {
      return;
    }

    _log.d('Setting Sentry.io user context and reporting error');

    final _sentry = await sentryClient;

    final SentryResponse response = await _sentry.capture(
      event: Event(
        loggerName: loggerName,
        exception: error,
        stackTrace: stackTrace,
      ),
    );

    if (response.isSuccessful) {
      _log.d('Success! Event ID: ${response.eventId}');
    } else {
      _log.e('Failed to report to Sentry.io: ${response.error}');
    }
  }
}

Future<Event> get _environmentEvent async {
  final packageInfo = await PackageInfo.fromPlatform();
  final deviceInfoPlugin = DeviceInfoPlugin();

  OperatingSystem os;
  Device device;

  if (Platform.isAndroid) {
    final androidInfo = await deviceInfoPlugin.androidInfo;
    os = OperatingSystem(
      name: 'android',
      version: androidInfo.version.release,
    );
    device = Device(
      model: androidInfo.model,
      manufacturer: androidInfo.manufacturer,
      modelId: androidInfo.product,
    );
  } else if (Platform.isIOS) {
    final iosInfo = await deviceInfoPlugin.iosInfo;
    os = OperatingSystem(
      name: iosInfo.systemName,
      version: iosInfo.systemVersion,
    );
    device = Device(
      model: iosInfo.utsname.machine,
      family: iosInfo.model,
      manufacturer: 'Apple',
    );
  }

  final environment = Event(
    release: '${packageInfo.version} (${packageInfo.buildNumber})',
    environment: AppConfig.instance.environment == AppFlavor.production
        ? 'production'
        : 'development',
    extra: {
      "tokenAcctId": appState?.masterUser?.accountId?.toString(),
      "workspaceId": appState?.currentWorkspace?.id?.toString(),
    },
    userContext: appState.currentProfile == null
        ? User(id: '0', username: 'null')
        : User(
            id: appState.currentProfile?.id.toString(),
            username: appState.currentProfile.userName,
          ),
    contexts: Contexts(
      operatingSystem: os,
      device: device,
      app: App(
        name: packageInfo.appName,
        version: packageInfo.version,
        build: packageInfo.buildNumber,
      ),
    ),
  );
  return environment;
}
