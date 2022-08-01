import 'dart:async';

import 'package:connectivity/connectivity.dart';
import 'package:firebase_dynamic_links/firebase_dynamic_links.dart';
import 'package:flutter/material.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/events.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/map_config.dart';
import 'package:rydr_app/app/notifications.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/tags_config.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_request.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/deal_metrics.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/device_settings.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/models/workspace_features.dart';
import 'package:rydr_app/services/deal_metrics.dart';

import 'package:rydr_app/services/device_settings.dart';
import 'package:rydr_app/services/publisher_account_stats.dart';
import 'package:rydr_app/services/workspaces.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

AppState appState = new AppState();

class AppState {
  static final AppState _appState = new AppState._internal();

  final _log = getLogger('AppState');
  final _hasInternet = BehaviorSubject<bool>();
  final _currentProfileStats = BehaviorSubject<PublisherAccountStatsResponse>();
  final _currentProfileDealStats = BehaviorSubject<DealMetricsResponse>();
  final _currentProfileUnreadNotifications = BehaviorSubject<int>();
  final _currentProfileOptInToAi = BehaviorSubject<bool>();

  final _updatedRequest = BehaviorSubject<DealRequestChange>();

  StreamSubscription _onConnectivityChangeFromResume;
  StreamSubscription _onConnectivityChange;

  /// we add notification messages here and can listen to the stream
  /// on the dialog page for request messages
  StreamController messageStream = StreamController.broadcast();

  PublisherAccount _masterUser;
  PublisherAccount _currentProfile;
  Workspace _currentWorkspace;
  List<Workspace> _workspaces;

  PublisherAccount get masterUser => _masterUser;
  PublisherAccount get currentProfile => _currentProfile;
  Workspace get currentWorkspace => _currentWorkspace;
  List<Workspace> get workspaces => _workspaces;

  void setMasterUser(PublisherAccount u) => _masterUser = u;
  void setCurrentProfile(PublisherAccount p) => _currentProfile = p;
  void setCurrentWorkspace(Workspace w) => _currentWorkspace = w;
  void setWorkspaces(List<Workspace> ws) => _workspaces = ws;

  bool _isConfigured = false;

  /// if we have a deep link when opening the app then store it here
  String _deepLink;

  /// we get these once pubaccounts load, and the other once we have a current user
  OnboardSettings onboardSettings;

  /// last recorded location of master user
  PlaceLatLng lastLatLng;

  GlobalKey<NavigatorState> _navKey;

  factory AppState() => _appState;

  void dispose() {
    _hasInternet.close();
    _currentProfileStats.close();
    _currentProfileDealStats.close();
    _currentProfileUnreadNotifications.close();
    _currentProfileOptInToAi.close();
    _updatedRequest.close();

    _onConnectivityChangeFromResume?.cancel();
    _onConnectivityChange?.cancel();

    messageStream.close();
  }

  AppState._internal() {
    /// nothing to do
  }

  initialize(GlobalKey<NavigatorState> navKey) async {
    if (!_isConfigured) {
      _log.i('initialize');

      _isConfigured = true;
      _navKey = navKey;

      /// get initial onboard settings
      onboardSettings = await DeviceSettings.getOnboardSettings();

      /// initial check for internet connectivity
      setHasInternet(await Connectivity().checkConnectivity());

      /// init dynamic links which would grab initial and any future deeplinks
      /// for when the app might resume from the background via deep link
      initDynamicLinks();

      /// load map & tags configuration
      await mapConfig.initValues();
      await tagsConfig.initValues();

      /// setup notifications now that we're guaranteed to have appstate (with or without users)
      /// appNotifications is a singleton so we are safe to call this multiple times in case
      /// there was a timeout or otherwise an issue with loading the master users and needing a retry
      appNotifications.initialize(navKey);

      /// log app open with analytics, this will also increment the "app opens"
      /// device setting for the given fb account on the device and set additional user properties in analytics
      AppAnalytics.instance.logAppOpen();

      _onConnectivityChangeFromResume = AppEvents.instance.eventBus
          .on<ConnectivityChangeEvent>()
          .listen((ConnectivityChangeEvent evt) => setHasInternet(evt.result));

      _onConnectivityChange =
          Connectivity().onConnectivityChanged.listen(setHasInternet);
    }
  }

  String get deepLink => _deepLink;

  bool get hasInternet => _hasInternet.stream.value ?? true;
  void setHasInternet(ConnectivityResult res) => _hasInternet.sink
      .add(res == ConnectivityResult.mobile || res == ConnectivityResult.wifi);

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

  BehaviorSubject<bool> get currentProfileOptInToAi =>
      _currentProfileOptInToAi.stream;
  void setCurrentProfileOptInToAi(bool val) {
    _currentProfile.optInToAi = val;
    _currentProfileOptInToAi.sink.add(val);
  }

  bool get isBusinessPro =>
      this.currentProfile != null &&
      this.currentProfile.isBusiness &&
      this.currentProfile.subscriptionType != SubscriptionType.None;

  bool get hasAnyProfilesOnDevice =>
      _workspaces != null &&
      _workspaces.isNotEmpty &&
      _workspaces.any((Workspace ws) => ws.hasLinkedPublishers == true);

  bool isAiAvailable(PublisherAccount account) {
    /// if the current profile is a creator and they are viewing themselves
    /// then return back the flag that indicates if they have enabled or not
    if (_currentProfile.isCreator && account.id == _currentProfile.id) {
      return account.optInToAi;
    }

    /// if we have a business viewing their own profile then they must
    /// have a paid subscription and we'll return their opt in setting, otherwise its always false
    if (isBusinessPro && account.id == _currentProfile.id) {
      return account.optInToAi;
    }

    /// if we have a business viewing a creator, then first the business must have a paid subscription
    /// and then we'll return back their current profile opt-in setting, otherwise is always false
    if (isBusinessPro && account.isCreator) {
      return account.optInToAi;
    }

    return false;
  }

  /// check the currentDelinquent count from the profiles' content stats
  /// and if we have more than the profile is allotted to have then return false
  /// we can use this flag for when the app loads or we switch profiles to redirect to a roadblock
  /// NOTE: this depends on content stats being synchronously loaded when we do the switchProfile call
  bool get creatorIsDelinquent => _currentProfileStats.value != null &&
          _currentProfileStats.value.model != null
      ? _currentProfileStats.value.model
              .tryGetDealStatValue(DealStatType.currentDelinquent) >
          _currentProfile.maxDelinquent
      : false;

  bool hasTeamsEnabled() {
    if (_workspaces == null || _workspaces.isEmpty) {
      return false;
    }

    /// if this master user already has a 'team' workspace then we can't take it away from them here
    /// so that would override the features enabled on their personal workspace and we'd say they have 'teams' feature
    if (_workspaces
            .where((Workspace ws) => ws.type == WorkspaceType.Team)
            .length >
        0) {
      return true;
    }

    /// if they don't already have a teams account, then check the features on their personal workspace
    return WorkspaceFeatures.hasTeams(_workspaces
        .firstWhere((Workspace ws) => ws.type == WorkspaceType.Personal,
            orElse: () => null)
        ?.workspaceFeatures);
  }

  /// update the last known location of the user which we'll get
  /// from the map if they have location service turned on
  void setLastLocation(PlaceLatLng latLng) {
    _log.d('setLastLocation | $latLng');

    lastLatLng = latLng;
  }

  /// switches appstate to the desired workspace, we will at this point have
  /// validated that the workspace is valid for the given user and device
  Future<bool> switchWorkspace(Workspace workspace) async {
    /// null-out current profile, then update current workspace
    _currentProfile = null;
    _currentWorkspace = workspace;

    /// update device info with new current profile and workspace settings
    await DeviceSettings.saveDeviceInfoActiveProfile(
        _currentWorkspace.id, null);

    /// clear streams
    _currentProfileStats.sink.add(null);
    _currentProfileDealStats.sink.add(null);
    _currentProfileUnreadNotifications.sink.add(null);

    return true;
  }

  /// switch to the requested user, meaning we find the desired user in either the currently active or any workspace
  /// then update the current user to the newly requested user and possibly workspace. Returns false if we don't find them
  Future<bool> switchProfile(int profileId, [int workspaceId]) async {
    PublisherAccount newCurrentProfile;

    /// if we're passing a workspaceid, then load a fresh workspace response and
    /// next try to load the profile in the workspace to ensure its still linked there
    if (workspaceId != null) {
      /// Make a copy of the current workspace so we can switch back to it
      /// in case we fail to make the switch here...
      final Map<String, dynamic> origWorkspace = _currentWorkspace.toJson();

      final WorkspaceResponse workspaceResponse =
          await WorkspacesService.getWorkspace(workspaceId, true);

      if (workspaceResponse.hasError) {
        _log.w('switchProfile | $workspaceId not found');
        return false;
      }

      /// set the new workspace in state so we use the right header in
      /// futher api calls to check if the profile is linked
      _currentWorkspace = workspaceResponse.model;

      /// try to load the requested profile for the given workspace
      newCurrentProfile = await WorkspacesService.getWorkspacePublisherAccount(
          _currentWorkspace.id, profileId);

      /// if unsuccessful, then switch back to the original workspace
      if (newCurrentProfile == null) {
        _currentWorkspace = Workspace.fromJson(origWorkspace);
      }
    } else {
      /// load the profile for the current workspace to ensure its still valid,
      /// we're linked to it, and can reliable access / switch to it
      newCurrentProfile = await WorkspacesService.getWorkspacePublisherAccount(
          _currentWorkspace.id, profileId);
    }

    if (newCurrentProfile == null) {
      _log.w('switchProfile | $profileId not found in workspace $workspaceId');
      return false;
    }

    /// update the current workspace and profile on the app state
    _currentProfile = newCurrentProfile;

    /// load stats for the currently active profile
    await loadProfileStats();

    /// update unread notifications and ai enabled flag on stream
    _currentProfileUnreadNotifications.sink
        .add(newCurrentProfile.unreadNotifications);
    _currentProfileOptInToAi.sink.add(newCurrentProfile.optInToAi);

    /// update device info with new current profile and workspace settings
    await DeviceSettings.saveDeviceInfoActiveProfile(
        _currentWorkspace.id, _currentProfile.id);

    _log.i('switchProfile | ${_currentProfile.userName}');

    return true;
  }

  /// this is called after we've removed a profile from a given workspace
  /// we reload the current workspace and then determine if there are any left and
  /// either switch to the next one in line or have none to switch to
  Future<bool> removeCurrentProfile() async {
    /// set current profile to null so we won't use it in the header
    /// for the upcoming api call to refresh the current workspace
    _currentProfile = null;

    final WorkspaceResponse workspaceResponse =
        await WorkspacesService.getWorkspace(_currentWorkspace.id);

    /// if there was an error, or we have no more profiles left in the workspace
    /// then return false, which will trigger the caller to send the user to the connect/pages ui
    if (workspaceResponse.hasError ||
        workspaceResponse.model == null ||
        workspaceResponse.model.hasLinkedPublishers == false) {
      _currentWorkspace.publisherAccountInfo = [];

      /// update currently active workspace and NO profile id
      await DeviceSettings.saveDeviceInfoActiveProfile(
          _currentWorkspace.id, null);

      /// we have no other users left, return false
      return false;
    }

    /// no error, and we still have linked profiles, so set the workspace in app state
    /// and pick the latest from the up-to-3 linked pub accounts returned
    _currentWorkspace = workspaceResponse.model;

    /// we have user(s) left, so make the next one in line the current one
    _currentProfile = _currentWorkspace.publisherAccountInfo[0];

    /// update currently active workspace and active profile id
    await DeviceSettings.saveDeviceInfoActiveProfile(
        _currentWorkspace.id, _currentProfile.id);

    return true;
  }

  /// load current users pub account stats which will give us things like
  /// current pending, invites, number of active deals, etc. we use this to display
  /// static notifications as well as other places to showcase action items
  Future<void> loadProfileStats() async {
    _currentProfileStats.sink
        .add(await PublisherAccountStatsService.getAccountStats());

    /// load current users deal completion stats (applicable currently to business accounts only)
    /// which would give us full completion stats for their entire business + deal completion insights
    if (_currentProfile.isBusiness) {
      _currentProfileDealStats.sink
          .add(await DealMetricsService.getDealMetrics(forceRefresh: true));
    }
  }

  /// we can call this to get the 'home' route for where to send a profile to once
  /// they've either linked or switched to it, or even when the app first starts again
  String getInitialRoute() {
    String route;

    if (_masterUser == null) {
      /// no master user, so we'll have to send them back to the login
      route = AppRouting.getAuthenticate;
    } else if (_currentProfile == null) {
      /// if we have no profile - BUT - we have a valid workspace
      /// then send the user to the pages ui, otherwise we have nothing
      /// linked and/or no workspace so they first need to connect something
      if (_currentWorkspace == null) {
        route = AppRouting.getConnectProfile;
      } else {
        route = AppRouting.getConnectPages;
      }
    } else if (_currentProfile.isCreator) {
      /// if a creator is delinquent, then we roadblock them
      /// otherwise, check if they've onboarded and eitehr make them finish or go to the map
      route = creatorIsDelinquent
          ? AppRouting.getProfileDelinquent
          : onboardSettings.doneAsCreator
              ? AppRouting.getDealsMap
              : AppRouting.getConnectOnboard;
    } else {
      /// business either finishes onboarding or goes to their home page
      route = onboardSettings.doneAsBusiness
          ? AppRouting.getHome
          : AppRouting.getConnectOnboard;
    }

    return route;
  }

  void handleRequestStatusChange(Deal deal, DealRequestStatus toStatus) {
    final bool isBusiness = _currentProfile.isBusiness;

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
          _currentProfile.isBusiness
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

      if (res.model.dealRequestStats[type] != null) {
        /// TODO: seems there's an error here still at time
        final int currentValue = res.model.dealRequestStats[type];

        if (decrement) {
          res.model.dealRequestStats[type] = currentValue - 1;
        } else {
          res.model.dealRequestStats[type] = currentValue + 1;
        }
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
      _currentProfile.updateUnreadNotificationsCount(decrement);
    }
  }

  void clearNotificationsCount() {
    _currentProfileUnreadNotifications.sink.add(0);
    _currentProfile.unreadNotifications = 0;
  }

  /// this is called when the user logs out of their master user (facebook login)
  /// we simply clear the app state by setting current profile & workspace, master user, and list of workspaces to null
  void signOut() {
    _currentProfile = null;
    _currentWorkspace = null;
    _masterUser = null;
    _workspaces = null;
  }

  /// initialize firebase dynamic (deep) link functionality which will both attempt to get/handle
  /// an incoming link from / after app launch, as well as add a listener for when the app resumes
  void initDynamicLinks() async {
    final PendingDynamicLinkData data =
        await FirebaseDynamicLinks.instance.getInitialLink();
    final Uri deepLink = data?.link;

    if (deepLink != null) {
      _log.i('initDynamicLinks | ${deepLink.path}');

      /// on app start we don't handle deep link directly but rather store it
      _deepLink = deepLink.path;

      /// should we store the deep link in some array list
      /// of shared preferences, we could then process/send it/them along
      /// with things like 'first connect' and any pub-account linking
      /// which could be used as attributing initial install, linking, etc. to a referrer?
      ///
      /// My assumption here is that at this stage it would be considered
      /// a new install and we'd want to store the deep link referral id or
      /// something to that affect, from the url
      ///
      /// we should have the master users' account id in each generated 'share' link?
    }

    FirebaseDynamicLinks.instance.onLink(
        onSuccess: (PendingDynamicLinkData dynamicLink) async {
          final Uri deepLink = dynamicLink?.link;

          if (deepLink != null) {
            _log.i('onLink | ${deepLink.path}');

            /// store deep link in local property before calling function to process
            /// and navigate or show a snackbar if user is not able to navigate to it
            _deepLink = deepLink.path;

            /// should we store the deep link in some array list
            /// of shared preferences, we could then process/send it/them along
            /// with things like 'first connect' and any pub-account linking
            /// which could be used as attributing initial install, linking, etc. to a referrer?

            processDeepLink();
          }
        },
        onError: (OnLinkErrorException e) async =>
            _log.w('onLink | ${e.message}'));
  }

  /// figure out what to do (if anything) with an incoming deep link
  /// depending on where we want to go, if we're logged in, have the right type of account
  /// available, etc. etc.
  void processDeepLink() {
    /// bail if we don't have a deep link
    if (_deepLink == null) {
      return;
    }

    /// if we have no master user or a current workspace or profile then do nothing
    if (_masterUser == null ||
        _currentWorkspace == null ||
        _currentWorkspace?.hasLinkedPublishers == false ||
        _currentProfile == null) {
      /// clear deep link
      _deepLink = null;

      return;
    }

    /// if the current profile is a business, then show a snackbar
    /// that a deal can only be viewed by a creator
    if (_currentProfile.isBusiness) {
      /// clear deep link
      _deepLink = null;

      showSharedModalAlert(_navKey.currentState.overlay.context,
          Text("Only Creators can view RYDRs"),
          actions: [
            ModalAlertAction(
                label: "OK",
                onPressed: () =>
                    Navigator.of(_navKey.currentState.overlay.context).pop())
          ]);
      return;
    } else {
      /// if we have a creator then take them to the deal page
      _navKey.currentState.pushNamed(
          AppRouting.getDealViewByLinkRoute(_deepLink.replaceAll('/x/', '')));

      /// clear deep link
      _deepLink = null;
    }
  }
}
