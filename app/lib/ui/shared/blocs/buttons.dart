import 'package:rxdart/rxdart.dart';

class ButtonsBloc {
  final _tapDown = BehaviorSubject<bool>.seeded(false);

  ButtonsBloc() {
    // _tapDown.sink.add(_tapActive);
  }

  dispose() {
    _tapDown.close();
  }

  BehaviorSubject<bool> get tapDown => _tapDown.stream;

  void buttonPress(bool tap) {
    _tapDown.sink.add(tap);
  }
}
