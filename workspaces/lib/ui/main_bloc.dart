import 'package:rxdart/subjects.dart';

class MainBloc {
  final _tabIndex = BehaviorSubject<int>.seeded(2);

  dispose() {
    _tabIndex.close();
  }

  void setTab(int index) => _tabIndex.sink.add(index);

  Stream<int> get tabIndex => _tabIndex.stream;
}
