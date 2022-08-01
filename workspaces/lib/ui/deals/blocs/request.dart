import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/responses/base.dart';
import 'package:rydrworkspaces/models/responses/deal.dart';
import 'package:rydrworkspaces/models/responses/publisher_media.dart';
import 'package:rydrworkspaces/services/deal.dart';
import 'package:rydrworkspaces/services/deal_request.dart';

class RequestBloc {
  final _dealResponse = BehaviorSubject<DealResponse>();
  final _dealRequestResponse = BehaviorSubject<BaseResponse>();
  final _mediaResponse = BehaviorSubject<PublisherMediaResponse>();

  dispose() {
    _dealResponse.close();
    _dealRequestResponse.close();
    _mediaResponse.close();
  }

  BehaviorSubject<DealResponse> get dealResponse => _dealResponse.stream;
  BehaviorSubject<BaseResponse> get dealRequestResponse =>
      _dealRequestResponse.stream;
  BehaviorSubject<PublisherMediaResponse> get mediaResponse =>
      _mediaResponse.stream;

  void loadRequest(
    Deal deal,
    int dealId,
    int publisherAccountId,
  ) async {
    if (deal != null) {
      _dealResponse.sink.add(
        DealResponse(deal, null),
      );
    } else {
      _dealResponse.sink.add(await DealService.getDeal(
        dealId,
        requestedPublisherAccountId: publisherAccountId,
      ));
    }
  }

  Future<BaseResponse> sendRequest(Deal deal) async {
    final BaseResponse res = await DealRequestService.addRequest(deal.id);

    if (res.error == null) {
      ///appState.handleRequestStatusChange(
      ///deal,
      ///DealRequestStatus.requested,
      ///);
    }

    _dealRequestResponse.sink.add(res);

    return res;
  }

  Future<bool> declineRequest(Deal deal) async =>
      await _updateRequest(deal, DealRequestStatus.denied);

  Future<bool> redeem(Deal deal) async =>
      await _updateRequest(deal, DealRequestStatus.redeemed);

  Future<bool> markCancelled(Deal deal) async =>
      await _updateRequest(deal, DealRequestStatus.cancelled, "RYDR expired");

  Future<bool> markDelinquent(Deal deal) async => await _updateRequest(
      deal, DealRequestStatus.delinquent, "Request marked as delinquent");

  Future<bool> addTimeToComplete(Deal deal) async {
    /// adding time extends the completion date by the default number of days, we do this by first
    /// calculating the expiration date from today, then getting the diff between when it was first marked redeemed
    /// and then date the total days between the two and make that the new days in which the request is delinquent
    final int newCompletionDays = deal
        .request.lastStatusChange.occurredOnDateTime
        .difference(deal.request.newCompletionDeadline)
        .inDays
        .abs();

    final BaseResponse res = await DealRequestService.updateDaysUntilDelinquent(
      deal,
      newCompletionDays,
    );

    return res.error == null;
  }

  Future<bool> _updateRequest(Deal deal, DealRequestStatus toStatus,
      [String reason]) async {
    final BaseResponse res = await DealRequestService.updateRequestStatus(
      deal,
      toStatus,
      reason: reason,
    );

    if (res.error == null) {
      //appState.handleRequestStatusChange(
      //deal,
      //toStatus,
      //);
    }

    _dealRequestResponse.sink.add(res);

    return res.error == null;
  }
}
