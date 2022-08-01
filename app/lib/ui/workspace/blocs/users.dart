import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/services/workspaces.dart';

class WorkspaceUsersBloc {
  final _usersResponse = BehaviorSubject<WorkspaceUsersResponse>();

  int _skip = 0;
  int _take = 50;
  bool _isLoading = false;
  bool _hasMore = false;

  dispose() {
    _usersResponse.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<WorkspaceUsersResponse> get usersResponse => _usersResponse.stream;

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadUsers({
    bool reset = false,
    bool forceRefresh = false,
  }) async {
    if (_isLoading) {
      return;
    }

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    WorkspaceUsersResponse res = await WorkspacesService.getWorkspaceUsers(
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
      final List<WorkspaceUser> existing = _usersResponse.value.models;
      existing.addAll(res.models);

      /// update the requests on the response before adding to stream
      res = WorkspaceUsersResponse.fromModels(existing);
    }

    _usersResponse.sink.add(res);

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

  Future<bool> removeUser(WorkspaceUser user) async {
    final res = await WorkspacesService.unlinkUserFromWorkspace(
        appState.currentWorkspace.id, user.userId);

    if (res.error == null) {
      AppAnalytics.instance.logScreen('workspace/users/deleted');
    }

    return res.error == null;
  }
}
