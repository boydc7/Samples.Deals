import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/services/deal_request.dart';
import 'package:rydr_app/services/location.dart';

class RequestCompleteConfirmBloc {
  final _rating = BehaviorSubject<int>.seeded(0);

  void dispose() {
    _rating.close();
  }

  BehaviorSubject<int> get rating => _rating.stream;
  void setRating(int rating) => _rating.sink.add(rating);
  int get starRating => rating.value ?? 0;

  Future<bool> completeRequest(
    Deal deal,
    List<PublisherMedia> completionMedia,
    String notes,
  ) async {
    /// attempt to get the users current location which we can store
    /// with the redeem request status update
    final CurrentLocationResponse locationResponse =
        await LocationService.getInstance().getCurrentLocation();

    /// update deal requests with requested status and optional "reason"
    /// content which would be the input from the user in the notes field
    /// as well as list of media ids of seleted media for the completed request
    final BaseResponse res = await DealRequestService.updateRequestStatus(
      deal,
      DealRequestStatus.completed,
      reason: notes.length > 0
          ? starRating == 0
              ? "Rating Skipped: $notes"
              : "$starRating stars: $notes"
          : starRating == 0 ? "Rating Skipped" : "$starRating stars",
      completionMediaIds:
          completionMedia.map((PublisherMedia m) => m.mediaId).toList(),
      usersCurrentLatitude: locationResponse?.position?.target?.latitude,
      usersCurrentLongitude: locationResponse?.position?.target?.longitude,
    );

    if (res.error == null) {
      appState.handleRequestStatusChange(
        deal,
        DealRequestStatus.completed,
      );
    }

    return res.error == null;
  }
}
