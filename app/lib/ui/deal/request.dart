import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/ui/deal/blocs/request.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_cancelled_denied_delinquent.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_complete.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_completed.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_inprogress_redeemed.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_redeem.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_requested_invited.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold_update.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

/// This is a "router" page, loading the request
/// and if successful we'll send to the status-specific scaffold page
class RequestPage extends StatefulWidget {
  /// if we're coming from the map page as an influencer
  /// we already have a deal loaded and just pass it to this ui
  final Deal deal;

  /// if we don't have a deal then we'll have an id and possibly
  /// a publisher account id of which (as a business) we want to get a request for
  final int dealId;
  final int publisherAccountId;

  /// we can override which page to send the user to
  /// by checking the sendToRequestStatus if available
  final DealRequestStatus sendToRequestStatus;

  RequestPage({
    this.deal,
    this.dealId,
    this.publisherAccountId,
    this.sendToRequestStatus,
  });

  @override
  _RequestPageState createState() => _RequestPageState();
}

class _RequestPageState extends State<RequestPage> {
  final RequestBloc _bloc = RequestBloc();

  ThemeData _theme;
  bool _darkMode;

  @override
  void initState() {
    _bloc.loadRequest(
      widget.deal,
      widget.dealId,
      widget.publisherAccountId,
    );
    super.initState();
  }

  @override
  void dispose() {
    _bloc.dispose();
    super.dispose();
  }

  Future<void> refresh() => _bloc.loadRequest(
        null,
        widget.deal != null ? widget.deal.id : widget.dealId,
        widget.deal != null
            ? widget.deal.request.publisherAccount.id
            : widget.publisherAccountId,
      );

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    return StreamBuilder<DealResponse>(
      stream: _bloc.dealResponse,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return _buildLoadingBody();
        } else if (snapshot.data.error != null) {
          return _buildErrorBody(snapshot.data);
        } else {
          final DealRequestStatus status = snapshot.data.model?.request?.status;

          /// if we have a status where we want to send the user to
          /// then act on that first, otherwise redirect to the right request scaffold
          /// based on what the current status of the request is
          ///
          /// NOTE! when looking to sendToRequestStatus is 'completed' then we'll also want to make sure
          /// that the request is in a valid state to be completed which is either inProgress or redeemed
          return widget.sendToRequestStatus == DealRequestStatus.denied ||
                  widget.sendToRequestStatus == DealRequestStatus.cancelled ||
                  widget.sendToRequestStatus == DealRequestStatus.inProgress
              ? RequestUpdatePage(
                  snapshot.data.model,
                  widget.sendToRequestStatus,
                )
              : widget.sendToRequestStatus == DealRequestStatus.redeemed
                  ? RequestRedeemPage(snapshot.data.model)
                  : widget.sendToRequestStatus == DealRequestStatus.completed &&
                          (status == DealRequestStatus.inProgress ||
                              status == DealRequestStatus.redeemed)
                      ? RequestCompletePage(snapshot.data.model)
                      : status == DealRequestStatus.inProgress ||
                              status == DealRequestStatus.redeemed
                          ? RequestInProgressPage(snapshot.data.model, refresh)
                          : status == DealRequestStatus.completed
                              ? RequestCompletedPage(
                                  snapshot.data.model, refresh)
                              : status == DealRequestStatus.denied ||
                                      status == DealRequestStatus.cancelled ||
                                      status == DealRequestStatus.delinquent
                                  ? RequestCancelledDeniedDelinquentPage(
                                      snapshot.data.model, refresh)
                                  : RequestRequestedInvitedPage(
                                      snapshot.data.model, refresh);
        }
      },
    );
  }

  Widget _buildLoadingBody() => Scaffold(
        backgroundColor:
            _darkMode ? _theme.scaffoldBackgroundColor : AppColors.white,
        body: ListView(
          children: <Widget>[LoadingDetailsShimmer()],
        ),
      );

  Widget _buildErrorBody(DealResponse dealResponse) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          backgroundColor:
              _darkMode ? _theme.appBarTheme.color : AppColors.white,
        ),
        backgroundColor:
            _darkMode ? _theme.scaffoldBackgroundColor : AppColors.white50,
        body: RetryError(
          onRetry: () => _bloc.loadRequest(
            widget.deal,
            widget.dealId,
            widget.publisherAccountId,
          ),
          error: dealResponse.error,
        ),
      );
}
