import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/notifications.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/events.dart';

class OnboardScreenNotificationsPrimer extends StatefulWidget {
  final Function moveForward;

  OnboardScreenNotificationsPrimer({
    @required this.moveForward,
  });

  @override
  State<StatefulWidget> createState() {
    return _OnboardScreenNotificationsPrimerState();
  }
}

class _OnboardScreenNotificationsPrimerState
    extends State<OnboardScreenNotificationsPrimer> {
  StreamSubscription _subAppResume;

  @override
  void initState() {
    super.initState();

    /// listen to ios event that'll get triggered in response to us
    /// asking the user for push notification permissions - we'll handle the callback
    /// using our appNotifications method that'll either do nothing or show a modal if the user declines
    _subAppResume = AppEvents.instance.eventBus
        .on<IOsSettingsRegisteredEvent>()
        .listen((event) {
      /// if for some reason this widget was not destroyed we'll make sure
      /// that we're still mounted before we respond to the user making a choice
      /// after we pop up the ios request
      if (mounted) {
        appNotifications.iOSPermissionsPromptResponse(
          settings: event.settings,
          onAcceptedContinue: widget.moveForward,
          onDeniedContinue: widget.moveForward,
        );
      }
    });
  }

  @override
  void dispose() {
    _subAppResume?.cancel();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (appState.currentProfile == null) {
      return Container(height: 0);
    }

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: 32.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Expanded(
              child: FadeInOpacityOnly(
                15,
                SizedBox(
                  height: 140.0,
                  width: 140.0,
                  child: SvgPicture.asset(
                    'assets/icons/access-notifications.svg',
                    color: Theme.of(context).textTheme.bodyText2.color,
                  ),
                ),
              ),
            ),
            Container(
              height: 140.0,
              child: Column(
                children: <Widget>[
                  FadeInTopBottom(
                    15,
                    Text(
                      "Stay Up to Date",
                      style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(fontSize: 18.0),
                          ),
                    ),
                    300,
                    begin: -40,
                  ),
                  SizedBox(
                    height: 8.0,
                  ),
                  FadeInTopBottom(
                    15,
                    Text(
                      appState.currentProfile.isBusiness
                          ? "We would like to notify you when your RYDR has been requested or a Creator sends you a message."
                          : "We would like to notify you when you've been approved for a RYDR or a Business sends you a message.",
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                    300,
                    begin: -40,
                  ),
                ],
              ),
            ),
            FadeInOpacityOnly(
              35,
              Row(
                children: <Widget>[
                  Expanded(
                    child: PrimaryButton(
                      labelColor: Theme.of(context).hintColor,
                      buttonColor: Theme.of(context).canvasColor,
                      label: 'Not Now',
                      onTap: widget.moveForward,
                    ),
                  ),
                  SizedBox(
                    width: 8,
                  ),
                  Expanded(
                    child: PrimaryButton(
                      label: 'Allow',
                      onTap: () => appNotifications.iOSPermissionsPrompt(false),
                    ),
                  ),
                ],
              ),
            ),
            SizedBox(
              height: 8.0,
            ),
            FadeInOpacityOnly(
              35,
              Text(
                "You will only get important ones. We promise. 🙏",
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(color: Theme.of(context).hintColor),
                    ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
