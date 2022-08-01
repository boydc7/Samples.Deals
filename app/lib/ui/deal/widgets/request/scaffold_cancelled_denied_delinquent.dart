import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/widgets/request/messages.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold.dart';
import 'package:rydr_app/ui/deal/widgets/request/status_history.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_notes.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_type_listItem.dart';
import 'package:rydr_app/ui/deal/widgets/shared/brand.dart';
import 'package:rydr_app/ui/deal/widgets/shared/place.dart';
import 'package:rydr_app/ui/deal/widgets/request/value.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/state.dart';

class RequestCancelledDeniedDelinquentPage extends StatelessWidget {
  final Deal deal;
  final Function refresh;

  RequestCancelledDeniedDelinquentPage(this.deal, this.refresh);

  @override
  Widget build(BuildContext context) => RequestScaffold(
      deal,
      [
        FadeInTopBottom(
          10,
          Column(
            children: <Widget>[
              sectionDivider(context),
              _buildCancelledDeclinedDelinquentReason(context),
            ],
          ),
          350,
          begin: -20.0,
        ),
        sectionDivider(context),
        FadeInTopBottom(
          15,
          Column(
            children: <Widget>[
              SizedBox(height: 4.0),
              DealMessages(deal),
              RequestStatusHistory(deal),
              DealReceiveTypeListItem(deal),
              DealReceiveNotes(deal),
              DealValue(deal),
              DealBrand(deal),
              DealPlace(deal, false),
            ],
          ),
          350,
          begin: -20.0,
        ),
      ],
      this.refresh);

  Widget _buildCancelledDeclinedDelinquentReason(BuildContext context) {
    /// get the last change of status which will tell us potential reason
    /// as well as who made the decision to cancel this rydr
    final lastChangeById =
        deal.request.lastStatusChange.modifiedByPublisherAccountId;
    final status = deal.request.status;
    final bool isBusiness = appState.currentProfile.isBusiness;
    final int userId = appState.currentProfile.id;

    final String title =
        "${status == DealRequestStatus.delinquent ? 'Marked delinquent' : status == DealRequestStatus.cancelled ? 'Cancelled' : 'Declined'} by ${lastChangeById == userId ? appState.currentProfile.userName : isBusiness ? deal.request.publisherAccount.userName : deal.publisherAccount.userName}";

    final lastChangeReason = deal.request.lastStatusChange.reason ??
        "${lastChangeById == userId ? appState.currentProfile.userName : isBusiness ? deal.request.publisherAccount.userName : deal.publisherAccount.userName} did not give a reason for ${status == DealRequestStatus.delinquent ? 'marking it delinquent' : status == DealRequestStatus.cancelled ? 'cancelling' : 'declining'}";

    return Column(
      children: <Widget>[
        SizedBox(height: 12.0),
        Row(
          mainAxisAlignment: MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
                width: 72,
                height: 40,
                child: Icon(
                  AppIcons.timesCircle,
                  color: Theme.of(context).appBarTheme.iconTheme.color,
                )),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(height: 4.0),
                  Text(title, style: Theme.of(context).textTheme.bodyText1),
                  SizedBox(height: 6.0),
                  Padding(
                    padding: EdgeInsets.only(right: 16.0),
                    child: Text(
                      lastChangeReason,
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(height: 1.1),
                          ),
                    ),
                  )
                ],
              ),
            )
          ],
        ),
        SizedBox(height: 20.0),
      ],
    );
  }
}
