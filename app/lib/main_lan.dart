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
    apiHost: 'http://192.168.86.27:2080/',
    appHost: 'http://localhost:5080/#/',
    environment: AppFlavor.local,
    fbAppClientId: '313209629397860',
    fbAppSecret: '9c3f7f602e8feac128bfa191b21d353f',
    googleMapsKey: 'AIzaSyAnK0y4IP1h6Orx6FsW6TZAf4F08NuMunQ',
    sentryDsn: 'https://284d5d952825476085da08b9d57691af@sentry.io/2709439',
    enableDebug: true,
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
