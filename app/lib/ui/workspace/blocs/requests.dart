import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/services/workspaces.dart';

class WorkspaceRequestsBloc {
  final _requestsResponse = BehaviorSubject<WorkspaceAccessRequestsResponse>();

  int _skip = 0;
  int _take = 50;
  bool _isLoading = false;
  bool _hasMore = false;

  dispose() {
    _requestsResponse.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<WorkspaceAccessRequestsResponse> get requestsResponse =>
      _requestsResponse.stream;

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadRequests({bool reset = false}) async {
    if (_isLoading) {
      return;
    }

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    WorkspaceAccessRequestsResponse res =
        await WorkspacesService.getAccessRequests(
      appState.currentWorkspace.id,
      skip: _skip,
      take: _take,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _take;

    if (_skip > 0 && res.error == null) {
      final List<WorkspaceAccessRequest> existing =
          _requestsResponse.value.models;
      existing.addAll(res.models);

      /// update the requests on the response before adding to stream
      res = WorkspaceAccessRequestsResponse.fromModels(existing);
    }

    _requestsResponse.sink.add(res);

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

  Future<bool> updateRequest(WorkspaceAccessRequest request,
      [bool decline = false]) async {
    var res;

    if (decline) {
      res = await WorkspacesService.deleteAccessRequest(
          appState.currentWorkspace.id, request.user.userId);
    } else {
      res = await WorkspacesService.linkUserToWorkspace(
          appState.currentWorkspace.id, request.user.userId);
    }

    if (res.error == null) {
      /// update the existing response by removing the request
      /// and adding it back onto the stream which will remove it from the listview
      WorkspaceAccessRequestsResponse existingResponse =
          _requestsResponse.value;
      existingResponse.models.removeWhere((WorkspaceAccessRequest req) =>
          req.user.userId == request.user.userId);

      _requestsResponse.sink.add(existingResponse);

      /// decrement the access requests count on the workspace in appstate
      appState.currentWorkspace.accessRequests =
          appState.currentWorkspace.accessRequests - 1;

      AppAnalytics.instance.logScreen(decline
          ? 'workspace/requests/declined'
          : 'workspace/requests/accepted');

      return true;
    } else {
      return false;
    }
  }
}
