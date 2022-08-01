import 'dart:async';
import 'dart:io' show Platform;

import 'package:flutter/material.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/events.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/workspace.dart';

import 'package:rydr_app/ui/notifications/widgets/overlay.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/models/device_settings.dart';

import 'package:rydr_app/services/device_settings.dart';

/// Application-level global variable
AppNotifications appNotifications = new AppNotifications();

class AppNotifications {
  final log = getLogger('AppNotifications');
  static final AppNotifications _appNotifications =
      AppNotifications._internal();
  static final FirebaseMessaging _firebaseMessaging = FirebaseMessaging();

  BuildContext _currentContext;
  OverlayEntry _notificationOverlay;
  Timer _notificationTimer;

  GlobalKey<NavigatorState> _navKey;

  bool _notificationShowing = false;
  bool _isConfigured = false;

  StreamSubscription _subAppResume;

  factory AppNotifications() {
    return _appNotifications;
  }

  AppNotifications._internal() {
    /// nothing to do as of yet
  }

  dispose() {
    _subAppResume?.cancel();
  }

  /// initialize is (should be) called only once (from within app.dart)
  /// vs. from within _internal, since we need to pass a global navigator state key
  initialize(GlobalKey<NavigatorState> navKey) async {
    /// guard against accidental initialization after we've already
    /// initialized notifications and attached our firebase listeners
    if (!_isConfigured) {
      log.i('initialize');

      _navKey = navKey;
      _isConfigured = true;

      /// get current app state
      _currentContext = _navKey.currentState.overlay.context;

      /// onMessage = app is in foreground
      /// onLaunch = app was terminated, user clicks notification
      /// onResume = app was in background, user clicks notification
      _firebaseMessaging.configure(
        onMessage: (Map<String, dynamic> message) async =>
            processMessage(message, true, 'onMessage'),
        onLaunch: (Map<String, dynamic> message) async =>
            processMessage(message, false, 'onLaunch'),
        onResume: (Map<String, dynamic> message) async =>
            processMessage(message, false, 'onResume'),
      );

      /// when we 'hear' back from iOs (after making a call either implicity the first time to ask
      /// or other times when we query for current settings, we'll fire an event that can be 'heard'
      /// by anyone listing to app events of type IOsSettingsRegisteredEvent
      _firebaseMessaging.onIosSettingsRegistered.listen((
        IosNotificationSettings settings,
      ) {
        AppEvents.instance.eventBus
            .fire(IOsSettingsRegisteredEvent(IOsPushNotificationSettings(
          alert: settings.alert,
          badge: settings.badge,
          sound: settings.sound,
        )));
      });

      /// if we get a resume event then trigger another iOs permissions prompt
      /// so if a user switches to ios settings and turns notifications on/off we can re-check
      /// as soon as the app gets back in the foreground (is resumed)
      _subAppResume =
          AppEvents.instance.eventBus.on<AppResumedEvent>().listen((event) {
        if (event.resumed) {
          iOSPermissionsPrompt(true);
        }
      });
    }
  }

  /// can be called to prompt the user, or silently (on iOS only, and only one time) to enable
  /// push notifications, Response from the user can be 'listened to' using app events
  /// appEvents.eventBus.on<IOsSettingsRegisteredEvent>().listen((event) {
  ///    onIosSettingsRegistered(event.settings);
  ///  });
  void iOSPermissionsPrompt(bool checkOnly) {
    if (Platform.isIOS) {
      final bool hasAskedAlready = appState.onboardSettings.askedNotifications;

      /// if we're looking to only 'check' on the status of push notifications
      /// but we've never previously asked before, then we dont' do anything
      if (checkOnly && !hasAskedAlready) {
        return;
      }

      _firebaseMessaging.requestNotificationPermissions(
        const IosNotificationSettings(
          sound: true,
          badge: true,
          alert: true,
        ),
      );

      /// update device storage so we know that we've asked already
      /// we can only ask once in iOS so once we ask we shall never ask again
      /// but rather just inform the user (if denied) that they should enable them
      if (!hasAskedAlready) {
        OnboardSettings settings = appState.onboardSettings;
        settings.askedNotifications = true;

        DeviceSettings.saveOnboardSettings(settings);
      }
    }
  }

  void iOSPermissionsPromptResponse({
    @required IOsPushNotificationSettings settings,
    @required Function onDeniedContinue,
    @required Function onAcceptedContinue,
  }) {
    /// log the users decision to enable or decline permissions with analytics
    AppAnalytics.instance.logEvent(settings.alert
        ? 'perms_notifications_accepted'
        : 'perms_notifications_declined');

    if (!settings.alert) {
      /// if the user agreed and we prompted them to enable notifications, but then
      /// they changed their mind and declined them after all we show a dialog telling them
      /// how to enable them if they choose to at another time
      showSharedModalAlert(_currentContext, Text("We won't notify you..."),
          content: Text(
              "If you change your mind in the future you can always enable notifications in your Settings"),
          actions: <ModalAlertAction>[
            ModalAlertAction(
                label: "Ok",
                onPressed: () {
                  Navigator.of(_currentContext).pop();

                  onDeniedContinue();
                })
          ]);
    } else {
      /// user enabled notifications
      onAcceptedContinue();
    }
  }

  Future<bool> _switchToTargetProfile(
    AppNotification message, [
    bool ignoreWorkspaceId = false,
  ]) async {
    /// current profile could be null (if we're on the switcher for example)
    /// so guard against null-id on the current profile in app state
    final int currentProfileId = appState.currentProfile?.id;

    /// if we don't have a toPublisherAccount on the message then this good to go since it wasn't meant for any
    /// particular user on the device but rather would be some kind of generic notification
    if (message.toPublisherAccount == null) {
      return true;
    }

    /// if the target profile is the same that is currently active, and we have a creator
    /// then we're good to go
    if (currentProfileId == message.toPublisherAccount.id &&
        appState.currentProfile.isCreator) {
      return true;
    }

    /// if the target profile is the same as currently active, and we have a business
    /// and we're looking to ignore the workspace check, then we're good to go
    if (currentProfileId == message.toPublisherAccount.id &&
        ignoreWorkspaceId) {
      return true;
    }

    /// if the target profile is the same that is currently active, and we have a business
    /// then check the workspace for personal vs. team, and only validate the workspace it was meant for
    /// when the workspaceId > 0 and the current workspace is a team as well
    if (currentProfileId == message.toPublisherAccount.id &&
        appState.currentProfile.isBusiness &&
        ((appState.currentWorkspace.type == WorkspaceType.Personal &&
                message.workspaceId == 0) ||
            (appState.currentWorkspace.type == WorkspaceType.Team &&
                message.workspaceId == appState.currentWorkspace.id))) {
      return true;
    }

    /// if this notification is meant for another profile than the currently active one, - OR -
    /// the business profile that its meant for is from another workspace,
    /// then try to switch to it which will either be successful or not
    final bool goodToGo = await appState.switchProfile(
        message.toPublisherAccount.id,
        message.workspaceId > 0 ? message.workspaceId : null);

    if (!goodToGo) {
      return false;
    } else {
      /// update the message indicating that we switched the user
      message.switchedUser = true;

      log.i('_switchToTargetProfile | ${appState.currentProfile.userName}');

      showSharedLoadingLogo(
        _currentContext,
        content: "Switching Profile",
      );

      /// force an artificial delay in showing this overlay
      /// before potentially then navigating the user to another page
      final bool done = await Future.delayed(const Duration(seconds: 2), () {
        Navigator.of(_currentContext).pop();
      }).then((_) {
        return true;
      });

      return done;
    }
  }

  /// ALL firebase incoming messages are processed by this function
  /// and from here we determine whether they are local and need to be shown
  /// or if they are native then we'd get here from a user click and we can figure out
  /// where to redirect the user to depending on where they currently are on the app
  void processMessage(
    Map<String, dynamic> message,
    bool isLocal,
    String fromCaller,
  ) async {
    log.d('$fromCaller | $message');

    /// we take the incoming unstructured message, convert it into a proper message class
    /// then we can inspect it and understand what to do with the message,
    final AppNotification _message = AppNotification.fromMessage(message);

    /// if we have a message that is good to show then either process
    /// a local notification (from onMessage) or a native 'click'
    if (isLocal) {
      /// next, process some logic which decides whether or not the message is even elligible
      /// to be shown to the user or should be supressed
      final bool showMessage = await shouldShowLocalNotification(_message);

      /// if we should show the message on the device (via overlay that comes down from the top)
      /// then we call processLocal message which will create the overlay, start a timer to hide it
      /// and then display it on the device for the current user
      ///
      /// otherwise, we're likely on the page already that the dialog message should go on
      /// e.g. on the chat page for a request, and we simply add the message to the app message stream
      /// which we then listen to on the request dialog page and append to the list
      if (showMessage) {
        /// if this message is intended for the current user then increment that users' unread count
        if (_message.toPublisherAccount != null &&
            _message.toPublisherAccount.id == appState.currentProfile?.id &&
            (_message.workspaceId == 0 ||
                _message.workspaceId == appState.currentWorkspace.id)) {
          appState.updateUnreadNotificationsCount();

          /// update the system tray app badge counter
          /// to the current count of unread notifications for the current user
          //if (await FlutterAppBadger.isAppBadgeSupported()) {
          //FlutterAppBadger.updateBadgeCount(
          //  appState.currentProfile.unreadNotifications);
          //}
        }

        processLocal(_message);
      } else {
        appState.messageStream.add(_message);
      }
    } else {
      processNative(_message);
    }
  }

  void processNative(AppNotification message) async {
    // the only time this would be false if we don't have the user
    // on the users device / were unable to switch to it...
    if (await _switchToTargetProfile(message)) {
      navigate(message);
    }
  }

  /// process an incoming message that should show on the users device while their in the app
  /// this is used both for showing a notification and for showing a notification saying
  /// that the user is not found on the device after they click a message for an intended user
  /// that may have since been removed from the device
  void processLocal(AppNotification message,
      [bool showMissingUserError = false]) {
    // if we still have a timer running the cancel it
    if (_notificationTimer != null) {
      _notificationTimer.cancel();
    }

    /// if we still have a notification showing then we'll need to remove it
    /// from the navigator overlays
    if (_notificationShowing) {
      _notificationOverlay.remove();
    }

    /// create the new notification passing in the current navigator context
    /// and message we received from app.dart
    _notificationOverlay = OverlayEntry(builder: (BuildContext context) {
      return AppNotificationOverlay(
        message: message,
        showError: showMissingUserError,
        onTap: () {
          _notificationOverlay.remove();
          _notificationShowing = false;

          /// if this was an error then the tap does nothing
          if (!showMissingUserError) {
            processLocalNavigateClick(message);
          }
        },
        onDismiss: () {
          _notificationOverlay.remove();
          _notificationShowing = false;
        },
      );
    });

    /// add the overlay to the current navigator
    Navigator.of(_currentContext).overlay.insert(_notificationOverlay);
    _notificationShowing = true;

    /// create a new timer that will trigger hiding the notification
    /// if its still shown (wasn't dismised by the user or superceeded by another notification)
    _notificationTimer = Timer(const Duration(seconds: 7), () {
      if (_notificationShowing) {
        _notificationOverlay.remove();
        _notificationShowing = false;
      }
    });
  }

  /// when the user clicks a local notification message then we call this
  /// we'll first switch to the intended user if need be, and ensure that we were able to do so
  /// then we'll make a call to navigate to the route intended by the message
  void processLocalNavigateClick(AppNotification message) async {
    if (await _switchToTargetProfile(message)) {
      navigate(message);
    } else {
      log.w(
          'processLocalNavigateClick | profile: ${message.toPublisherAccount.id} not found on device');

      processLocal(message, true);
    }
  }

  /// when the user clicks a local notification message from the list then we call this
  /// we'll first switch to the intended user if need be, and ensure that we were able to do so
  /// then we'll make a call to navigate to the route intended by the message
  ///
  /// NOTE! when tapping on a notification from the list we are as of now ensured that even if
  /// the target and current profile don't match, we're at least guaranteed to be in the same workspace
  /// since we don't as of yet support showing notifications from profiles in different workspaces (same is fine)
  /// this is why we pass 'true' to the _switchToTargetProfile indicating we want to skip the workspaceId check
  void processLocalListTap(AppNotification message) async {
    if (await _switchToTargetProfile(message, true)) {
      navigate(message);
    } else {
      log.w(
          'processLocalNavigateClick | profile: ${message.toPublisherAccount.id} not found on device');

      processLocal(message, true);
    }
  }

  /// calling this means we are ready to navigate the user
  /// to the route that was identified when converting the message into a notification model
  /// at this point we've passed any errors and have switched to the target user if user
  /// had a different user active prior to navigation
  void navigate(AppNotification message) {
    log.i('navigate | navigate to message');

    /// if we're good to navigate the user to the inteded message
    /// then we can also reduce their unread notification count by one
    appState.updateUnreadNotificationsCount(true);

    /// if we didn't switch the user we can push onto the stack
    /// if we did we'll want to clear the stack of all existing pages
    if (message.route != null) {
      log.i('navigate | message route is: ${message.route}');

      if (!message.switchedUser) {
        Navigator.of(_currentContext).pushNamed(message.route);
      } else {
        Navigator.of(_currentContext).pushNamedAndRemoveUntil(
            message.route, (Route<dynamic> route) => false);
      }
    } else {
      log.i('navigate | message has no route');

      /// if we did not navigate the user to a new route
      /// because the notification did not have a route but we DID switch users
      /// then we'll need to still clear the stack and send the user to their notifications list/profile
      if (message.switchedUser) {
        Navigator.of(_currentContext).pushNamedAndRemoveUntil(
            AppRouting.getProfileMeRoute, (Route<dynamic> route) => false);
      }
    }
  }

  /// Here we'll figure out if the incoming local notification should even be shown
  /// to the user based on a few different logic points
  Future<bool> shouldShowLocalNotification(AppNotification message) async {
    /// if there are no users on this device then supress any messages
    /// this could happen if a message token on the server is still valid or in flight
    /// but the user has removed all users from the device (and would be on the start page again)
    if (appState.hasAnyProfilesOnDevice == false) {
      log.w('shouldShowLocalNotification | no profiles on device');
      return false;
    }

    /// if this message is intended for a given publisher and the current user
    /// is that same publisher then we don't need to show it to the user him/herself
    if (message.fromPublisherAccount != null &&
        message.fromPublisherAccount.id == appState.currentProfile?.id) {
      log.i('shouldShowLocalNotification | target user matches current user');

      return false;
    }

    /// this will pop the first route from our navigator without actually popping it (return true)
    /// this route name will essentially be the current 'page' we're on which we'll use to figure out
    /// if we should be navigating the user somewhere else based on where the message should route vs. where they are now
    String currentRouteName;
    Navigator.of(_currentContext).popUntil((route) {
      currentRouteName = route.settings.name;
      return true;
    });

    if (message.route != null && message.route == currentRouteName) {
      log.i(
          'shouldShowLocalNotification | target route: ${message.route} same as current route: $currentRouteName');

      return false;
    }

    return true;
  }
}
