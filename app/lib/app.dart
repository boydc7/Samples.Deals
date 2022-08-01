import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app_bloc.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class AppEntry extends StatefulWidget {
  final GlobalKey<NavigatorState> navKey;

  AppEntry(this.navKey);

  @override
  _AppEntryState createState() => _AppEntryState();
}

class _AppEntryState extends State<AppEntry> {
  final AppBloc _bloc = AppBloc();
  StreamSubscription _subOnAuthStateChanged;

  @override
  void initState() {
    super.initState();

    /// call 'start app' which will init base services, etc.
    /// then we can listen to changes in auth state and redirect the user
    /// to either the login page, or if already connected then either to their profiles
    /// or to the next step of their onboarding which may be to connect a profile
    _bloc.startApp(widget.navKey);

    _subOnAuthStateChanged = AuthenticationService.instance()
        .authState
        .listen(_onAppPageStateChanged);
  }

  @override
  void dispose() {
    _subOnAuthStateChanged?.cancel();

    super.dispose();
  }

  /// listen for changes to the auth state on initial open/load of the app
  /// if we were able to authenticate/find a firebase user on the device
  void _onAppPageStateChanged(AuthState state) {
    if (state == AuthState.notAuthenticated) {
      Navigator.of(context).pushNamedAndRemoveUntil(
          AppRouting.getAuthenticate, (Route<dynamic> route) => false);
    } else if (state == AuthState.authenticated) {
      /// if we have a firebase authenticated user, then load workspaces which will find/set/update state with workspace(s) and profiles
      /// then based on that we can call getInitialRoute which will evaluate the state and direct the user to the next step, depending on
      /// if we have workspaces and profiles or not... so we'll either go to the login page, or profiles, or directly to a last active one
      AuthenticationService.instance().handleAuthenticated().then((_) {
        Navigator.of(context).pushNamedAndRemoveUntil(
            appState.getInitialRoute(), (Route<dynamic> route) => false);
      });
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: SafeArea(
          top: true,
          child: FadeInOpacityOnly(
            5,
            Container(
              width: double.infinity,
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  LoadingLogo(
                    radius: 72.0,
                    color: Theme.of(context).textTheme.bodyText2.color,
                  ),
                ],
              ),
            ),
            duration: 550,
          ),
        ),
      );
}
