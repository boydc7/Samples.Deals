import 'dart:async';

import 'package:apple_sign_in/apple_sign_in.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/ui/connect/widgets/animated_logo.dart';

import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class AuthenticatePage extends StatefulWidget {
  @override
  _AuthenticatePageState createState() => _AuthenticatePageState();
}

class _AuthenticatePageState extends State<AuthenticatePage> {
  StreamSubscription _subAuthState;

  @override
  void initState() {
    super.initState();
    _subAuthState =
        AuthenticationService.instance().authState.listen(_onAuthStateChanged);
  }

  @override
  void dispose() {
    _subAuthState?.cancel();

    super.dispose();
  }

  /// listen to changes in auth state, and if we detect that we're 'connected'
  /// then we can navigate the user to the next step
  void _onAuthStateChanged(AuthState state) {
    if (state == AuthState.authenticated) {
      Future.delayed(const Duration(milliseconds: 4000), () {
        /// if we have a firebase authenticated user then handle authentication
        /// which will call workspaces and determine current workspace & profile if available
        AuthenticationService.instance().handleAuthenticated().then((_) {
          /// once we've made the handleAuthenticated call, we'll have updated our app state
          /// and either have valid workspaces & profiles, or even a last stored profile
          /// or none yet, so calling getInitialRoute will tell us where to navigate next
          Navigator.of(context).pushNamedAndRemoveUntil(
              appState.getInitialRoute(), (Route<dynamic> route) => false);
        });
      });
    }
  }

  void _showLoginOptions() {
    showSharedModalBottomActions(
      context,
      actions: [
        ModalBottomAction(
            child: Text("View Terms of Use"),
            onTap: () => Utils.launchUrl(
                  context,
                  AppLinks.termsUrl,
                  trackingName: 'terms',
                )),
        ModalBottomAction(
            child: Text("View Privacy Policy"),
            onTap: () => Utils.launchUrl(
                  context,
                  AppLinks.privacyUrl,
                  trackingName: 'privacy',
                )),
      ],
    );
  }

  /// check first if we have a valid iOS version that supports
  /// sign in with apple, otherwise alert the user they are unable
  void _signInWithApple() async {
    if (await AppleSignIn.isAvailable()) {
      AuthenticationService.instance().signInWithApple();
    } else {
      showSharedModalError(context,
          title: "Not supported",
          subTitle:
              "Apple Sign-In is not supported on your device. It's available with iOS version 13+.");
    }
  }

  /// start the google authentication flow
  void _signInWithGoogle() async {
    AuthenticationService.instance().signInWithGoogle();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: Stack(
          alignment: Alignment.topRight,
          children: <Widget>[
            _buildLoginScreen(),
            Positioned(
              right: 8,
              child: SafeArea(
                child: StreamBuilder<AuthState>(
                  stream: AuthenticationService.instance().authState,
                  builder: (context, snapshot) {
                    /// if we're in the middle of authenticating, then we'll hide the 'ellipsis' icon
                    /// which opens the login options
                    final bool hideOptions =
                        snapshot.data == AuthState.authenticated ||
                            snapshot.data == AuthState.authenticatingApple ||
                            snapshot.data == AuthState.authenticatingGoogle;

                    return AnimatedOpacity(
                      opacity: hideOptions ? 0.0 : 1.0,
                      duration: Duration(milliseconds: 350),
                      child: IconButton(
                        icon: Icon(AppIcons.ellipsisV),
                        highlightColor: Colors.transparent,
                        color: Theme.of(context).textTheme.bodyText2.color,
                        onPressed: _showLoginOptions,
                      ),
                    );
                  },
                ),
              ),
            )
          ],
        ),
      );

  Widget _buildLoginScreen() => StreamBuilder<AuthState>(
        stream: AuthenticationService.instance().authState,
        builder: (context, snapshot) {
          final AuthState state =
              snapshot.data == null ? AuthState.idle : snapshot.data;
          final bool idle = state == AuthState.idle;
          final bool connected = state == AuthState.authenticated;
          final bool connecting = state == AuthState.authenticatingApple ||
              state == AuthState.authenticatingGoogle;
          final bool showError = state == AuthState.error;
          final Color statusColor = idle
              ? Theme.of(context).brightness == Brightness.dark
                  ? Colors.white
                  : AppColors.grey800
              : showError
                  ? Theme.of(context).brightness == Brightness.dark
                      ? Colors.white
                      : AppColors.grey800
                  : connecting
                      ? Theme.of(context).primaryColor
                      : connected
                          ? AppColors.successGreen
                          : Theme.of(context).brightness == Brightness.dark
                              ? Colors.white
                              : AppColors.grey800;

          return Stack(
            alignment: Alignment.center,
            children: <Widget>[
              Container(
                padding: EdgeInsets.only(
                    bottom: 16.0, top: MediaQuery.of(context).padding.top),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: <Widget>[
                    Expanded(
                      child: FadeInScaleUp(
                        30,
                        SafeArea(
                            top: true,
                            bottom: false,
                            child: AnimatedLogo(statusColor)),
                        duration: 2500,
                        startScale: 0.925,
                      ),
                    ),

                    /// animate login buttons in our out, depending on current state
                    /// of connecting, connected, or having an error
                    AnimatedSwitcher(
                      duration: Duration(milliseconds: 500),
                      child: connected
                          ? FadeOutOpacityOnly(5, _buildLoginButtons(state))
                          : showError
                              ? _buildLoginButtons(state)
                              : FadeInBottomTop(
                                  50, _buildLoginButtons(state), 350),
                    ),
                  ],
                ),
              ),

              /// once connected, this will overlay the screen to create a fade effect into the next
              /// screen where the user will continue on
              Visibility(
                visible: connected,
                child: FadeInOpacityOnly(
                  40,
                  Container(
                    height: MediaQuery.of(context).size.height,
                    width: MediaQuery.of(context).size.width,
                    color: Theme.of(context).scaffoldBackgroundColor,
                  ),
                  duration: 2000,
                ),
              )
            ],
          );
        },
      );

  Widget _buildLoginButtons(AuthState state) => SafeArea(
        bottom: true,
        top: false,
        child: Column(
          children: <Widget>[
            Padding(
              padding: EdgeInsets.only(bottom: 16.0),
              child: _buildConnectError(state),
            ),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 32.0, vertical: 8.0),
              child: _buildGoogleButton(state),
            ),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 32.0, vertical: 8.0),
              child: _buildAppleButton(state),
            ),
            Padding(
              padding: EdgeInsets.only(top: 12.0, left: 32.0, right: 32.0),
              child: RichText(
                textAlign: TextAlign.center,
                text: TextSpan(
                  children: [
                    TextSpan(
                        text:
                            "By registering you confirm that you accept our \n"),
                    LinkTextSpan(
                      context: context,
                      text: 'Terms of Use',
                      url: AppLinks.termsUrl,
                    ),
                    TextSpan(text: " and "),
                    LinkTextSpan(
                      context: context,
                      text: 'Privacy Policy.',
                      url: AppLinks.privacyUrl,
                    ),
                  ],
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          color: Theme.of(context).hintColor,
                        ),
                      ),
                ),
              ),
            ),
          ],
        ),
      );

  Widget _buildConnectError(AuthState state) => state == AuthState.error
      ? FadeInOpacityOnly(
          10,
          Text(
            "We were unable to connect your account",
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                    color: AppColors.errorRed,
                    fontWeight: FontWeight.w600,
                  ),
                ),
          ),
        )
      : Container();

  Widget _buildAppleButton(AuthState state) => Container(
        width: double.infinity,
        child: InkWell(
          borderRadius: BorderRadius.circular(80.0),
          onTap:
              state == AuthState.authenticatingApple ? () {} : _signInWithApple,
          child: Container(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(80.0),
              color: Theme.of(context).textTheme.bodyText2.color,
            ),
            padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 12),
            child: state == AuthState.authenticatingApple
                ? Text(
                    'Connecting...',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 16.0,
                        color: Theme.of(context).scaffoldBackgroundColor),
                  )
                : Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Icon(
                        AppIcons.apple,
                        size: 18.0,
                        color: Theme.of(context).scaffoldBackgroundColor,
                      ),
                      SizedBox(
                        width: 12.0,
                      ),
                      Text(
                        'Sign in with Apple',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 16.0,
                            color: Theme.of(context).scaffoldBackgroundColor),
                      ),
                    ],
                  ),
          ),
        ),
      );

  Widget _buildGoogleButton(AuthState state) => Container(
        width: double.infinity,
        child: InkWell(
          borderRadius: BorderRadius.circular(80.0),
          onTap: state == AuthState.authenticatingGoogle
              ? () {}
              : _signInWithGoogle,
          child: Container(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(80.0),
              boxShadow: AppShadows.elevation[0],
              color: Theme.of(context).brightness == Brightness.dark
                  ? Theme.of(context).appBarTheme.color
                  : Theme.of(context).scaffoldBackgroundColor,
            ),
            padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 12),
            child: state == AuthState.authenticatingGoogle
                ? Text(
                    'Connecting...',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 16.0,
                        color: Theme.of(context).textTheme.bodyText2.color),
                  )
                : Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      Container(
                        height: 18,
                        width: 18,
                        decoration: BoxDecoration(
                          image: DecorationImage(
                            fit: BoxFit.cover,
                            image: AssetImage(
                              'assets/icons/google-icon.png',
                            ),
                          ),
                        ),
                      ),
                      SizedBox(
                        width: 12.0,
                      ),
                      Text(
                        'Sign in with Google',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                            fontWeight: FontWeight.w600,
                            fontSize: 16.0,
                            color:
                                Theme.of(context).brightness == Brightness.dark
                                    ? Colors.white
                                    : Color(0xFF757575)),
                      ),
                    ],
                  ),
          ),
        ),
      );
}

class LinkTextSpan extends TextSpan {
  LinkTextSpan({@required BuildContext context, String url, String text})
      : super(
            style: TextStyle(
                color: Theme.of(context).brightness == Brightness.dark
                    ? Colors.white
                    : AppColors.grey400,
                fontWeight: FontWeight.bold),
            text: text ?? url,
            recognizer: TapGestureRecognizer()
              ..onTap =
                  () => Utils.launchUrl(context, url, trackingName: 'connect'));
}
