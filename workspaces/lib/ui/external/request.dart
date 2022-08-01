import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/responses/deal.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/completion_details.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/creator_details.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/messages.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/status_history.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/value.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/description.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/expiration_date.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/place.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/quantity.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_notes.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_type_listItem.dart';
import 'package:rydrworkspaces/ui/external/blocs/request.dart';

class RequestReport extends StatefulWidget {
  final String id;

  RequestReport(this.id);

  @override
  _RequestReportState createState() => _RequestReportState();
}

class _RequestReportState extends State<RequestReport> {
  final RequestBloc _bloc = RequestBloc();

  @override
  void initState() {
    super.initState();

    print(widget.id);

    _bloc.loadReport(widget.id);
  }

  @override
  void dispose() {
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
        appBar: AppBar(
          automaticallyImplyLeading: false,
          backgroundColor: AppColors.white,
          elevation: 0,
        ),
        body: StreamBuilder<DealResponse>(
          stream: _bloc.dealResponse,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return _buildLoadingBody();
            } else if (snapshot.data.error != null) {
              return _buildErrorBody(snapshot.data);
            } else {
              final Deal deal = snapshot.data.deal;

              return Container(
                  padding: EdgeInsets.all(16),
                  child: ListView(
                    children: <Widget>[
                      DealCompletionDetails(deal),
                      RequestCreatorDetails(deal),
                      DealReceiveTypeListItem(deal),
                      DealMessages(deal),
                      RequestStatusHistory(deal),
                      DealReceiveNotes(deal),
                      DealDescription(deal),
                      DealQuantity(deal),
                      DealExpirationDate(deal),
                      DealValue(deal),
                      DealPlace(deal, false),
                    ],
                  ));
            }
          },
        ));
  }

  Widget _buildLoadingBody() => Scaffold(
        body: ListView(
          children: <Widget>[Text("Loading")],
        ),
      );

  Widget _buildErrorBody(DealResponse dealResponse) => Scaffold(
      appBar: AppBar(
        elevation: 0,
        backgroundColor: AppColors.white,
      ),
      body: Text(dealResponse.error.message));
}
