import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app.dart';
import 'package:rydrworkspaces/app/config.dart';
import 'package:logger/logger.dart';

final GlobalKey<NavigatorState> navKey = GlobalKey();

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  AppConfig(
    environment: AppFlavor.local,
    apiHost: 'http://localhost:2080/',
    fbAppClientId: '313209629397860',
    fbAppSecret: '9c3f7f602e8feac128bfa191b21d353f',
    googleMapsKey: 'AIzaSyAewrmaKekMmDpWVEwzi6AYY8Sbrvq7NdQ',
    enableDebug: true,
    noCache: false,
  );

  /// NOTE: this was set to await but then it won'tn load web
  AppConfig.instance.init();

  Logger.level = AppConfig.isProduction() ? Level.info : Level.verbose;

  runApp(RydrAdminApp());
}
