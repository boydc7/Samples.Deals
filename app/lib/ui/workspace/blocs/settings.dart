import 'dart:async';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/services/workspaces.dart';

class WorkspaceSettingsBloc {
  final _workspaceResponse = BehaviorSubject<WorkspaceResponse>();

  WorkspaceSettingsBloc() {
    _workspaceResponse.sink
        .add(WorkspaceResponse.fromModel(appState.currentWorkspace));
  }

  dispose() {
    _workspaceResponse.close();
  }

  BehaviorSubject<WorkspaceResponse> get workspaceResponse =>
      _workspaceResponse.stream;

  Future<void> load([bool forceRefresh = false]) async {
    final WorkspaceResponse workspaceResponse =
        await WorkspacesService.getWorkspace(
      appState.currentWorkspace.id,
      forceRefresh,
    );

    /// if successful, update the workspace in app state so we would update
    /// things like access request which change the notification badge, etc.
    if (workspaceResponse.error == null && workspaceResponse.model != null) {
      appState.setCurrentWorkspace(workspaceResponse.model);
    }

    _workspaceResponse.sink.add(workspaceResponse);
  }

  Future<bool> leaveTeam() async {
    final BasicVoidResponse res =
        await WorkspacesService.unlinkUserFromWorkspace(
            appState.currentWorkspace.id, appState.currentProfile.id);

    return res.error == null;
  }
}
