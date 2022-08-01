import 'package:flutter/material.dart';
import 'package:rydr_app/app/events.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/services/authenticate.dart';

class AppBloc {
  AppBloc() {
    /// Call getInstance on app events which will initialize and setup our app events class
    /// which includes main system lifecycle events listerners, etc.
    AppEvents.instance;
  }

  void startApp(GlobalKey<NavigatorState> navKey) async {
    /// Initialize our app state which loads some profile / device settings
    /// including 'onboarding' settings so we'd potentially send them to finish onboarding
    await appState.initialize(navKey);

    /// start up the authentication service, which will update auth state
    /// stream that we can listen to in the app entry page for changes and navigate
    /// the user from there to either login or load profiles, or onboarding, etc.
    await AuthenticationService.instance().init();
  }
}
