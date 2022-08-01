import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydrworkspaces/app/analytics.dart';
import 'package:rydrworkspaces/app/log.dart';
import 'package:rydrworkspaces/app/routing.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/enums/publisher_account.dart';
import 'package:rydrworkspaces/models/enums/workspace.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/responses/deal_metrics.dart';
import 'package:rydrworkspaces/models/responses/publisher_account_stats.dart';
import 'package:rydrworkspaces/models/workspace.dart';
import 'package:rydrworkspaces/models/workspace_features.dart';
import 'package:rydrworkspaces/services/deal.dart';
import 'package:rydrworkspaces/services/device_settings.dart';
import 'package:rydrworkspaces/services/publisher_account_stats.dart';

AppState appState = new AppState();

class AppState {
  static final AppState _appState = new AppState._internal();

  final _log = getLogger('AppState');
  final _currentProfileStats = BehaviorSubject<PublisherAccountStatsResponse>();
  final _currentProfileDealStats = BehaviorSubject<DealMetricsResponse>();
  final _currentProfileUnreadNotifications = BehaviorSubject<int>();

  final _updatedRequest = BehaviorSubject<DealRequestChange>();

  /// we add notification messages here and can listen to the stream
  /// on the dialog page for request messages
  StreamController messageStream = StreamController.broadcast();

  PublisherAccount masterUser;
  PublisherAccount currentProfile;
  Workspace currentWorkspace;
  List<Workspace> workspaces;

  bool _isConfigured = false;

  factory AppState() => _appState;

  void dispose() {
    _currentProfileStats.close();
    _currentProfileDealStats.close();
    _currentProfileUnreadNotifications.close();
    _updatedRequest.close();

    messageStream.close();
  }

  AppState._internal() {
    /// nothing to do
  }

  initialize() async {
    if (!_isConfigured) {
      _log.i('initialize');

      _isConfigured = true;
    }
  }

  /// stream that we can send a request to which was updated
  /// then we can subscribe from request list and handle changes to that request
  /// by either removing or otherwise updating the request
  Stream<DealRequestChange> get updatedRequest => _updatedRequest.stream;

  Stream<PublisherAccountStatsResponse> get currentProfileStats =>
      _currentProfileStats.stream;
  Stream<DealMetricsResponse> get currentProfileDealStats =>
      _currentProfileDealStats.stream;
  Stream<int> get currentProfileUnreadNotifications =>
      _currentProfileUnreadNotifications.stream;

  bool isAiAvailable(PublisherAccount account) {
    /// if the current profile is a creator and they are viewing themselves
    /// then return back the flag that indicates if they have enabled or not
    if (currentProfile.isCreator && account.id == currentProfile.id) {
      return account.optInToAi;
    }

    /// if we have a business viewing their own profile then they must
    /// have a paid subscription and we'll return their opt in setting, otherwise its always false
    if (currentProfile.isBusiness && account.id == currentProfile.id) {
      if (currentProfile.subscriptionType != SubscriptionType.None) {
        return account.optInToAi;
      }

      return false;
    }

    /// if we have a business viewing a creator, then first the business must have a paid subscription
    /// and then we'll return back their current profile opt-in setting, otherwise is always false
    if (currentProfile.isBusiness && account.isCreator) {
      if (currentProfile.subscriptionType != SubscriptionType.None) {
        return account.optInToAi;
      }

      return false;
    }

    return false;
  }

  bool hasTeamsEnabled() {
    if (workspaces == null || workspaces.isEmpty) {
      return false;
    }

    /// if this master user already has a 'team' workspace then we can't take it away from them here
    /// so that would override the features enabled on their personal workspace and we'd say they have 'teams' feature
    if (workspaces
            .where((Workspace ws) => ws.type == WorkspaceType.Team)
            .length >
        0) {
      return true;
    }

    /// if they don't already have a teams account, then check the features on their personal workspace
    return WorkspaceFeatures.hasTeams(workspaces
        .firstWhere((Workspace ws) => ws.type == WorkspaceType.Personal,
            orElse: () => null)
        ?.workspaceFeatures);
  }

  Future<bool> switchWorkspace(Workspace workspace) async {
    if (this.workspaces.where((Workspace ws) => ws.id == workspace.id).length ==
        0) {
      _log.w('switchWorkspace | ${workspace.name} not found');
      return false;
    }

    /// update workspace
    this.currentProfile = null;
    this.currentWorkspace = workspace;

    /// update device info with new current profile and workspace settings
    await DeviceSettings.saveDeviceInfoActiveProfile(
        this.currentWorkspace.id, null);

    /// clear streams
    _currentProfileStats.sink.add(null);
    _currentProfileDealStats.sink.add(null);
    _currentProfileUnreadNotifications.sink.add(null);

    return true;
  }

  /// switch to the requested user, meaning we find the desired user in either the currently active or any workspace
  /// then update the current user to the newly requested user and possibly workspace. Returns false if we don't find them
  Future<bool> switchProfile(int profileId, [int workspaceId]) async {
    Workspace newCurrentWorkspace;
    PublisherAccount newCurrentProfile;

    /// if we're passing a workspaceid, then find the profile in the requested workspace
    /// which may be different than the active one
    if (workspaceId != null) {
      newCurrentWorkspace = appState.workspaces.firstWhere(
          (Workspace ws) => ws.id == workspaceId,
          orElse: () => null);

      if (newCurrentWorkspace == null) {
        _log.w('switchProfile | $workspaceId not found');
        return false;
      }

      newCurrentProfile = newCurrentWorkspace.publisherAccountInfo
          .firstWhere((PublisherAccount u) => u.id == profileId, orElse: () {
        return null;
      });
    } else {
      newCurrentWorkspace = this.currentWorkspace;

      newCurrentProfile = currentWorkspace.publisherAccountInfo
          .firstWhere((PublisherAccount u) => u.id == profileId, orElse: () {
        return null;
      });
    }

    if (newCurrentProfile == null) {
      _log.w('switchProfile | $profileId not found in workspace $workspaceId');
      return false;
    }

    /// update the current workspace and profile on the app state
    this.currentWorkspace = newCurrentWorkspace;
    this.currentProfile = newCurrentProfile;

    /// load stats for the currently active profile
    await loadProfileStats();

    /// update unread notifications on stream
    _currentProfileUnreadNotifications.sink
        .add(newCurrentProfile.unreadNotifications);

    /// update device info with new current profile and workspace settings
    await DeviceSettings.saveDeviceInfoActiveProfile(
        this.currentWorkspace.id, this.currentProfile.id);

    _log.i('switchProfile | ${currentProfile.userName}');

    return true;
  }

  /// adding a new user will add it to existing list of users
  /// make it the current user, and set it as the current in device prefs
  Future<void> addUser(PublisherAccount profile) async {
    this.currentWorkspace.publisherAccountInfo == null
        ? this.currentWorkspace.publisherAccountInfo = [profile]
        : this.currentWorkspace.publisherAccountInfo.add(profile);

    /// switch to new user
    this.switchProfile(profile.id);
  }

  /// remove as current profile, remove from current workspace, get last one if we have
  /// any profiles left after and set it as the current one, otherwise clear state
  Future<bool> removeCurrentProfile() async {
    this
        .currentWorkspace
        .publisherAccountInfo
        .removeWhere((PublisherAccount u) => u.id == this.currentProfile.id);

    if (this.currentWorkspace.publisherAccountInfo.length > 0) {
      ///  we have user(s) left, so make the next one in line the current one
      this.currentProfile = this.currentWorkspace.publisherAccountInfo[0];

      /// update currently active workspace and active profile id
      await DeviceSettings.saveDeviceInfoActiveProfile(
          this.currentWorkspace.id, this.currentProfile.id);

      return true;
    } else {
      this.currentWorkspace.publisherAccountInfo = [];
      this.currentProfile = null;

      /// update currently active workspace and NO profile id
      await DeviceSettings.saveDeviceInfoActiveProfile(
          this.currentWorkspace.id, null);

      /// we have no other users left, return false
      return false;
    }
  }

  Future<void> loadProfileStats() async {
    /// load current users pub account stats which will give us things like
    /// current pending, invites, number of active deals, etc. we use this to display
    /// static notifications as well as other places to showcase action items
    _currentProfileStats.sink
        .add(await PublisherAccountStatsService.getAccountStats());

    /// load current users deal completion stats (applicable currently to business accounts only)
    /// which would give us full completion stats for their entire business + deal completion insights
    if (currentProfile.isBusiness) {
      _currentProfileDealStats.sink.add(await DealService.getDealMetrics());
    }
  }

  /// we can call this to get the 'home' route for where to send a profile to once
  /// they've either linked or switched to it, or even when the app first starts again
  String getProfileHomeRoute() {
    String route;

    if (appState.masterUser == null) {
      /// no master user, then we have to have the user go and connect their facebook
      route = getLogin;
    } else if (appState.currentProfile == null) {
      /// no current profile yet, then they have to go and choose one
      route = getConnectPages;
    } else {
      /// business either finishes onboarding or goes to their home page
      route = getHome;
    }

    return route;
  }

  void handleRequestStatusChange(Deal deal, DealRequestStatus toStatus) {
    final bool isBusiness = currentProfile.isBusiness;

    /// if we don't have a deal request yet then the from status is null
    final DealRequestStatus fromStatus =
        deal.request == null ? null : deal.request.status;

    /// update the status on the request of this deal/request
    /// then update the deal on the stream which we can listen to for changes
    _updatedRequest.sink.add(DealRequestChange(
      fromStatus,
      toStatus,
      deal,
    ));

    /// if a request is made from a creator then increment the current requested
    /// while this mostly applies to a business we still update it regardless
    if (toStatus == DealRequestStatus.requested) {
      _updateAccountStat(DealStatType.currentRequested);

      AppAnalytics.instance.logScreen('request/requested');
      return;
    }

    /// if an invite is accepted by a creator, then decrement their invite count
    /// and increment their current approved (in progress) if its a business then
    /// decrement their requested count instead
    if (toStatus == DealRequestStatus.inProgress) {
      _updateAccountStat(
          isBusiness
              ? DealStatType.currentRequested
              : DealStatType.currentInvites,
          true);
      _updateAccountStat(DealStatType.currentApproved);

      AppAnalytics.instance
          .logScreen(isBusiness ? 'request/apprpoved' : 'request/accepted');
      return;
    }

    /// if an invte is declined from a creator, then decrement their invite count
    /// if its a request declined by the business then decrement their requested count
    if (toStatus == DealRequestStatus.denied) {
      _updateAccountStat(
          currentProfile.isBusiness
              ? DealStatType.currentRequested
              : DealStatType.currentInvites,
          true);

      AppAnalytics.instance.logScreen('request/declined');
      return;
    }

    /// if a request is now marked redeemed we decrement current approved and increment redeemed
    /// this applies to either/both business or creators
    if (toStatus == DealRequestStatus.redeemed) {
      _updateAccountStat(DealStatType.currentApproved, true);
      _updateAccountStat(DealStatType.currentRedeemed);

      AppAnalytics.instance.logScreen('request/redeemed');
      return;
    }

    /// if cancelled then, depending on what the prior status was either decrement
    /// redeemed or in progress, or invites, or requested
    if (toStatus == DealRequestStatus.cancelled) {
      if (fromStatus == DealRequestStatus.inProgress) {
        _updateAccountStat(DealStatType.currentApproved, true);
      } else if (fromStatus == DealRequestStatus.invited) {
        _updateAccountStat(DealStatType.currentInvites, true);
      } else if (fromStatus == DealRequestStatus.redeemed) {
        _updateAccountStat(DealStatType.currentRedeemed, true);
      } else if (fromStatus == DealRequestStatus.requested) {
        _updateAccountStat(DealStatType.currentRequested, true);
      }

      AppAnalytics.instance.logScreen('request/cancelled');
      return;
    }

    /// if marked delinquent then, depending on prior status either decrement
    /// the in progress, or redeemed counter as well as increment delinquent
    if (toStatus == DealRequestStatus.delinquent) {
      _updateAccountStat(DealStatType.currentDelinquent);

      if (fromStatus == DealRequestStatus.inProgress) {
        _updateAccountStat(DealStatType.currentApproved, true);
      } else if (fromStatus == DealRequestStatus.redeemed) {
        _updateAccountStat(DealStatType.currentRedeemed, true);
      }

      AppAnalytics.instance.logScreen('request/delinquent');
      return;
    }

    /// if marked completed then, depending on the prior status we eithere decrement
    /// approved or redeemed, and increment current completed count
    if (toStatus == DealRequestStatus.completed) {
      _updateAccountStat(DealStatType.currentCompleted);

      if (fromStatus == DealRequestStatus.inProgress) {
        _updateAccountStat(DealStatType.currentApproved, true);
      } else if (fromStatus == DealRequestStatus.redeemed) {
        _updateAccountStat(DealStatType.currentRedeemed, true);
      }

      AppAnalytics.instance.logScreen('request/completed');
      return;
    }
  }

  void _updateAccountStat(DealStatType type, [bool decrement = false]) {
    try {
      final PublisherAccountStatsResponse res = _currentProfileStats.value;
      final int currentValue = res.accountStats.dealRequestStats[type];

      if (decrement) {
        res.accountStats.dealRequestStats[type] = currentValue - 1;
      } else {
        res.accountStats.dealRequestStats[type] = currentValue + 1;
      }
    } catch (ex) {
      _log.e('updateAccountStat', ex);
    }
  }

  void updateUnreadNotificationsCount([bool decrement = false]) {
    try {
      final int currentCount = _currentProfileUnreadNotifications.value;
      if (decrement) {
        _currentProfileUnreadNotifications.sink.add(currentCount - 1);
      } else {
        _currentProfileUnreadNotifications.sink.add(currentCount + 1);
      }
    } catch (ex) {
      _log.e('updateUnreadNotificationsCount', ex);
    } finally {
      appState.currentProfile.updateUnreadNotificationsCount(decrement);
    }
  }

  void clearNotificationsCount() {
    _currentProfileUnreadNotifications.sink.add(0);
    appState.currentProfile.unreadNotifications = 0;
  }

  /// this is called when the user logs out of their master user (facebook login)
  /// we simply clear the app state by setting current profile & workspace, master user, and list of workspaces to null
  void signOut() {
    this.currentProfile = null;
    this.currentWorkspace = null;
    this.masterUser = null;
    this.workspaces = null;
  }
}

class DealRequestChange {
  final DealRequestStatus fromStatus;
  final DealRequestStatus toStatus;
  final Deal deal;

  DealRequestChange(
    this.fromStatus,
    this.toStatus,
    this.deal,
  );
}
