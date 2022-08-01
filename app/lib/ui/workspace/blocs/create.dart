import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/services/workspaces.dart';

enum WorkspaceCreateState {
  Create,
  Creating,
  Error,
}

class WorkspaceCreateBloc {
  final _page =
      BehaviorSubject<WorkspaceCreateState>.seeded(WorkspaceCreateState.Create);
  final _canPreview = BehaviorSubject<bool>.seeded(false);
  final _canChooseAccounts = BehaviorSubject<bool>.seeded(false);
  final _onPage = BehaviorSubject<int>.seeded(0);
  final _name = BehaviorSubject<String>();
  final _creatingStep = BehaviorSubject<int>();

  dispose() {
    _page.close();
    _name.close();
    _canChooseAccounts.close();
    _onPage.close();
    _canPreview.close();
    _creatingStep.close();
  }

  Stream<WorkspaceCreateState> get page => _page.stream;
  Stream<bool> get canPreview => _canPreview.stream;
  Stream<bool> get canChooseAccounts => _canChooseAccounts.stream;
  Stream<int> get onPage => _onPage.stream;
  Stream<int> get creatingStep => _creatingStep.stream;

  String get name => _name.value;

  void setPage(int i) {
    _onPage.sink.add(i);
  }

  void setName(String name) {
    _name.sink.add(name);
    _checkCanChooseAccounts();
    _checkCanPreview();
  }

  void backToForm() => _page.sink.add(WorkspaceCreateState.Create);

  Future<bool> createWorkspace() async {
    _page.sink.add(WorkspaceCreateState.Creating);
    _creatingStep.sink.add(0);

    await Future.delayed(Duration(seconds: 2));

    _creatingStep.sink.add(1);

    await Future.delayed(Duration(seconds: 2));

    _creatingStep.sink.add(2);

    final WorkspaceResponse createRes = await WorkspacesService.createWorkspace(
      name,
      null,
    );

    if (createRes.error == null) {
      appState.workspaces.add(createRes.model);
      await appState.switchWorkspace(createRes.model);

      AppAnalytics.instance.logScreen('workspace/created');

      return true;
    } else {
      _page.sink.add(WorkspaceCreateState.Error);

      return false;
    }
  }

  void _checkCanChooseAccounts() => _canChooseAccounts.sink
      .add(_name.value != null && _name.value.length > 4);

  void _checkCanPreview() {
    _canPreview.sink.add(_name.value != null && _name.value.length > 4);
  }
}
