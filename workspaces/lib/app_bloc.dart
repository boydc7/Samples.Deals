import 'package:rxdart/subjects.dart';

enum AppPageState {
  loading,
  errorInternet,
  errorTimeout,
  errorServer,
  initialized,
}

class AppBloc {
  final _state = BehaviorSubject<AppPageState>.seeded(AppPageState.loading);

  dispose() {
    _state.close();
  }

  Stream<AppPageState> get state => _state.stream;

  void initUser() {}
}
