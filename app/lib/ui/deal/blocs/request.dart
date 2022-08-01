import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/deal.dart';
import 'package:rydr_app/services/deal_request.dart';
import 'package:rydr_app/services/location.dart';

class RequestBloc {
  final _dealResponse = BehaviorSubject<DealResponse>();
  final _dealRequestResponse = BehaviorSubject<BaseResponse>();
  final _mediaResponse = BehaviorSubject<PublisherMediasResponse>();
  final _fadeOutStatus = BehaviorSubject<bool>();
  final _ticketRightSideUp = BehaviorSubject<bool>.seeded(true);
  final _scrollOffset = BehaviorSubject<double>();

  dispose() {
    _dealResponse.close();
    _dealRequestResponse.close();
    _mediaResponse.close();
    _fadeOutStatus.close();
    _ticketRightSideUp.close();
    _scrollOffset.close();
  }

  BehaviorSubject<DealResponse> get dealResponse => _dealResponse.stream;
  BehaviorSubject<BaseResponse> get dealRequestResponse =>
      _dealRequestResponse.stream;
  BehaviorSubject<PublisherMediasResponse> get mediaResponse =>
      _mediaResponse.stream;
  BehaviorSubject<bool> get fadeOutStatus => _fadeOutStatus.stream;
  BehaviorSubject<bool> get ticketRightSideUp => _ticketRightSideUp.stream;
  BehaviorSubject<double> get scrollOffset => _scrollOffset.stream;

  Future<void> loadRequest(
    Deal deal,
    int dealId,
    int publisherAccountId,
  ) async {
    if (deal != null) {
      _dealResponse.sink.add(
        DealResponse.fromModel(deal),
      );
    } else {
      _dealResponse.sink.add(await DealService.getDeal(
        dealId,
        requestedPublisherAccountId: publisherAccountId,
        userLatitude:
            appState.lastLatLng != null ? appState.lastLatLng.latitude : null,
        userLongitude:
            appState.lastLatLng != null ? appState.lastLatLng.longitude : null,
      ));
    }
  }

  Future<BaseResponse> sendRequest(Deal deal) async {
    final BaseResponse res = await DealRequestService.addRequest(deal.id);

    if (res.error == null) {
      appState.handleRequestStatusChange(
        deal,
        DealRequestStatus.requested,
      );
    }

    _dealRequestResponse.sink.add(res);

    return res;
  }

  Future<bool> acceptInvite(Deal deal) async =>
      await _updateRequest(deal, DealRequestStatus.inProgress);

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

  void setScrollOffset(double value) {
    _scrollOffset.sink.add(value);

    if (value <= 0 && _fadeOutStatus?.value != true) {
      _fadeOutStatus.sink.add(true);
    } else if (value > 0 && _fadeOutStatus?.value != false) {
      _fadeOutStatus.sink.add(false);
    }
  }

  void setTicketRightSideUp(bool value) => _ticketRightSideUp.sink.add(value);

  Future<bool> _updateRequest(Deal deal, DealRequestStatus toStatus,
      [String reason]) async {
    /// attempt to get the users current location which we can store
    /// with the redeem request status update
    final CurrentLocationResponse locationResponse =
        await LocationService.getInstance().getCurrentLocation();

    final BaseResponse res = await DealRequestService.updateRequestStatus(
      deal,
      toStatus,
      usersCurrentLatitude: locationResponse?.position?.target?.latitude,
      usersCurrentLongitude: locationResponse?.position?.target?.longitude,
      reason: reason,
    );

    if (res.error == null) {
      appState.handleRequestStatusChange(
        deal,
        toStatus,
      );
    }

    _dealRequestResponse.sink.add(res);

    return res.error == null;
  }

  Future<String> getExternalReportUrl(Deal deal) async {
    final StringIdResponse res =
        await DealRequestService.getRequestExternalId(deal);

    if (res.error == null) {
      return Uri.parse('${AppConfig.instance.appHost}?xr=${res.id}').toString();
    } else {
      return null;
    }
  }
}
