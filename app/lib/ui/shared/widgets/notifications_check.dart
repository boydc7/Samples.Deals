import 'dart:async';
import 'dart:io' show Platform;

import 'package:flutter/material.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/events.dart';
import 'package:rydr_app/app/notifications.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/models/device_settings.dart';
import 'package:rydr_app/models/deal.dart';

import 'package:rydr_app/services/device_settings.dart';

/// Notifications check widget which is only applicable if we've already asked the user
/// and they have denied or turned off push notifications (iOS only)
/// we currently display it on top of the list of notifications if applicable
class NotificationsCheck extends StatefulWidget {
  final Deal deal;
  final bool canDismiss;

  NotificationsCheck({
    this.deal,
    this.canDismiss = true,
  });

  @override
  State<StatefulWidget> createState() => _NotificationsCheckState();
}

class _NotificationsCheckState extends State<NotificationsCheck> {
  StreamSubscription _subLocationPermission;
  bool showNotice = false;
  bool showFirstAsk = false;

  Map<String, String> _pageContent = {
    "enable_notifications": "Enable Notifications",
    "enable_notifications_prompt":
        "Open your Settings to enable notifications for RYDR",
    "notifications_disabled": "Push notifications are disabled for RYDR.",

    /// this is overwritte within initState
    "first_ask_message": "Get notified...",
  };

  @override
  void initState() {
    super.initState();

    /// listen for current settings of push notifications on iOS device
    /// by listening to appEvents global singleton (eventbus) emitting IOsSettingsRegisteredEvent
    _subLocationPermission = AppEvents.instance.eventBus
        .on<IOsSettingsRegisteredEvent>()
        .listen((event) {
      onIosSettingsRegistered(event.settings);

      /// if for some reason this widget was not destroyed we'll make sure
      /// that we're still mounted before we respond to the user making a choice
      /// after we pop up the ios request
      if (mounted) {
        /// if we were just asking for the first time
        /// then we can show the response from the user
        if (showFirstAsk) {
          appNotifications.iOSPermissionsPromptResponse(
            settings: event.settings,
            onAcceptedContinue: () => null,
            onDeniedContinue: () => null,
          );

          if (!mounted) {
            return;
          }

          setState(() => showFirstAsk = false);
        }
      }
    });

    /// trigger a check only on the current status of ios notifications
    /// this will prevent prompt if we've not asked already
    appNotifications.iOSPermissionsPrompt(true);

    /// if we've never asked the user for notifications
    /// then we can show the first ask box that would let them enable them
    showFirstAsk = appState.onboardSettings.askedNotifications == false;

    /// set the message for the first ask, depending on who we're displaying this to
    /// and whether or not we have a request passed to this widget or not
    if (showFirstAsk) {
      if (appState.currentProfile.isBusiness) {
        _pageContent['first_ask_message'] = widget.deal != null &&
                widget.deal.request != null
            ? "Get notified as soon as ${widget.deal.request.publisherAccount.userName} sends or replies to a message related to this RYDR."
            : "Get notified as soon as a Creator requests a RYDR or sends you a message";
      } else {
        _pageContent['first_ask_message'] = widget.deal != null
            ? "Get notified as soon as ${widget.deal.publisherAccount.userName} responds to your request or sends you a message"
            : "Get notified as soon as a Business responds to your request or sends you a message";
      }
    }
  }

  @override
  void dispose() {
    _subLocationPermission?.cancel();

    super.dispose();
  }

  void onIosSettingsRegistered(IOsPushNotificationSettings settings) async {
    bool saveSettings = false;
    int daysToWaitToAskAgain = -1;

    showNotice = false;

    /// get previously saved settings to see where we stand, when we last checked/saved the settings
    /// and then whether or not we should show a prompt to the user
    final IOsPushNotificationSettings deviceSettings =
        await DeviceSettings.getIOsSettings();

    /// if we don't have any settings then save a fresh set and don't do anything else for now
    if (deviceSettings == null) {
      saveSettings = true;
    } else {
      /// if push notifications are OFF then figure out if we should show the notice and save settings
      if (!settings.alert) {
        /// if the user has not previously dismissed the settings
        /// then we should save this check and show the notice
        ///
        /// if the user did dismiss the notice but its been x-days
        /// then we will re-show it to them which they can dismiss again
        if (!deviceSettings.dismissed) {
          saveSettings = true;
          showNotice = true;
        } else if (DateTime.now()
                .toUtc()
                .difference(deviceSettings.lastChecked)
                .inDays >
            daysToWaitToAskAgain) {
          /// update dismissed flag to false
          DeviceSettings.saveIOsSettingsDismissed(false);

          saveSettings = true;
          showNotice = true;
        }
      }
    }

    /// check if we should save settings
    if (saveSettings) {
      DeviceSettings.saveIOsSettings(IOsPushNotificationSettings(
        alert: settings.alert,
        badge: settings.badge,
        sound: settings.sound,
      ));
    }

    if (!mounted) {
      return;
    }

    setState(() {});
  }

  void dismissNotice() {
    DeviceSettings.saveIOsSettingsDismissed(true);

    setState(() => showNotice = false);
  }

  @override
  Widget build(BuildContext context) {
    /// only available for ios devices
    if (!Platform.isIOS) {
      return Container();
    }

    return showFirstAsk
        ? buildFirstAsk()
        : showNotice ? buildNotice() : Container();
  }

  Widget buildFirstAsk() => Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          SizedBox(height: 16.0),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 32.0),
            child: Text(
              _pageContent['first_ask_message'],
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(color: AppColors.grey300),
                  ),
            ),
          ),
          SizedBox(height: 16.0),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 16.0),
            child: PrimaryButton(
              context: context,
              hasShadow: true,
              buttonColor: AppColors.errorRed,
              label: _pageContent['enable_notifications'],
              onTap: () => appNotifications.iOSPermissionsPrompt(false),
            ),
          ),
          SizedBox(height: 20.0),
          Divider(
            height: 0,
          )
        ],
      );

  Widget buildNotice() => FadeInTopBottom(
        10,
        Container(
          color: Theme.of(context).canvasColor,
          child: Stack(
            alignment: Alignment.topRight,
            children: <Widget>[
              Column(
                crossAxisAlignment: CrossAxisAlignment.center,
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  SizedBox(height: 20.0),
                  Padding(
                    padding: EdgeInsets.symmetric(horizontal: 32.0),
                    child: Text(
                      _pageContent['notifications_disabled'],
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.caption,
                    ),
                  ),
                  SizedBox(height: 8.0),
                  Padding(
                    padding: EdgeInsets.symmetric(horizontal: 16.0),
                    child: PrimaryButton(
                      context: context,
                      label: _pageContent['enable_notifications'],
                      onTap: () => showSharedModalAlert(
                          context, Text(_pageContent['enable_notifications']),
                          content:
                              Text(_pageContent['enable_notifications_prompt']),
                          actions: [
                            ModalAlertAction(
                                label: "OK",
                                onPressed: () => Navigator.of(context).pop())
                          ]),
                    ),
                  ),
                  SizedBox(height: 16.0),
                  Divider(
                    height: 0,
                  )
                ],
              ),
              widget.canDismiss
                  ? Positioned(
                      right: 8.0,
                      top: 16.0,
                      child: GestureDetector(
                        child: Icon(
                          AppIcons.times,
                          size: 18.0,
                          color: AppColors.grey300,
                        ),
                        onTap: dismissNotice,
                      ),
                    )
                  : Container()
            ],
          ),
        ),
        10,
        begin: -80,
      );
}
