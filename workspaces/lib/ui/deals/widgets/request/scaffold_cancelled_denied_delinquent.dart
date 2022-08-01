import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/messages.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/status_history.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/value.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/place.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_notes.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_type_listItem.dart';

class RequestCancelledDeniedDelinquent extends StatelessWidget {
  final Deal deal;

  RequestCancelledDeniedDelinquent(this.deal);

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        _buildCancelledDeclinedDelinquentReason(context),
        DealMessages(deal),
        RequestStatusHistory(deal),
        DealReceiveTypeListItem(deal),
        DealReceiveNotes(deal),
        DealValue(deal),
        DealPlace(deal, false),
      ],
    );
  }

  Widget _buildCancelledDeclinedDelinquentReason(BuildContext context) {
    /// get the last change of status which will tell us potential reason
    /// as well as who made the decision to cancel this rydr
    final lastChangeById =
        deal.request.lastStatusChange.modifiedByPublisherAccountId;
    final status = deal.request.status;

    /// TODO:
    final int userId = 0;
    //final int userId = appState.currentProfile.id;
    final profileUsername = "USERNAME TODO";

    final String title =
        "${status == DealRequestStatus.delinquent ? 'Marked delinquent' : status == DealRequestStatus.cancelled ? 'Cancelled' : 'Declined'} by ${lastChangeById == userId ? profileUsername : deal.request.publisherAccount.userName}";

    /// TODO:
    //final lastChangeReason = deal.request.lastStatusChange.reason ??
    //  "${lastChangeById == userId ? appState.currentProfile.userName : isBusiness ? deal.request.publisherAccount.userName : deal.publisherAccount.userName} did not give a reason for ${status == DealRequestStatus.delinquent ? 'marking it delinquent' : status == DealRequestStatus.cancelled ? 'cancelling' : 'declining'}";
    final lastChangeReason = "TO DO";

    return Column(
      children: <Widget>[
        SizedBox(height: 12.0),
        Row(
          mainAxisAlignment: MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(width: 72, height: 40, child: Icon(Icons.close)),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(height: 4.0),
                  Text(title, style: Theme.of(context).textTheme.bodyText2),
                  SizedBox(height: 6.0),
                  Padding(
                    padding: EdgeInsets.only(right: 16.0),
                    child: Text(
                      lastChangeReason,
                      style: Theme.of(context).textTheme.bodyText1.merge(
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
