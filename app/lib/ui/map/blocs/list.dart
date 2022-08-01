import 'package:rxdart/subjects.dart';

/// TODO: either move more mapbloc stuff here or move this to mapbloc and rid the world of this file
class MapListBloc {
  final _showButtons = BehaviorSubject<bool>.seeded(true);

  double _extent;

  dispose() {
    _showButtons.close();
  }

  void setExtent(double val) => _extent = val;
  double get extent => _extent ?? 0;

  BehaviorSubject<bool> get showButtons => _showButtons.stream;

  void setShowButtons(bool val) => _showButtons.sink.add(val);
}
