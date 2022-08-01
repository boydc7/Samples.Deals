import 'dart:async';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/services/public_api.dart';

class ConnectHelpBloc {
  final _page = BehaviorSubject<int>();

  String _username;
  String get username => _username;

  bool _isPrivate = false;
  bool get isPrivate => _isPrivate;
  bool _isBusiness = false;
  bool get isBusiness => _isBusiness;

  dispose() {
    _page.close();
  }

  BehaviorSubject<int> get page => _page.stream;
  void setPage(int page) => _page.sink.add(page);

  Future<bool> lookupHandle(String username) async {
    _username = username;

    final BizInfoResult bizInfoResult =
        await PublicApiService.getIgBizInfo(username);

    if (bizInfoResult != null) {
      _isPrivate = bizInfoResult.isPrivate;
      _isBusiness = bizInfoResult.isBusiness;

      return true;
    }

    return false;
  }
}
