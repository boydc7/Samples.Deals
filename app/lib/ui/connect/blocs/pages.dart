import 'dart:async';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/fbig_users.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/services/publisher_account.dart';
import 'package:rydr_app/services/workspaces.dart';
import 'package:rydr_app/ui/connect/utils.dart';
import 'package:rydr_app/models/workspace.dart';

class ConnectPagesBloc {
  static final ConnectPagesBloc _instance = ConnectPagesBloc._internal();

  /// this will be page list of currently 'linked' profiles to the current workspace
  static final _linkedProfilesResponse =
      BehaviorSubject<WorkspacePublisherAccountInfoResponse>();

  static final _unlinkedProfilesResponse = BehaviorSubject<FbIgUsersResponse>();
  static final _workspacesResponse = BehaviorSubject<WorkspacesResponse>();
  static final _showingConnectedProfiles = BehaviorSubject<bool>();
  static final _smallFab = BehaviorSubject<bool>();

  static final _loadingFb = BehaviorSubject<bool>();

  int _skip = 0;
  int _take = 50;
  bool _isLoading = false;
  bool _hasMore = false;

  ConnectPagesBloc._internal();

  static ConnectPagesBloc get instance => _instance;

  dispose() {
    _linkedProfilesResponse.close();
    _unlinkedProfilesResponse.close();
    _workspacesResponse.close();
    _showingConnectedProfiles.close();
    _smallFab.close();
    _loadingFb.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  BehaviorSubject<FbIgUsersResponse> get unlinkedProfilesResponse =>
      _unlinkedProfilesResponse.stream;
  BehaviorSubject<WorkspacesResponse> get workspacesResponse =>
      _workspacesResponse.stream;
  BehaviorSubject<bool> get showingConnectedProfiles =>
      _showingConnectedProfiles.stream;
  Stream<bool> get smallFab => _smallFab.stream;
  Stream<bool> get loadingFb => _loadingFb.stream;
  Stream<WorkspacePublisherAccountInfoResponse> get linkedProfilesResponse =>
      _linkedProfilesResponse.stream;

  void setShowSmallFab(bool value) {
    if (_smallFab?.value != value) {
      _smallFab.sink.add(value);
    }
  }

  void setShowingConnectedProfiles(bool value) {
    if (_showingConnectedProfiles?.value != value) {
      _showingConnectedProfiles.sink.add(value);
    }
  }

  Future<void> loadLinkedProfiles({
    bool reset = false,
    bool forceRefresh = false,
  }) async {
    if (_isLoading) {
      return;
    }

    /// null response will trigger loading ui if we're resetting
    if (reset) {
      _linkedProfilesResponse.sink.add(null);
    }

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    WorkspacePublisherAccountInfoResponse res =
        await WorkspacesService.getWorkspaceUserPublisherAccounts(
      appState.currentWorkspace.id,
      skip: _skip,
      take: _take,
      forceRefresh: forceRefresh,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _take;

    if (_skip > 0 && res.error == null) {
      final List<PublisherAccount> existing =
          _linkedProfilesResponse.value.models;
      existing.addAll(res.models);

      /// update the requests on the response before adding to stream
      res = WorkspacePublisherAccountInfoResponse.fromModels(existing);
    }

    if (!_linkedProfilesResponse.isClosed) {
      _linkedProfilesResponse.sink.add(res);
    }

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

  Future<void> loadPages([bool refresh = false]) async {
    FbIgUsersResponse res;

    /// if the current workspace we're in does not have a facebook token account
    /// linked to it then we won't make the call at all and return an empty result
    if (!appState.currentWorkspace.hasFacebookToken) {
      _unlinkedProfilesResponse.sink.add(FbIgUsersResponse.fromModels([]));
      return;
    }

    /// set loading
    _loadingFb.sink.add(true);

    /// if we're looking to refresh unlinked pages, then set the current stream
    /// of them to null so that we trigger the loading connection state in the stream builder
    if (refresh) {
      _unlinkedProfilesResponse.sink.add(null);
    }

    /// only team owner or if personal workspace do we need to call for unlinked fb pages yet
    if (appState.currentWorkspace.type == WorkspaceType.Personal ||
        appState.currentWorkspace.role == WorkspaceRole.Admin) {
      res = await PublisherAccountService.getFbIgBusinessAccounts(refresh);
    }

    /// if this is a team workspace, then remove any known creator accounts
    /// since we'll only allow connecting business accounts at this time
    if (appState.currentWorkspace.type == WorkspaceType.Team &&
        res != null &&
        res.models != null &&
        res.models.isNotEmpty) {
      res = FbIgUsersResponse.fromModels(List.from(res.models
        ..removeWhere((el) =>
            el.linkedAsAccountType != RydrAccountType.unknown &&
            el.linkedAsAccountType != RydrAccountType.business)));
    }

    _loadingFb.sink.add(false);
    _unlinkedProfilesResponse.sink.add(res);
  }

  /// will force a refresh of the list of accounts on facebook that have not been linked to the current master account
  /// and if we have teams enabled then will also refresh workspaces
  Future<void> refreshPages() async {
    /// log as screen
    AppAnalytics.instance.logScreen('connect/pages/refresh');

    /// refresh the linked pages
    await loadLinkedProfiles(
      reset: true,
      forceRefresh: true,
    );

    /// potentially wait for workspace refresh to complete (if enabled)
    /// which would affect/update the access reqeusts / notification count if we're
    /// currently viewing the team workspace list of profiles (with the gear icon/counter)
    await refreshWorkspaces();

    /// refresh pages, no need to wait here as it'll update stream when ready
    loadPages(true);
  }

  /// if enabled, this will refresh teams anytime we refresh pages (pull down or from FB options)
  Future<void> refreshWorkspaces() async {
    if (!appState.hasTeamsEnabled()) {
      return;
    }

    /// reload all workspaces for the current device/user
    final WorkspacesResponse res = await WorkspacesService.getWorkspaces();

    if (res.error == null) {
      /// update the app state with the list of all workspaces+profiles
      /// and re-add the current one back so that's updated as well
      appState.setWorkspaces(res.models);
      appState.setCurrentWorkspace(res.models
          .firstWhere((Workspace ws) => ws.id == appState.currentWorkspace.id));
    }

    /// log as screen
    AppAnalytics.instance.logScreen('connect/workspaces/refresh');

    _workspacesResponse.sink.add(res);
  }

  void toggleShowConnectedProfiles() {
    _showingConnectedProfiles.sink
        .add(showingConnectedProfiles.value == true ? false : true);
  }

  Future<void> signOut() async =>
      await AuthenticationService.instance().signOut(true, 'User disconnected');

  Future<bool> linkUser(
      PublisherAccount userToLink, RydrAccountType linkAsType) async {
    final PublisherAccount linkedUser = await ConnectUtils.linkUser(
      userToLink,
      PublisherType.facebook,
      linkAsType,
    );

    return linkedUser != null;
  }
}
