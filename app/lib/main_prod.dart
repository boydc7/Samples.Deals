import 'dart:async';
import 'package:flutter/services.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/log.dart';

import 'app.dart';
import 'app/routing.dart';
import 'app/config.dart';
import 'app/theme.dart';

/// Navigator key we pass down to notifications class
/// which will allow us to query for context on the navigator state
/// and thus have the ability to render widgets into the UI (like overlay)
final GlobalKey<NavigatorState> navKey = GlobalKey();

Future<Null> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  AppConfig(
    apiHost: 'https://api.getrydr.com/',
    appHost: 'https://app.getrydr.com/',
    environment: AppFlavor.production,
    fbAppClientId: '286022225400402',
    fbAppSecret: '304743b8646df114be42df8a7734b98c',
    googleMapsKey: 'AIzaSyAnK0y4IP1h6Orx6FsW6TZAf4F08NuMunQ',
    sentryDsn: 'https://284d5d952825476085da08b9d57691af@sentry.io/2709439',
    enableDebug: false,
    noCache: false,
  );

  await AppConfig.instance.init();
  AppErrorLogger.instance.init();

  /// only allow portrait mode, this works for android only
  /// for iOS this was changed in the xcode projects' -> general settings
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp])
      .then((_) {
    runZonedGuarded<Future<Null>>(() async {
      runApp(MaterialApp(
        debugShowCheckedModeBanner: false,
        color: Colors.white,
        title: "RYDR",
        theme: AppTheme().buildTheme(),
        darkTheme: AppTheme().buildDarkTheme(),
        navigatorKey: navKey,
        home: AppEntry(navKey),
        onGenerateRoute: AppRouting.getRoute,
      ));
    }, (error, stackTrace) async {
      try {
        await AppErrorLogger.instance.reportError('Zoned', error, stackTrace);
      } catch (e) {}
    });
  });
}
