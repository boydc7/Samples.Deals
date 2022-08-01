import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/responses/deal.dart';
import 'package:rydrworkspaces/ui/deals/blocs/request.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/scaffold_cancelled_denied_delinquent.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/scaffold_completed.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/scaffold_inprogress_redeemed.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/scaffold_requested_invited.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/title.dart';

class Request extends StatefulWidget {
  /// if we're coming from the map page as an influencer
  /// we already have a deal loaded and just pass it to this ui
  final Deal deal;

  /// if we don't have a deal then we'll have an id and possibly
  /// a publisher account id of which (as a business) we want to get a request for
  final int dealId;
  final int publisherAccountId;

  Request({
    this.deal,
    this.dealId,
    this.publisherAccountId,
  });

  @override
  _RequestState createState() => _RequestState();
}

class _RequestState extends State<Request> {
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
          final DealRequestStatus status = snapshot.data.deal?.request?.status;

          return Scaffold(
              appBar: AppBar(
                elevation: 0,
                backgroundColor: AppColors.white,
                iconTheme: IconThemeData(color: AppColors.black),
              ),
              body: Container(
                  padding: EdgeInsets.all(16),
                  child: ListView(
                    children: <Widget>[
                      RequestTitle(widget.deal),
                      status == DealRequestStatus.inProgress ||
                              status == DealRequestStatus.redeemed
                          ? RequestInProgress(snapshot.data.deal)
                          : status == DealRequestStatus.completed
                              ? RequestCompleted(snapshot.data.deal)
                              : status == DealRequestStatus.denied ||
                                      status == DealRequestStatus.cancelled ||
                                      status == DealRequestStatus.delinquent
                                  ? RequestCancelledDeniedDelinquent(
                                      snapshot.data.deal)
                                  : RequestRequestedInvited(snapshot.data.deal),
                    ],
                  )));
        }
      },
    );
  }

  Widget _buildLoadingBody() => Scaffold(
        backgroundColor:
            _darkMode ? _theme.scaffoldBackgroundColor : AppColors.white,
        body: ListView(
          children: <Widget>[Text("Loading")],
        ),
      );

  Widget _buildErrorBody(DealResponse dealResponse) => Scaffold(
      appBar: AppBar(
        backgroundColor: _darkMode ? _theme.appBarTheme.color : AppColors.white,
      ),
      backgroundColor:
          _darkMode ? _theme.scaffoldBackgroundColor : AppColors.white50,
      body: Text(dealResponse.error.message));
}
