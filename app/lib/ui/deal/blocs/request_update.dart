import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/services/deal_request.dart';

/// this is used both on the request update page
/// and in the bottom bar from where a business can decline a request
class RequestUpdateBloc {
  final _showingTextField = BehaviorSubject<bool>();

  Deal _deal;
  DealRequestStatus _statusToUpdateTo;

  RequestUpdateBloc(Deal deal, DealRequestStatus statusToUpdateTo) {
    _deal = deal;
    _statusToUpdateTo = statusToUpdateTo;

    /// show the text field if we have approval notes
    /// or when we're cancelling or declining
    _showingTextField.sink.add(deal.approvalNotes == null &&
            statusToUpdateTo == DealRequestStatus.denied ||
        statusToUpdateTo == DealRequestStatus.cancelled);
  }

  dispose() {
    _showingTextField.close();
  }

  Stream<bool> get showingTextField => _showingTextField.stream;

  void setShowingTextField(bool value) => _showingTextField.sink.add(value);

  /// updates a request to either cancelled, denied, inProgress
  Future<BaseResponse> updateRequest(String notes) async {
    /// update deal requests with requested status and optional "reason"
    final BaseResponse res = await DealRequestService.updateRequestStatus(
      _deal,
      _statusToUpdateTo,
      reason: notes.length > 0 ? notes : null,
    );

    if (res.error == null) {
      appState.handleRequestStatusChange(
        _deal,
        _statusToUpdateTo,
      );
    }

    return res;
  }
}
