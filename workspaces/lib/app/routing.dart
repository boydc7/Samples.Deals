import 'package:flutter/material.dart';
import 'package:logger/logger.dart';
import 'package:rydrworkspaces/app/log.dart';
import 'package:rydrworkspaces/models/route_data.dart';
import 'package:rydrworkspaces/ui/external/request.dart';
import 'package:rydrworkspaces/ui/main.dart';
import 'package:rydrworkspaces/app/string_extensions.dart';

const String getHome = '/';
const String getExternalRequest = '/xrequest';
const String getConnectPages = '/connect';
const String getLogin = '/login';

Route<dynamic> generateRoute(RouteSettings settings) {
  final Logger _log = getLogger('generateRoute');
  final RouteData routingData = settings.name.getRoutingData;

  _log.i(
      'path: ${routingData.route} queryParameters: ${routingData.queryParameters}');

  switch (routingData.route) {
    case getHome:
      return _getPageRoute(MainPage(), settings);
    case getExternalRequest:
      return _getPageRoute(
          RequestReport(routingData.queryParameters['id']), settings);
    default:
      return _getPageRoute(MainPage(), settings);
  }
}

PageRoute _getPageRoute(Widget child, RouteSettings settings) =>
    _FadeRoute(child: child, routeName: settings.name);

class _FadeRoute extends PageRouteBuilder {
  final Widget child;
  final String routeName;
  _FadeRoute({this.child, this.routeName})
      : super(
          settings: RouteSettings(name: routeName),
          pageBuilder: (
            BuildContext context,
            Animation<double> animation,
            Animation<double> secondaryAnimation,
          ) =>
              child,
          transitionsBuilder: (
            BuildContext context,
            Animation<double> animation,
            Animation<double> secondaryAnimation,
            Widget child,
          ) =>
              FadeTransition(
            opacity: animation,
            child: child,
          ),
        );
}
