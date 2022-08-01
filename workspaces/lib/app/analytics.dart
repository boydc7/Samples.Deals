import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_analytics/observer.dart';
import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app/state.dart';
import 'package:rydrworkspaces/models/enums/workspace.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/workspace.dart';
import 'package:rydrworkspaces/services/device_settings.dart';

class AppAnalytics {
  static final AppAnalytics _singleton = AppAnalytics._internal();
  static AppAnalytics get instance => _singleton;

  FirebaseAnalytics _analytics;
  FirebaseAnalyticsObserver analyticsObserver;

  AppAnalytics._internal() {
    _analytics = FirebaseAnalytics();
    analyticsObserver = FirebaseAnalyticsObserver(analytics: _analytics);
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

    /// get the users personal workspace if available
    final Workspace personalWorkspace = appState.workspaces != null &&
            appState.workspaces
                .where((Workspace ws) => ws.type == WorkspaceType.Personal)
                .isNotEmpty
        ? appState.workspaces
            .firstWhere((Workspace ws) => ws.type == WorkspaceType.Personal)
        : null;

    /// is this device user an owner of a team workspace?
    final bool isWorkspaceOwner = appState.workspaces != null &&
        appState.workspaces
            .where((Workspace ws) =>
                ws.type == WorkspaceType.Team && ws.role == WorkspaceRole.Admin)
            .isNotEmpty;

    /// how many 'team' workspaces (not personal ones
    final int workspaceCount = appState.workspaces != null
        ? appState.workspaces
            .where((Workspace ws) => ws.type == WorkspaceType.Team)
            .length
        : 0;

    /// number of linked businesses in their personal workspace
    final int linkedBusinesses = personalWorkspace != null
        ? personalWorkspace.publisherAccountInfo
            .where((PublisherAccount account) => account.isBusiness)
            .length
        : 0;

    /// number of linked creators in their personal workspace
    final int linkedCreators = personalWorkspace != null
        ? personalWorkspace.publisherAccountInfo
            .where((PublisherAccount account) => account.isCreator)
            .length
        : 0;

    /// NOTE: should these be updated in places where they change?
    /// as of right now, these will be refreshed when the user opens the app only
    setUserProperty('linked_businesses', linkedBusinesses.toString());
    setUserProperty('linked_creators', linkedCreators.toString());
    setUserProperty('team_owner', isWorkspaceOwner.toString());
    setUserProperty('team_count', workspaceCount.toString());

    DeviceSettings.incrementAppOpens();
  }
}
