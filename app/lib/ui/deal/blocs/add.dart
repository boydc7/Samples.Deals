import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/services/deals.dart';

class AddBloc {
  final _drafts = BehaviorSubject<DealsResponse>();

  void dispose() {
    _drafts.close();
  }

  Stream<DealsResponse> get drafts => _drafts.stream;

  Future<void> loadDrafts([bool forceRefresh = false]) async =>
      _drafts.sink.add(await DealsService.getRecentDrafts(forceRefresh));
}
