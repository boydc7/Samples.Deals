import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/completion_details.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/messages.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/status_history.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/value.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/description.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/expiration_date.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/place.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/quantity.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_notes.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_type_listItem.dart';

class RequestCompleted extends StatelessWidget {
  final Deal deal;

  RequestCompleted(this.deal);

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        DealCompletionDetails(deal),
        _buildCompletedStatus(context),
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
    );
  }

  Widget _buildCompletedStatus(BuildContext context) {
    /// if this is the business, then they may still be able to navigate
    /// to the creators profile (if they're still within allowed time)
    final bool canViewProfile = deal.request.canSendMessages;

    /// determine when the last change was made and generate a display friendly string
    final String lastChange =
        deal.request.lastStatusChange.occurredOnDisplayAgo;

    return Column(
      children: <Widget>[
        ListTile(
          /*
          onTap: canViewProfile
              ? () => Navigator.of(context).pushNamed(
                    AppRouting.getProfileRoute(
                        deal.request.publisherAccount.id),
                    arguments: deal,
                  )
              : null,
              */
          contentPadding: EdgeInsets.symmetric(vertical: 8.0, horizontal: 16.0),
          leading: CircleAvatar(
            backgroundImage:
                NetworkImage(deal.request.publisherAccount.profilePicture),
          ),
          title: Text(
            '${deal.request.publisherAccount.userName}',
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
          subtitle: Text(
            'Completed $lastChange',
            style: Theme.of(context).textTheme.bodyText1.merge(
                  TextStyle(color: Theme.of(context).hintColor),
                ),
          ),
          trailing:
              canViewProfile ? Icon(Icons.chevron_right) : Container(width: 0),
        ),
      ],
    );
  }
}
