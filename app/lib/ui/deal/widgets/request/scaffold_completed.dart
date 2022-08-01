import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/ui/deal/widgets/request/completion_details.dart';
import 'package:rydr_app/ui/deal/widgets/request/messages.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_notes.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_type_listItem.dart';
import 'package:rydr_app/ui/deal/widgets/shared/brand.dart';
import 'package:rydr_app/ui/deal/widgets/shared/expiration_date.dart';
import 'package:rydr_app/ui/deal/widgets/shared/place.dart';
import 'package:rydr_app/ui/deal/widgets/request/value.dart';
import 'package:rydr_app/ui/deal/widgets/shared/quantity.dart';
import 'package:rydr_app/ui/deal/widgets/request/status_history.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

/// Viewing a "Completed" Request, both the business and the creator see this page
/// and we 'inject' this page from the main request.dart router file based on the request status == completed
class RequestCompletedPage extends StatelessWidget {
  final Deal deal;
  final Function refresh;

  RequestCompletedPage(this.deal, this.refresh);

  @override
  Widget build(BuildContext context) => RequestScaffold(
      deal,
      [
        Divider(height: 1),
        FadeInTopBottom(
          5,
          DealCompletionDetails(deal),
          350,
          begin: -20.0,
        ),
        FadeInTopBottom(
          10,
          _buildCompletedStatus(context),
          350,
          begin: -20.0,
        ),
        sectionDivider(context),
        FadeInTopBottom(
          15,
          Column(
            children: <Widget>[
              SizedBox(height: 4.0),
              DealReceiveTypeListItem(deal),
              DealReceiveNotes(deal),
              DealMessages(deal),
              RequestStatusHistory(deal),
              DealQuantity(deal),
              DealExpirationDate(deal),
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

  Widget _buildCompletedStatus(BuildContext context) {
    /// if this is the business, then they may still be able to navigate
    /// to the creators profile (if they're still within allowed time)
    final bool isBusiness = appState.currentProfile.isBusiness;
    final bool canViewProfile = isBusiness && deal.request.canSendMessages;

    /// determine when the last change was made and generate a display friendly string
    final String lastChange =
        deal.request.statusChanges.last.occurredOnDisplayAgo;

    return Column(
      children: <Widget>[
        ListTile(
          onTap: canViewProfile
              ? () => Navigator.of(context).pushNamed(
                    AppRouting.getProfileRoute(
                        deal.request.publisherAccount.id),
                    arguments: deal,
                  )
              : null,
          contentPadding: EdgeInsets.symmetric(vertical: 8.0, horizontal: 16.0),
          leading: UserAvatar(
            deal.request.publisherAccount,
          ),
          title: Text(
            isBusiness
                ? '${deal.request.publisherAccount.userName}'
                : 'Completed $lastChange',
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
          subtitle: isBusiness
              ? Text(
                  'Completed $lastChange',
                  style: Theme.of(context).textTheme.bodyText2.merge(
                        TextStyle(color: Theme.of(context).hintColor),
                      ),
                )
              : null,
          trailing:
              canViewProfile ? Icon(AppIcons.angleRight) : Container(width: 0),
        ),
      ],
    );
  }
}
