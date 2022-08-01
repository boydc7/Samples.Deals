import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/services/workspaces.dart';

class WorkspaceUsersProfilesBloc {
  final _profilesAssignedResponse =
      BehaviorSubject<WorkspacePublisherAccountInfoResponse>();
  final _profilesToAssignResponse =
      BehaviorSubject<WorkspacePublisherAccountInfoResponse>();

  int _skipAssigned = 0;
  int _takeAssigned = 100;
  int _skipToAssign = 0;
  int _takeToAssign = 100;
  bool _isLoadingAssigned = false;
  bool _hasMoreAssigned = false;
  bool _isLoadingToAssign = false;
  bool _hasMoreToAssign = false;

  dispose() {
    _profilesAssignedResponse.close();
    _profilesToAssignResponse.close();
  }

  bool get hasMoreAssigned => _hasMoreAssigned;
  bool get isLoadingAssigned => _isLoadingAssigned;
  bool get hasMoreToAssign => _hasMoreToAssign;
  bool get isLoadingToAssign => _isLoadingToAssign;

  Stream<WorkspacePublisherAccountInfoResponse> get profilesAssignedResponse =>
      _profilesAssignedResponse.stream;
  Stream<WorkspacePublisherAccountInfoResponse> get profilesToAssignResponse =>
      _profilesToAssignResponse.stream;

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadProfilesAssigned(
    int userId, {
    bool reset = false,
    bool forceRefresh = false,
  }) async {
    if (_isLoadingAssigned) {
      return;
    }

    /// set loading flag and either reset or use existing skip
    _isLoadingAssigned = true;
    _skipAssigned = reset ? 0 : _skipAssigned;

    WorkspacePublisherAccountInfoResponse res =
        await WorkspacesService.getWorkspaceUserPublisherAccounts(
      appState.currentWorkspace.id,
      userId: userId,
      skip: _skipAssigned,
      take: _takeAssigned,
      forceRefresh: forceRefresh,
      rydrAccountType: RydrAccountType.business,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMoreAssigned = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _takeAssigned;

    if (_skipAssigned > 0 && res.error == null) {
      final List<PublisherAccount> existing =
          _profilesAssignedResponse.value.models;
      existing.addAll(res.models);

      /// Create new response
      res = WorkspacePublisherAccountInfoResponse.fromModels(existing);
    }

    _profilesAssignedResponse.sink.add(res);

    /// increment skip if we have no error, and set loading to false
    _skipAssigned = res.error == null && _hasMoreAssigned
        ? _skipAssigned + _takeAssigned
        : _skipAssigned;
    _isLoadingAssigned = false;
  }

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadProfilesToAssign(
    int userId, {
    bool reset = false,
    bool forceRefresh = false,
  }) async {
    if (_isLoadingToAssign) {
      return;
    }

    /// set loading flag and either reset or use existing skip
    _isLoadingToAssign = true;
    _skipToAssign = reset ? 0 : _skipToAssign;

    WorkspacePublisherAccountInfoResponse res =
        await WorkspacesService.getWorkspaceUserPublisherAccounts(
      appState.currentWorkspace.id,
      userId: userId,
      skip: _skipToAssign,
      take: _takeToAssign,
      unlinked: true,
      forceRefresh: forceRefresh,
      rydrAccountType: RydrAccountType.business,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMoreToAssign = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _takeToAssign;

    if (_skipToAssign > 0 && res.error == null) {
      final List<PublisherAccount> existing =
          _profilesToAssignResponse.value.models;
      existing.addAll(res.models);

      /// Create new response
      res = WorkspacePublisherAccountInfoResponse.fromModels(existing);
    }

    _profilesToAssignResponse.sink.add(res);

    /// increment skip if we have no error, and set loading to false
    _skipToAssign = res.error == null && _hasMoreToAssign
        ? _skipToAssign + _takeToAssign
        : _skipToAssign;
    _isLoadingToAssign = false;
  }

  Future<bool> linkProfileToUser(
      WorkspaceUser user, PublisherAccount profile) async {
    final res = await WorkspacesService.linkProfileToWorkspaceUser(
        appState.currentWorkspace.id, user.userId, profile.id);

    if (res.error == null) {
      /// if we don't have an error then take the existing response
      /// add the newly assigned profile to it, and add it back to the stream
      WorkspacePublisherAccountInfoResponse wsResponse =
          _profilesAssignedResponse.value;
      wsResponse.models.add(profile);

      _profilesAssignedResponse.sink.add(wsResponse);

      /// remove the assigned profile from list of unassigned
      WorkspacePublisherAccountInfoResponse wsToAssignResponse =
          _profilesToAssignResponse.value;
      wsToAssignResponse.models
          .removeWhere((PublisherAccount account) => account.id == profile.id);

      _profilesToAssignResponse.sink.add(wsToAssignResponse);

      AppAnalytics.instance.logScreen('workspace/users/profiles/linked');
    }

    return res.error == null;
  }

  Future<bool> unlinkProfileFromUser(
      WorkspaceUser user, PublisherAccount profile) async {
    final res = await WorkspacesService.unlinkProfileFromWorkspaceUser(
        appState.currentWorkspace.id, user.userId, profile.id);

    if (res.error == null) {
      /// if we don't have an error then take the existing response
      /// remove the newly assigned profile from it, and add it back to the stream
      WorkspacePublisherAccountInfoResponse wsResponse =
          _profilesAssignedResponse.value;
      wsResponse.models
          .removeWhere((PublisherAccount account) => account.id == profile.id);

      _profilesAssignedResponse.sink.add(wsResponse);

      AppAnalytics.instance.logScreen('workspace/users/profiles/unlinked');
    }

    return res.error == null;
  }
}
