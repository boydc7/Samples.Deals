import 'dart:async';

import 'package:rxdart/subjects.dart';

class HomeBloc {
  final _tabIndex = BehaviorSubject<int>();

  dispose() {
    _tabIndex.close();
  }

  void setTab(int index) => _tabIndex.sink.add(index);

  Stream<int> get tabIndex => _tabIndex.stream;
}
