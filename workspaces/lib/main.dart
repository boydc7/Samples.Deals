import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app.dart';
import 'package:rydrworkspaces/app/config.dart';
import 'package:logger/logger.dart';

final GlobalKey<NavigatorState> navKey = GlobalKey();

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  const bool isReleaseBuild = bool.fromEnvironment('dart.vm.product');

  AppConfig(
    environment: isReleaseBuild ? AppFlavor.production : AppFlavor.development,
    apiHost: isReleaseBuild
        ? 'https://api.getrydr.com/'
        : 'http://apidev.getrydr.com/',
    fbAppClientId: isReleaseBuild ? '286022225400402' : '313209629397860',
    fbAppSecret: isReleaseBuild
        ? '304743b8646df114be42df8a7734b98c'
        : '9c3f7f602e8feac128bfa191b21d353f',
    googleMapsKey: isReleaseBuild
        ? 'AIzaSyAnK0y4IP1h6Orx6FsW6TZAf4F08NuMunQ'
        : 'AIzaSyAewrmaKekMmDpWVEwzi6AYY8Sbrvq7NdQ',
    enableDebug: !isReleaseBuild,
    noCache: false,
  );

  /// NOTE: this was set to await but then it won'tn load web
  AppConfig.instance.init();

  Logger.level = AppConfig.isProduction() ? Level.info : Level.verbose;

  runApp(RydrAdminApp());
}
