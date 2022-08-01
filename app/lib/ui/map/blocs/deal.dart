import 'package:rxdart/subjects.dart';

class MapDealBloc {
  final _showActions = BehaviorSubject<bool>.seeded(true);
  final _showBottomBar = BehaviorSubject<bool>.seeded(false);
  final _scrollFactor = BehaviorSubject<double>();

  dispose() {
    _showActions.close();
    _showBottomBar.close();
    _scrollFactor.close();
  }

  BehaviorSubject<bool> get showActions => _showActions.stream;
  BehaviorSubject<bool> get showBottomBar => _showBottomBar.stream;
  BehaviorSubject<double> get scrollFactor => _scrollFactor.stream;

  void setShowActions(bool val) => _showActions.sink.add(val);
  void setShowBottomBar(bool val) => _showBottomBar.sink.add(val);
  void setScrollFactor(double val) => _scrollFactor.sink.add(val);
}
