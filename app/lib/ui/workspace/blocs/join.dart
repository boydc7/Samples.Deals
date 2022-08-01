import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/services/workspaces.dart';

enum WorkspaceJoinResult {
  Sent,
  AlreadyJoined,
  IsOwner,
  InvalidToken,
  OtherError,
}

enum WorkspaceJoinPageState {
  Idle,
  Sending,
  Error,
  Sent,
}

class WorkspaceJoinBloc {
  final _canSendRequest = BehaviorSubject<bool>.seeded(false);
  final _pageState = BehaviorSubject<WorkspaceJoinPageState>.seeded(
      WorkspaceJoinPageState.Idle);

  dispose() {
    _canSendRequest.close();
    _pageState.close();
  }

  Stream<bool> get canSendRequest => _canSendRequest.stream;
  Stream<WorkspaceJoinPageState> get pageState => _pageState.stream;

  void setCanSendRequest(bool val) => _canSendRequest.sink.add(val);
  void setPageState(WorkspaceJoinPageState state) => _pageState.sink.add(state);

  Future<WorkspaceJoinResult> sendRequest(String code) async {
    setPageState(WorkspaceJoinPageState.Sending);

    await Future.delayed(Duration(milliseconds: 1500));

    final res = await WorkspacesService.requestAccess(code.toUpperCase());

    if (res.error == null) {
      /// successfully sent - set state accordingly and return sent result as well
      setPageState(WorkspaceJoinPageState.Sent);

      AppAnalytics.instance.logScreen('workspace/join/requested');

      return WorkspaceJoinResult.Sent;
    } else {
      /// error state
      setPageState(WorkspaceJoinPageState.Error);

      try {
        /// NOTE: These errors are not right yet, seems owner and invalid token are combined
        /// or maybe even the user already part error is the same as well? ask chad
        if (res.error.response.data['responseStatus']['message']
                .toString()
                .indexOf('InviteToken must be valid') >
            -1) {
          return WorkspaceJoinResult.InvalidToken;
        } else if (res.error.response.data['responseStatus']['message']
                .toString()
                .indexOf('workspace owner') >
            -1) {
          return WorkspaceJoinResult.IsOwner;
        } else if (res.error.response.data['responseStatus']['message']
                .toString()
                .indexOf('already part of') >
            -1) {
          return WorkspaceJoinResult.AlreadyJoined;
        } else {
          return WorkspaceJoinResult.OtherError;
        }
      } catch (e) {
        return WorkspaceJoinResult.OtherError;
      }
    }
  }
}
