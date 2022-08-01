import 'dart:async';
import 'package:apple_sign_in/apple_sign_in.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';
import 'package:google_sign_in/google_sign_in.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/device_settings.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/services/device_settings.dart';
import 'package:rydr_app/services/device_storage.dart';
import 'package:rydr_app/services/notifications.dart';
import 'package:rydr_app/services/publisher_account.dart';
import 'package:rydr_app/services/workspaces.dart';

/// if we don't have a master user (firebase user)
/// then this would be the auth states to listen / react to
enum AuthState {
  idle,
  authenticatingGoogle,
  authenticatingApple,
  authenticated,
  notAuthenticated,
  cancelledbyUser,
  error,
  signingOut,
}

/// once authenticated, we'll get a response of one of these
/// from making a call to get workspace
enum AuthenticatedState {
  noProfiles,
  hasProfiles,
  error,
}

class AuthenticationService {
  static AuthenticationService _instance;
  static final _log = getLogger('AuthenticationService');
  static final _authState = BehaviorSubject<AuthState>();

  FirebaseUser _currentFirebaseUser;
  Stream<FirebaseUser> _onAuthStateChanged;
  BehaviorSubject<AuthState> get authState => _authState.stream;
  FirebaseUser get currentFirebaseUser => _currentFirebaseUser;

  static final _firebaseAuth = FirebaseAuth.instance;

  AuthenticationService._internal() {
    _log.i('_internal | AuthLoginService initialized');
  }

  factory AuthenticationService.instance() =>
      _instance ??= AuthenticationService._internal();

  void dispose() {
    _authState.close();
    _onAuthStateChanged = null;
  }

  void setAuthState(AuthState state) => _authState.sink.add(state);

  /// this is called when the app first loads, we check for an existin firebase user/token
  /// on the device then attempt to log in the rydr user from there, we also listen to changes
  /// to the logged in state of the firebase user and react to changes (e.g. force a signout)
  Future<void> init() async {
    setAuthState(AuthState.idle);

    /// get an existing firebase user on the device, this would either be an apple or google
    /// linked firebase user, as those are the two options we support currently
    _currentFirebaseUser = await _firebaseAuth.currentUser();

    /// if this is a new install of the app, but we have a firebase user token
    /// on the device from a previous install, then force a signout of firebase
    if (await DeviceStorage.isNewInstall() && _currentFirebaseUser != null) {
      _log.i(
          'init | found existing user but new install - signing out of firebase');

      await signOut(false);
    }

    /// if we have an existing authenticated user that is not one that we support
    /// then we'll need to log out the firebase user to force a new authentication
    if (_currentFirebaseUser != null &&
        _currentFirebaseUser.providerData != null &&
        _currentFirebaseUser.providerData.isNotEmpty &&
        _currentFirebaseUser.providerData
            .any((d) => d.providerId == "facebook.com")) {
      await signOut(false);
    }

    /// if we have an existing authenticated user that is not one that we support
    /// then we'll need to log out the firebase user to force a new authentication
    if (_currentFirebaseUser != null &&
        _currentFirebaseUser.providerData != null &&
        _currentFirebaseUser.providerData.isNotEmpty &&
        _currentFirebaseUser.providerData
            .any((d) => d.providerId == "facebook.com")) {
      await _firebaseAuth.signOut();
      _currentFirebaseUser = null;
    }

    /// if we've got a valid firebase user then we're connected
    /// otheriwise, update auth state to reflect that we're not yet connected
    if (_currentFirebaseUser != null) {
      /// the firebase user will be our master user
      appState
          .setMasterUser(PublisherAccount.fromFirebase(_currentFirebaseUser));

      setAuthState(AuthState.authenticated);
    } else {
      setAuthState(AuthState.notAuthenticated);
    }

    /// Do not start listening until the end of init purposely...
    _onAuthStateChanged = _firebaseAuth.onAuthStateChanged;
    _onAuthStateChanged.listen(_onFirebaseAuthStateChanged);
  }

  Future<void> signInWithGoogle() async {
    setAuthState(AuthState.authenticatingGoogle);

    final GoogleSignIn googleSignIn = GoogleSignIn();

    try {
      final GoogleSignInAccount googleSignInAccount =
          await googleSignIn.signIn();

      /// if null, then the user cancelled
      if (googleSignInAccount == null) {
        setAuthState(AuthState.cancelledbyUser);

        _log.i('signInWithGoogle | user cancelled');
        return false;
      }

      final GoogleSignInAuthentication googleSignInAuthentication =
          await googleSignInAccount.authentication;

      /// sign into firebase, and connect the user with rydr
      await _signIntoFirebase(GoogleAuthProvider.getCredential(
        accessToken: googleSignInAuthentication.accessToken,
        idToken: googleSignInAuthentication.idToken,
      ));
    } catch (e) {
      _log.e('singInWithGoogle | ${e.toString()}', e);

      setAuthState(AuthState.error);
    }
  }

  Future<void> signInWithApple() async {
    setAuthState(AuthState.authenticatingApple);

    try {
      final AuthorizationResult result = await AppleSignIn.performRequests([
        AppleIdRequest(requestedScopes: [Scope.email, Scope.fullName])
      ]);

      switch (result.status) {
        case AuthorizationStatus.authorized:
          try {
            await _signIntoFirebase(
                OAuthProvider(
                  providerId: "apple.com",
                ).getCredential(
                  idToken:
                      String.fromCharCodes(result.credential.identityToken),
                  accessToken:
                      String.fromCharCodes(result.credential.authorizationCode),
                ),
                "${result.credential.fullName.givenName} ${result.credential.fullName.familyName}");
          } catch (e) {
            setAuthState(AuthState.error);

            _log.e('signInWithApple', e);
          }
          break;
        case AuthorizationStatus.error:
          setAuthState(AuthState.error);

          _log.e('signInWithApple', result.error);
          break;

        case AuthorizationStatus.cancelled:
          setAuthState(AuthState.cancelledbyUser);

          _log.i('signInWithApple | user cancelled');
          break;
      }
    } catch (e) {
      _log.e('signInWithApple | ${e.toString()}', e);
    }
  }

  /// currently only used for connecting facebook accounts to a rydr user
  /// also we only currently support this in the UI when in a personal workspace
  /// or when we don't yet have a workspace at all in which case we create a personal one on the server
  Future<bool> addProfileToRydrUser(
    String authProviderToken,
    String authProviderId,
  ) async {
    try {
      final BasicVoidResponse res =
          await AuthenticationService.instance().connectFacebook(
        authToken: authProviderToken,
        accountId: authProviderId,
        username: null,
      );

      if (res.error == null) {
        _logScreen('connect/connectedprofile');
        return true;
      } else {
        _log.e('addProfileToRydrUser', res.error);

        /// there was an issue connecting the account on our end
        //await signOut(ConnectProfileState.error, res.error.message);

        _logScreen('connect/error');
      }
    } catch (error, stackTrace) {
      AppErrorLogger.instance.reportError('Other', error, stackTrace);
      _log.e('addProfileToRydrUser', error);

      _logScreen('connect/errorprofile');

      rethrow;
    }

    return false;
  }

  Future<void> signOut(bool onPurpose, [String errorReason]) async {
    /// if on purpose, then we update auth state
    if (onPurpose) {
      setAuthState(AuthState.signingOut);

      /// log disconnect in analytics if this was on purpose
      /// and remove local preferences for last active profile/workspace
      await NotificationService.unsubscribe();
      await DeviceSettings.clearDeviceInfo();

      _logScreen('connect/logout');
    }

    /// if we have, or can load a valid firebase user, then explicitly
    /// sign them out from firebase as well
    if (_currentFirebaseUser != null ||
        await _firebaseAuth.currentUser() != null) {
      await _firebaseAuth.signOut();
      _currentFirebaseUser = null;
    }

    /// if we have a master user, then check what auth provider they were using
    /// and also issue a sign out on that provider
    if (appState.masterUser != null) {
      final AuthType authType = appState.masterUser.authType;

      if (authType == AuthType.Google) {
        await GoogleSignIn().signOut();
      }
    }

    /// clear app state users, workspaces, profiles, etc.
    appState.signOut();

    /// we're done, so set authstate back to idle
    setAuthState(AuthState.idle);

    _log.i('signOut | $errorReason');
  }

  /// once we've authenticated, meaning we have a valid firebase/rydr user
  /// then we can call this which will load existing workspaces for the master user
  /// and will try to check if we had a previously active ws/profile and set that
  /// or fall back to the first workspace and profile we find...
  Future<void> handleAuthenticated() async {
    final bool success = await loadWorkspaces();

    if (success) {
      /// we'll try and identify the workspace and profile to use,
      /// switch to, and then set as current in app statate
      Workspace _currentWorkspace;
      PublisherAccount _currentProfile;

      /// get a potentially last active workspace and user
      final DeviceInfo lastActive = await DeviceSettings.getDeviceInfo();

      /// if we have a last active workspace and profile then
      /// check if the user still has access to them from the list of workspaces
      /// and if so set the current workspace and profile to the last active ones
      if (lastActive.activeProfileId != null &&
          lastActive.activeWorkspaceId != null) {
        _currentWorkspace = appState.workspaces.firstWhere(
            (Workspace workspace) =>
                workspace.id == lastActive.activeWorkspaceId,
            orElse: () => null);

        /// if the workspace is valid, then check if the profile is also still a valid
        /// linked profile in this workspace
        if (_currentWorkspace != null) {
          /// set the identified workspace in appstate so that when we make
          /// the next call to check the linked profile we have the workspace in the header
          appState.setCurrentWorkspace(_currentWorkspace);

          /// will return either the publisheraccount or null
          /// indicating that its valid/linked or not
          _currentProfile =
              await WorkspacesService.getWorkspacePublisherAccount(
                  _currentWorkspace.id, lastActive.activeProfileId);
        }
      }

      /// if we don't have a current workspace, then use the users 'own' personal workspace as default
      /// and if we don't have a profile then use the first one from the current workspace we've identified
      /// NOTE! .publisher AccountInfo will only ever have a max of 3
      _currentWorkspace = _currentWorkspace == null
          ? appState.workspaces.firstWhere(
              (Workspace workspace) => workspace.type == WorkspaceType.Personal,
              orElse: () => appState.workspaces[0])
          : _currentWorkspace;

      _currentProfile = _currentProfile == null
          ? _currentWorkspace.hasLinkedPublishers
              ? _currentWorkspace.publisherAccountInfo[0]
              : null
          : _currentProfile;

      /// TODO: remove this
      _currentWorkspace.workspaceFeatures = -1;

      /// update app state with the current workspace
      appState.setCurrentWorkspace(_currentWorkspace);

      /// then, if we have a current profile, call 'switch' to switch to the current user which will triger loading some metrics and stuff
      /// and save the device info with the new/currently active profile and workspace
      if (_currentProfile != null) {
        await appState.switchProfile(_currentProfile.id);
      }
    }
  }

  /// set all workspaces for the given master user in app state
  /// and if we don't already have one set as current in app state, then use the first one
  Future<bool> loadWorkspaces() async {
    final WorkspacesResponse res = await WorkspacesService.getWorkspaces();

    /// if we were unable to load workspaces or don't have any, then return
    if (res.hasError || res.models == null || res.models.isEmpty) {
      return false;
    }

    /// fire-and-forget subscribe all profiles to messaging
    /// NOTE! not 100% neccessary but will 'speed up' changes on the server
    NotificationService.subscribe();

    /// update all workspaces in app state
    appState.setWorkspaces(res.models);

    /// set or refresh the current workspace
    appState.setCurrentWorkspace(res.models.firstWhere(
        (ws) => ws.id == appState.currentWorkspace?.id,
        orElse: () => res.models[0]));

    /// do a hard refresh of all linked profiles for the current workspace
    await WorkspacesService.getWorkspaceUserPublisherAccounts(
      appState.currentWorkspace.id,
      forceRefresh: true,
    );

    /// if the current workspace has a facebook token account, then also
    /// clear the facebook pages cache... its cached a bit on the server it seems
    /// so at least removing it locally will most of the time cause the reload to work right
    if (appState.currentWorkspace.hasFacebookToken) {
      PublisherAccountService.clearFacebookPages();
    }

    return true;
  }

  /// registers, or re-registers/connects a base user (apple, or goggle) firebase authencated user
  /// to RYDR, from there we can assume to have a valid current firebase user on the device and on our server
  Future<BasicVoidResponse> register({
    @required String firebaseToken,
    @required String firebaseId,
    String name,
    String avatar,
    String email,
    String username,
    String phone,
    bool isEmailVerified,
  }) async {
    final ApiResponse apiResponse = await AppApi.instance.post(
      'authentication/register',
      body: {
        "firebaseToken": firebaseToken,
        "firebaseId": firebaseId,
        "name": name,
        "avatar": avatar,
        "email": email,
        "username": username,
        "phone": phone,
        "isEmailVerified": isEmailVerified,
      },
    );

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// connect facebook token account (from facebook login flow) to existing firebase
  /// user active on the device - currentely we only support in the UI for when in personal workspace
  /// or when there's no workspace at all yet and we'll then create a personal one on the server
  Future<BasicVoidResponse> connectFacebook({
    @required String authToken,
    @required String accountId,
    String username,
  }) async {
    final ApiResponse apiResponse = await AppApi.instance.post(
      'facebook/connectuser',
      body: {
        "authToken": authToken,
        "accountId": accountId,
        "username": username,
      },
    );

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  void _onFirebaseAuthStateChanged(FirebaseUser firebaseUser) {
    _log.d('_onFirebaseAuthStateChanged | $firebaseUser');

    if (firebaseUser == null) {
      if (_currentFirebaseUser != null) {
        signOut(false, 'Firebase user invalidated');

        _logScreen('connect/firebaseusers/invalidated');
      }

      return;
    }

    if (_currentFirebaseUser == null) {
      _currentFirebaseUser = firebaseUser;
    }

    if (firebaseUser.uid != _currentFirebaseUser.uid) {
      signOut(
          false, 'Current firebase and updated firebase user state invalid');

      _logScreen('connect/firebaseusers/invalidstate');
      return;
    }
  }

  Future<void> _signIntoFirebase(AuthCredential credential,
      [String displayName]) async {
    try {
      final AuthResult authResult =
          await _firebaseAuth.signInWithCredential(credential);

      /// set the firebase user, update the firebase profile
      /// with some basic information we got back from apple auth
      _currentFirebaseUser = authResult.user;

      /// update displayname if we have one
      if (displayName != null) {
        await _currentFirebaseUser
            .updateProfile(UserUpdateInfo()..displayName = displayName);
      }

      /// the firebase user will be our master user
      /// pass along displayname if we have it (for apple right now...)
      appState.setMasterUser(
          PublisherAccount.fromFirebase(_currentFirebaseUser, displayName));
    } catch (error) {
      _log.e('_signIntoFirebase | ${error.hashCode}', error);

      setAuthState(AuthState.error);
    }

    /// connect firebase user with rydr if we have one
    if (_currentFirebaseUser != null) {
      await _connectFirebaseUserToRydr();
    }
  }

  Future<void> _connectFirebaseUserToRydr() async {
    try {
      /// this gets, and (if needed) refreshes the firebase token
      var idToken = await _currentFirebaseUser.getIdToken();

      var firstProviderData =
          (_currentFirebaseUser.providerData?.length ?? 0) <= 0
              ? null
              : _currentFirebaseUser.providerData.first;

      final BasicVoidResponse res = await register(
        firebaseId: _currentFirebaseUser.uid,
        firebaseToken: idToken.token,
        name:
            _currentFirebaseUser.displayName ?? firstProviderData?.displayName,
        avatar: _currentFirebaseUser.photoUrl ?? firstProviderData?.photoUrl,
        email: _currentFirebaseUser.email ?? firstProviderData?.email,
        phone: _currentFirebaseUser.phoneNumber,
        isEmailVerified: _currentFirebaseUser.isEmailVerified,
      );

      if (res.error == null) {
        /// finally, we are connected and can update the auth state to reflect that
        setAuthState(AuthState.authenticated);

        _logScreen('connect/connected');
      } else {
        setAuthState(AuthState.error);

        /// there was an issue connecting the account on our end
        await signOut(false, res.error.message);

        _logScreen('connect/error');
      }
    } catch (error, stackTrace) {
      setAuthState(AuthState.error);

      AppErrorLogger.instance.reportError('Other', error, stackTrace);
      _log.e('_connectFirebaseUserToRydr', error);

      await signOut(false, error.toString());

      _logScreen('connect/error');

      rethrow;
    }
  }

  void _logScreen(String path) => AppAnalytics.instance.logScreen(path);
}
