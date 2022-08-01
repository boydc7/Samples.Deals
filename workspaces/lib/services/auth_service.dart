import 'dart:async';

import 'package:firebase_auth/firebase_auth.dart';
import 'package:js/js.dart';
import 'package:logger/logger.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydrworkspaces/app/log.dart';
import 'package:rydrworkspaces/data/account_api.dart';
import 'package:rydrworkspaces/main.dart';
import 'package:rydrworkspaces/models/user.dart';
import 'package:rydrworkspaces/utils/fbjs.dart';

enum AuthState {
  unknown,
  unauthenticated,
  authenticating,
  authenticated,
}

class AuthService {
  static final Logger _log = getLogger("AuthService");
  static final _authStateChangeSubject = BehaviorSubject<AuthState>();
  static final _firebaseAuth = FirebaseAuth.instance;
  static final AuthService _instance = AuthService._internal();

  Stream<FirebaseUser> _onFirebaseAuthStateChangedObservable;
  User _currentRydrUser;
  FirebaseUser _currentGfbUser;
  AuthState _currentState = AuthState.unknown;

  AuthService._internal();

  static AuthService get instance => _instance;

  FirebaseUser get currentGfbUser => _currentGfbUser;

  String get currentAccountId => _currentGfbUser?.uid;

  Stream<AuthState> get onAuthStateChanged => _authStateChangeSubject.stream;

  bool isAuthenticated() => _currentGfbUser != null && _currentRydrUser != null;

  void dispose() {
    _authStateChangeSubject.close();
    _onFirebaseAuthStateChangedObservable = null;
  }

  Future<void> init() async {
    try {
      _setAuthState(AuthState.authenticating);

      await _initGfbUser();

      if (_currentGfbUser == null) {
        await signOut();
        return;
      }

      await _syncRydrUser();

      if (_verifyCurrentConfig()) {
        _setAuthState(AuthState.authenticated);
      } else {
        await signOut();
      }
    } catch (x) {
      _log.e(x.toString(), x);

      await signOut(x.toString());

      rethrow;
    } finally {
      // Do not start listening until the end of init purposely...
      _onFirebaseAuthStateChangedObservable = _firebaseAuth.onAuthStateChanged;
      _onFirebaseAuthStateChangedObservable.listen(_onFirebaseAuthStateChanged);
    }
  }

  void _onFirebaseAuthStateChanged(FirebaseUser gfbUser) {
    if (gfbUser == null) {
      if (_currentGfbUser != null) {
        signOut();
      }

      return;
    }

    if (_currentGfbUser == null) {
      _currentGfbUser = gfbUser;
    }

    if (gfbUser.uid != _currentGfbUser.uid) {
      signOut();
      return;
    }

    _setAuthState(AuthState.authenticated);
  }

  Future<void> signOut([String errorReason]) async {
    _currentRydrUser = null;

    if (_currentGfbUser != null || await _firebaseAuth.currentUser() != null) {
      _currentGfbUser = null;
      await _firebaseAuth.signOut();
    }

    if (errorReason != null) {
      _log.d(errorReason);
    }

    _setAuthState(AuthState.unauthenticated);

    navKey.currentState.pushReplacementNamed('/auth');
  }

  Future<void> onAuthenticate(
    AuthCredential credential, {
    String authProviderToken,
    String authProviderId,
  }) async {
    if (_currentState == AuthState.authenticating) {
      return;
    }

    try {
      _setAuthState(AuthState.authenticating);

      await _firebaseAuth.signInWithCredential(credential);

      _currentGfbUser = await _firebaseAuth.currentUser();

      if (_currentGfbUser == null) {
        await signOut();
        return;
      }

      await _syncRydrUser();

      if (_verifyCurrentConfig()) {
        _setAuthState(AuthState.authenticated);
      } else {
        await signOut();
      }
    } catch (x) {
      await signOut();
      rethrow;
    }
  }

  void _setAuthState(AuthState toState) {
    _log.d('Setting AuthState to [$toState]');
    _currentState = toState;
    _authStateChangeSubject.add(toState);
  }

  bool _verifyCurrentConfig() {
    if (_currentGfbUser == null || _currentRydrUser == null) {
      return false;
    }

    // We have valid objects, do they match?
    if (_currentRydrUser.authProviderUid != _currentGfbUser.uid) {
      _log.w(
          'Mismatch UID in auth verify - gfb:[${_currentGfbUser.uid}], tapi:[${_currentRydrUser.authProviderUid}]');
      return false;
    }

    return true;
  }

  Future<void> _initGfbUser() async {
    if (_currentGfbUser == null) {
      _currentGfbUser = await _firebaseAuth.currentUser();
    }

    if (_currentGfbUser == null) {
      await signOut();
    }
  }

  Future<void> _syncRydrUser() async {
    // Go to the Rydr api and sync the current user info with the GFB user auth info
    if (_currentRydrUser == null ||
        _currentRydrUser.authProviderUid != _currentGfbUser.uid) {
      _currentRydrUser = await AccountApi.instance.getMyUser();
    }
  }
}

class FacebookAuthService {
  static final Logger _log = getLogger('FacebookAuthService');

  static Future<void> signOut() async {
    try {
      Fb.logout(
        allowInterop(
          (r) {
            _log.d('Facebook JS logout complete');
          },
        ),
      );
    } catch (x) {
      _log.w(x.toString(), x);
    }

    await AuthService.instance.signOut();
  }

  static Future<void> tryAuthenticate() async {
    try {
      var completer = new Completer();

      Fb.login(
        allowInterop(
          (r) {
            if (r?.status == null) {
              throw ('Fb login response or status missing');
            }

            final isConnected = r.status == 'connected';

            completer.complete(_FbJsLoginResponse(
              isConnected: isConnected,
              token: isConnected ? r.authResponse.accessToken : null,
              userId: isConnected ? r.authResponse.userID : null,
            ));
          },
        ),
        LoginOptions(
          scope:
              'pages_show_list,instagram_basic,instagram_manage_insights,email,public_profile',
        ),
      );

      var fbResult = await completer.future;

      if (fbResult == null || !fbResult.isConnected) {
        _log.w('Could not successfully login via FbJs');
        await signOut();
      } else {
        _log.d('Facebook token [${fbResult.token}]');

        var authCredential = FacebookAuthProvider.getCredential(
          accessToken: fbResult.token,
        );

        await AuthService.instance.onAuthenticate(
          authCredential,
          authProviderToken: fbResult.token,
          authProviderId: fbResult.userId,
        );
      }
    } catch (x) {
      _log.e(x.toString(), x);

      await signOut();
    }
  }
}

class _FbJsLoginResponse {
  final bool isConnected;
  final String token;
  final String userId;

  _FbJsLoginResponse({
    this.isConnected,
    this.token,
    this.userId,
  });
}
