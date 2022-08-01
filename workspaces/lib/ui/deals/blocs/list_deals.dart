import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/list_page_arguments.dart';
import 'package:rydrworkspaces/models/requests/deals.dart';
import 'package:rydrworkspaces/models/responses/deals.dart';
import 'package:rydrworkspaces/services/deals.dart';

class ListBloc {
  final _filterArgs = BehaviorSubject<ListPageArguments>();
  final _dealsResponse = BehaviorSubject<DealsResponse>();
  final _smallFab = BehaviorSubject<bool>();

  int _skip = 0;
  int _take = 25;
  bool _isLoading = false;
  bool _hasMore = false;

  dispose() {
    _filterArgs.close();
    _dealsResponse.close();
    _smallFab.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<ListPageArguments> get filterArgs => _filterArgs.stream;
  Stream<DealsResponse> get dealsResponse => _dealsResponse.stream;
  Stream<bool> get smallFab => _smallFab.stream;

  void setShowSmallFab(bool value) {
    if (_smallFab?.value != value) {
      _smallFab.sink.add(value);
    }
  }

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadList(ListPageArguments args, [bool reset = false]) async {
    if (_isLoading) {
      return;
    }

    /// null response will trigger loading ui if we're resetting
    if (reset) {
      _dealsResponse.sink.add(null);
    }

    /// update args
    _filterArgs.sink.add(args);

    /// set loading flag and either reset or use existing skip
    _isLoading = true;
    _skip = reset ? 0 : _skip;

    DealsResponse res = await DealsService.queryDeals(
      request: _buildRequest(args, reset),
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore =
        res.deals != null && res.deals.isNotEmpty && res.deals.length == _take;

    if (_skip > 0 && res.error == null) {
      final List<Deal> existing = _dealsResponse.value.deals;
      existing.addAll(res.deals);

      /// update the requests on the response before adding to stream
      res = DealsResponse(existing, null);
    }

    if (!_dealsResponse.isClosed) {
      _dealsResponse.sink.add(res);
    }

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

/*
  /// re-active a paused deal from the list
  Future<bool> reactivateDeal(Deal deal) async {
    final DealSaveResponse res = await DealService.saveDeal(Deal()
      ..id = deal.id
      ..status = DealStatus.published);

    if (res.error == null) {
      final int idx = _indexOfDeal(deal.id);
      final Deal origDeal = _origDeal(idx);

      if (origDeal != null) {
        final List<Deal> updatedDeals = List.from(_dealsResponse.value.deals)
          ..replaceRange(
              idx, idx + 1, [origDeal..status = DealStatus.published]);

        _dealsResponse.sink.add(DealsResponse(updatedDeals, null));
      }
    }

    return res.error == null;
  }

  /// archive a deal from the list
  Future<bool> archiveDeal(Deal deal) async {
    final DealSaveResponse res = await DealService.saveDeal(Deal()
      ..id = deal.id
      ..status = DealStatus.completed);

    if (res.error == null) {
      final int idx = _indexOfDeal(deal.id);
      final Deal origDeal = _origDeal(idx);

      if (origDeal != null) {
        final List<Deal> updatedDeals = List.from(_dealsResponse.value.deals)
          ..removeRange(idx, idx + 1);

        _dealsResponse.sink.add(DealsResponse(updatedDeals, null));
      }
    }

    return res.error == null;
  }
  */

  int _indexOfDeal(int dealId) =>
      _dealsResponse.value.deals.indexWhere((Deal d) => d.id == dealId);

  Deal _origDeal(int idx) => idx > -1 ? _dealsResponse.value?.deals[idx] : null;

  DealsRequest _buildRequest(ListPageArguments args, bool refresh) =>
      DealsRequest(
        skip: _skip,
        take: _take,
        status: args.filterDealStatus != null ? args.filterDealStatus : null,
        dealId: args.filterDealId != null ? args.filterDealId : null,
        dealPublisherAccountId: args.filterDealPublisherAccountId != null
            ? args.filterDealPublisherAccountId
            : null,
        dealRequestPublisherAccountId:
            args.filterDealRequestPublisherAccountId != null
                ? args.filterDealRequestPublisherAccountId
                : null,
        requestsQuery: false,
        requestStatus:
            args.filterRequestStatus != null ? args.filterRequestStatus : null,
        sort: args.sortDeals != null
            ? args.sortDeals
            : args.isRequests ? null : DealSort.newest,
        refresh: refresh,
      );
}
