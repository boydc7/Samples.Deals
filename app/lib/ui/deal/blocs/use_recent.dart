import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/services/deals.dart';

class UseRecentBloc {
  final _recentDeals = BehaviorSubject<List<Deal>>();

  void dispose() {
    _recentDeals.close();
  }

  BehaviorSubject<List<Deal>> get recentDeals => _recentDeals.stream;

  Future<void> loadRecentDeals([bool forceRefresh = false]) async {
    final DealsResponse res = await DealsService.getRecentDeals(forceRefresh);

    return _recentDeals.sink.add(res.models);
  }

  /// for showing 'history' of most recent deal values for re-use
  /// we'll return back a distinct list based on the field we want to use
  List<String> recentDealsContent(String fieldToUse) {
    final List<Deal> deals = recentDeals.value ?? [];

    List<String> data = fieldToUse == 'description'
        ? deals.map((Deal d) => d.description).toSet().toList()
        : fieldToUse == 'receiveNotes'
            ? deals.map((Deal d) => d.receiveNotes).toSet().toList()
            : deals.map((Deal d) => d.approvalNotes).toSet().toList();

    return data..removeWhere((String el) => el == null || el.trim() == "");
  }
}
