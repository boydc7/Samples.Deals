import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/services/device_settings.dart';

class AppAnalytics {
  static final AppAnalytics _singleton = AppAnalytics._internal();
  static AppAnalytics get instance => _singleton;

  FirebaseAnalytics _analytics;

  AppAnalytics._internal() {
    _analytics = FirebaseAnalytics();
  }

  /// format a path from routing into a screen name that we send to analytics for tracking
  /// depending on if we have a non-personal workspace and/or current profile we'll append this...
  String formatScreenName(String path) {
    if (appState.currentWorkspace != null &&
        appState.currentWorkspace.type != WorkspaceType.Personal) {
      return 'team/$path';
    } else if (appState.currentProfile != null) {
      return appState.currentProfile.isCreator
          ? 'creator/$path'
          : 'business/$path';
    } else {
      return path;
    }
  }

  /// convenience for returning route settings to when we're pushing onto the navigator manually
  /// and not from the generate route
  RouteSettings getRouteSettings(String path, [bool format = true]) =>
      RouteSettings(name: format ? formatScreenName(path) : path);

  void setUserProperty(String name, String value) =>
      _analytics..setUserProperty(name: name, value: value);

  /// track a screen manually from a response to a certain action on views that we want to track
  /// example would be on the connect facebook response where they did not agree to certain permissions
  void logScreen(String path, [format = true]) => _analytics.setCurrentScreen(
      screenName: format ? formatScreenName(path) : path);

  void logEvent(String name, [Map<String, dynamic> params]) =>
      _analytics.logEvent(
        name: name,
        parameters: params,
      );

  void logAppOpen() {
    _analytics.logAppOpen();

    DeviceSettings.incrementAppOpens();
  }
}
