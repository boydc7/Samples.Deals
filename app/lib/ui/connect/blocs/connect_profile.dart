import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:flutter_facebook_login/flutter_facebook_login.dart';
import 'package:rydr_app/app/analytics.dart';

import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/responses/id_response.dart';

import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/services/instagram.dart';

enum ConnectProfileState {
  idle,
  connectingFacebook,
  connectingInstagram,
  connectedFacebook,
  connectedInstagram,
  permissionsDenied,
  cancelledbyUser,
  errorInstagram,
  errorFacebook,
}

class ConnectProfileBloc {
  final _log = getLogger('ConnectProfileBloc');
  final FacebookLogin _fbSignIn = FacebookLogin();
  final _connectState =
      BehaviorSubject<ConnectProfileState>.seeded(ConnectProfileState.idle);

  dispose() {
    _connectState.close();
  }

  BehaviorSubject<ConnectProfileState> get connectState => _connectState.stream;
  void setState(ConnectProfileState state) => _connectState.sink.add(state);

  Future<String> getInstagramAuthUrl() async {
    final StringIdResponse res = await InstagramService.getAuthUrl();

    return res.model;
  }

  Future<bool> processInstagramResult(ConnectInstagramResults res) async {
    /// no result indicates the user cancelled out
    if (res == null) {
      setState(ConnectProfileState.cancelledbyUser);

      /// return null indicating not success, nor an error
      return null;
    } else if (res.success) {
      setState(ConnectProfileState.connectedInstagram);

      return true;
    } else {
      setState(ConnectProfileState.errorInstagram);
    }

    return false;
  }

  Future<bool> loginWithFacebook() async {
    setState(ConnectProfileState.connectingFacebook);

    try {
      /// only webview is supported currently
      _fbSignIn.loginBehavior = FacebookLoginBehavior.webViewOnly;

      final FacebookLoginResult fbLoginResult = await _fbSignIn.logIn(
        [
          'pages_show_list',
          'instagram_basic',
          'instagram_manage_insights',
          'email',
          'public_profile',
          'manage_pages',
        ],
      );

      switch (fbLoginResult.status) {
        case FacebookLoginStatus.loggedIn:
          final FacebookAccessToken accessToken = fbLoginResult.accessToken;

          _log.d('login | received FB token ${accessToken.toMap()}');

          /// check if the user declined permission(s) needed in order for us to retrieve
          /// and if so, don't create the account and instead inform the user to authorize those permissions
          var declined = fbLoginResult.accessToken.declinedPermissions.any(
              (p) =>
                  p == 'pages_show_list' ||
                  p == 'instagram_basic' ||
                  p == 'instagram_manage_insights' ||
                  p == 'email' ||
                  p == 'public_profile' ||
                  p == 'manage_pages');

          if (declined) {
            setState(ConnectProfileState.permissionsDenied);

            /// log as screen
            _logScreen('connect/facebook/errorpermissions');
          } else {
            final bool success =
                await AuthenticationService.instance().addProfileToRydrUser(
              fbLoginResult.accessToken.token,
              fbLoginResult.accessToken.userId,
            );

            if (success) {
              /// load all workspaces now which should have either a new one
              /// or the one we had prior and have had replace it with the new fb token acct
              final bool successWs =
                  await AuthenticationService.instance().loadWorkspaces();

              setState(successWs
                  ? ConnectProfileState.connectedFacebook
                  : ConnectProfileState.errorFacebook);
              return true;
            } else {
              setState(ConnectProfileState.errorFacebook);
            }

            _logScreen('connect/facebook/done', false);
          }

          break;
        case FacebookLoginStatus.cancelledByUser:
          {
            _log.e('loginWithFacebook | cancelled by the user');
            _logScreen('connect/facebook/cancelled');

            setState(ConnectProfileState.cancelledbyUser);
            break;
          }
        case FacebookLoginStatus.error:
          {
            _log.e('loginWithFacebook | fberror', fbLoginResult.errorMessage);
            _logScreen('connect/facebook/error');

            setState(ConnectProfileState.errorFacebook);
            break;
          }
      }
    } catch (x) {
      _log.e('loginWithFacebook | catcherror', x.toString());
      _logScreen('connect/facebook/errorcatch');

      setState(ConnectProfileState.errorFacebook);
    }

    return false;
  }

  void _logScreen(String path, [bool dynamicFormat = true]) =>
      AppAnalytics.instance.logScreen(path, dynamicFormat);
}

class ConnectInstagramResults {
  final bool success;
  final String username;
  final String postBackId;
  final RydrAccountType linkedAsAccountType;
  final String error;
  final String errorReason;
  final String errorDesc;

  ConnectInstagramResults(
    this.success,
    this.username,
    this.postBackId,
    this.linkedAsAccountType,
    this.error,
    this.errorReason,
    this.errorDesc,
  );
}
