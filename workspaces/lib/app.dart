import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/login.dart';
import 'package:rydrworkspaces/main.dart';
import 'package:rydrworkspaces/redirect.dart';
import 'package:rydrworkspaces/services/auth_service.dart';

class RydrAdminApp extends StatefulWidget {
  @override
  _RydrAdminAppState createState() => _RydrAdminAppState();
}

class _RydrAdminAppState extends State<RydrAdminApp> {
  @override
  void initState() {
    super.initState();

    AuthService.instance.init();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      //showPerformanceOverlay: true,
      color: Colors.white,
      title: 'RYDR Workspaces',
      theme: AppTheme().buildTheme(),
      darkTheme: AppTheme().buildDarkTheme(),

      navigatorKey: navKey,
      initialRoute: '/',
      routes: {
        '/': (context) => RedirectPage(),
        '/auth': (context) => LoginPage(),
      },
    );
  }
}
