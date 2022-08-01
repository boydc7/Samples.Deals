import 'dart:io' show Platform;

import 'package:flutter/material.dart';

import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/services/device_settings.dart';

import 'package:rydr_app/models/device_settings.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/ui/connect/widgets/onboard_business_intro.dart';
import 'package:rydr_app/ui/connect/widgets/onboard_creator_intro.dart';
import 'package:rydr_app/ui/connect/widgets/onboard_creator_location.dart';
import 'package:rydr_app/ui/connect/widgets/onboard_notifications.dart';

/// TODO: onboard remove for creator
class ConnectOnboardPage extends StatefulWidget {
  @override
  _ConnectOnboardPageState createState() => _ConnectOnboardPageState();
}

class _ConnectOnboardPageState extends State<ConnectOnboardPage> {
  PageController pageControllerOnboarding;

  int currentPage = 0;

  List<Widget> pages = [];

  @override
  void initState() {
    /// build the list of screens for business or creator onboarding
    /// depending on certain step(s) that might have already been completed
    if (appState.currentProfile.isBusiness) {
      pages.add(OnboardBusinessScreenIntro(
        moveForward: goToNextPage,
      ));

      /// for iOS devices we include the primer screen for asking
      /// for notification permissons unless we've already asked
      if (Platform.isIOS && !appState.onboardSettings.askedNotifications) {
        pages.add(
          OnboardScreenNotificationsPrimer(
            moveForward: goToNextPage,
          ),
        );
      }
    } else {
      pages.add(OnboardCreatorScreenIntro(
        moveForward: goToNextPage,
      ));

      /// include the location tracking permission primer screen
      /// if we've not previously asked the user to track location
      if (!appState.onboardSettings.askedLocation) {
        pages.add(OnboardCreatorScreenLocationPrimer(
          moveNext: goToNextPage,
        ));
      }

      /// for iOS devices we include the primer screen for asking
      /// for notification permissons unless we've already asked
      if (Platform.isIOS && !appState.onboardSettings.askedNotifications) {
        pages.add(
          OnboardScreenNotificationsPrimer(
            moveForward: goToNextPage,
          ),
        );
      }
    }

    pageControllerOnboarding = PageController(
      keepPage: true,
      initialPage: 0,
    );

    pageControllerOnboarding.addListener(() {
      int next = pageControllerOnboarding.page.round();

      if (currentPage != next) {
        setState(() {
          currentPage = next;
        });
      }
    });

    super.initState();
  }

  @override
  void dispose() {
    pageControllerOnboarding.dispose();

    super.dispose();
  }

  void goToNextPage() {
    if (currentPage == pages.length - 1) {
      markOnboardCompleted();
    } else {
      pageControllerOnboarding.nextPage(
        curve: Curves.fastOutSlowIn,
        duration: Duration(milliseconds: 250),
      );
    }
  }

  /// this can be called from the last of the onboard flow screens
  /// it'll update the settings indicating we've completed onboarding for the give type of account
  /// then send the user to the account-specific homepage
  void markOnboardCompleted() async {
    /// get existing onboard settings, update the relevant flag
    /// then update the settings in storage which will also update state
    OnboardSettings settings = appState.onboardSettings;

    /// update onboarding as completed for the given current user
    if (appState.currentProfile.isBusiness) {
      settings.doneAsBusiness = true;
    } else {
      settings.doneAsCreator = true;
    }

    DeviceSettings.saveOnboardSettings(settings);

    /// send user to their respective home pages
    String route = appState.currentProfile.isBusiness
        ? AppRouting.getHome
        : AppRouting.getDealsMap;

    Navigator.of(context)
        .pushNamedAndRemoveUntil(route, (Route<dynamic> route) => false);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: _accountTypePage(),
    );
  }

  Widget _accountTypePage() {
    return Container(
      padding: EdgeInsets.symmetric(vertical: 16.0),
      child: Column(
        children: <Widget>[
          Expanded(
            child: FadeInOpacityOnly(
              5,
              PageView.builder(
                physics: NeverScrollableScrollPhysics(),
                controller: pageControllerOnboarding,
                itemCount: pages.length,
                itemBuilder: (context, position) {
                  return pages[position];
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}
