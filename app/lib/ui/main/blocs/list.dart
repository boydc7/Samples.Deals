import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_request.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/models/requests/deals.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/services/deal.dart';
import 'package:rydr_app/services/deals.dart';
import 'package:rydr_app/models/responses/places.dart';
import 'package:rydr_app/services/place.dart';

class ListBloc {
  final _filterArgs = BehaviorSubject<ListPageArguments>();
  final _dealsResponse = BehaviorSubject<DealsResponse>();
  final _places = BehaviorSubject<List<Place>>();
  final _smallFab = BehaviorSubject<bool>();

  int _skip = 0;
  int _take = 15;
  bool _isLoading = false;
  bool _hasMore = false;

  dispose() {
    _filterArgs.close();
    _dealsResponse.close();
    _places.close();
    _smallFab.close();
  }

  bool get hasMore => _hasMore;
  bool get isLoading => _isLoading;

  Stream<ListPageArguments> get filterArgs => _filterArgs.stream;
  Stream<DealsResponse> get dealsResponse => _dealsResponse.stream;
  Stream<List<Place>> get places => _places.stream;
  Stream<bool> get smallFab => _smallFab.stream;

  void setShowSmallFab(bool value) {
    if (_smallFab?.value != value) {
      _smallFab.sink.add(value);
    }
  }

  void loadPlaces() async {
    final PlacesResponse res =
        await PlaceService.getPublisherPlaces(appState.currentProfile.id);

    if (!_places.isClosed) {
      _places.sink.add(res.models ?? []);
    }
  }

  /// make this a 'future' to be compatible with refreshindicator list view widget
  Future<void> loadList(
    ListPageArguments args, {
    bool reset = false,
    bool forceRefresh = false,
  }) async {
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

    /// TODO: seems skip/take is not working as expected
    /// when i take 15 then do another take and skip 15 i get duplicates
    DealsResponse res = await DealsService.queryDeals(
      appState.currentProfile.isBusiness,
      request: _buildRequest(args, reset),
      forceRefresh: forceRefresh,
    );

    /// set the hasMore flag if we have >=take records coming back
    _hasMore = res.models != null &&
        res.models.isNotEmpty &&
        res.models.length == _take;

    if (_skip > 0 && res.error == null) {
      final List<Deal> existing = _dealsResponse.value.models;
      existing.addAll(res.models);

      /// update the requests on the response before adding to stream
      res = DealsResponse.fromModels(existing);
    }

    if (!_dealsResponse.isClosed) {
      _dealsResponse.sink.add(res);
    }

    /// increment skip if we have no error, and set loading to false
    _skip = res.error == null && _hasMore ? _skip + _take : _skip;
    _isLoading = false;
  }

  /// set new exipration date on an expired deal
  Future<bool> updateExpiration(Deal deal, DateTime expirationDate) async {
    final res = await DealService.updateExpirationDate(
      deal.id,
      expirationDate,
    );

    if (res.error == null) {
      final int idx = _indexOfDeal(deal.id);
      final Deal origDeal = _origDeal(idx);

      if (origDeal != null) {
        final List<Deal> updatedDeals = List.from(_dealsResponse.value.models)
          ..replaceRange(
              idx, idx + 1, [origDeal..expirationDate = expirationDate]);

        _dealsResponse.sink.add(DealsResponse.fromModels(updatedDeals));
      }
    }

    return res.error == null;
  }

  /// re-active a paused deal from the list
  Future<bool> reactivateDeal(Deal deal) async {
    final res = await DealService.updateStatus(
      deal.id,
      DealStatus.published,
    );

    if (res.error == null) {
      final int idx = _indexOfDeal(deal.id);
      final Deal origDeal = _origDeal(idx);

      if (origDeal != null) {
        final List<Deal> updatedDeals = List.from(_dealsResponse.value.models)
          ..replaceRange(
              idx, idx + 1, [origDeal..status = DealStatus.published]);

        _dealsResponse.sink.add(DealsResponse.fromModels(updatedDeals));
      }
    }

    return res.error == null;
  }

  /// archive or delete a deal from the list
  Future<bool> archiveOrDeleteDeal(Deal deal, [bool delete = false]) async {
    BasicVoidResponse res;

    if (delete) {
      res = await DealService.deleteDeal(deal);
    } else {
      res = await DealService.updateStatus(
        deal.id,
        DealStatus.completed,
      );
    }

    if (res.error == null) {
      final int idx = _indexOfDeal(deal.id);
      final Deal origDeal = _origDeal(idx);

      if (origDeal != null) {
        final List<Deal> updatedDeals = List.from(_dealsResponse.value.models)
          ..removeRange(idx, idx + 1);

        _dealsResponse.sink.add(DealsResponse.fromModels(updatedDeals));
      }
    }

    return res.error == null;
  }

  /// on request list we subscribe to app state changes to a deal request
  /// if we get one then we call this which will look for and then remove
  /// the request from the existing list (e.g. when an invite is accepted we remove from the list of invites)
  void handleRequestChanged(DealRequestChange change) {
    final int idx = _indexOfDeal(change.deal.id);
    final Deal origDeal = _origDeal(idx);

    if (origDeal != null) {
      List<Deal> existingDeals = List.from(_dealsResponse.value.models);

      if (origDeal.request.status != change.toStatus) {
        existingDeals.removeRange(idx, idx + 1);

        _dealsResponse.sink.add(DealsResponse.fromModels(existingDeals));
      }
    }
  }

  int _indexOfDeal(int dealId) => _dealsResponse?.value != null
      ? _dealsResponse.value.models.indexWhere((Deal d) => d.id == dealId)
      : -1;

  Deal _origDeal(int idx) =>
      idx > -1 ? _dealsResponse.value?.models[idx] : null;

  DealsRequest _buildRequest(ListPageArguments args, bool refresh) =>
      DealsRequest(
        skip: _skip,
        take: _take,
        status: args.filterDealStatus != null ? args.filterDealStatus : null,
        dealId: args.filterDealId != null ? args.filterDealId : null,
        placeId: args.filterDealPlaceId,
        publisherAccountId: args.filterDealPublisherAccountId != null
            ? args.filterDealPublisherAccountId
            : null,
        dealRequestPublisherAccountId:
            args.filterDealRequestPublisherAccountId != null
                ? args.filterDealRequestPublisherAccountId
                : null,
        requestsQuery: args.isRequests,
        requestStatus:
            args.filterRequestStatus != null ? args.filterRequestStatus : null,
        sort: args.sortDeals != null
            ? args.sortDeals
            : args.isRequests ? null : DealSort.newest,
        includeExpired: args.includeExpired,
        refresh: refresh,
      );
}
