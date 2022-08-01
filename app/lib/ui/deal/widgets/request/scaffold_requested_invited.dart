import 'package:flutter/material.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/ui/deal/widgets/request/creator_details.dart';
import 'package:rydr_app/ui/deal/widgets/request/messages.dart';
import 'package:rydr_app/ui/deal/widgets/request/scaffold.dart';
import 'package:rydr_app/ui/deal/widgets/shared/actionButton.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_notes.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_type.dart';
import 'package:rydr_app/ui/deal/widgets/shared/brand.dart';
import 'package:rydr_app/ui/deal/widgets/shared/expiration_date.dart';
import 'package:rydr_app/ui/deal/widgets/shared/place.dart';
import 'package:rydr_app/ui/deal/widgets/request/value.dart';
import 'package:rydr_app/ui/deal/widgets/shared/quantity.dart';
import 'package:rydr_app/ui/deal/widgets/shared/receive_type_listItem.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/models/deal.dart';

/// This is a request in either invited or requested state
/// visible to both the business and the creator

class RequestRequestedInvitedPage extends StatelessWidget {
  final Deal deal;
  final Function refresh;

  RequestRequestedInvitedPage(this.deal, this.refresh);

  @override
  Widget build(BuildContext context) => RequestScaffold(
        deal,
        [
          FadeInTopBottom(
            5,
            RequestCreatorDetails(deal),
            350,
            begin: -20.0,
          ),
          FadeInTopBottom(
            5,
            DealActionButton(deal),
            350,
            begin: -20.0,
          ),
          FadeInTopBottom(
            10,
            appState.currentProfile.isBusiness
                ? Column(
                    children: [
                      sectionDivider(context),
                      DealReceiveTypeListItem(deal),
                      DealReceiveNotes(
                        deal,
                        hideBorder: true,
                      ),
                    ],
                  )
                : DealReceiveType(deal),
            350,
            begin: -20.0,
          ),
          FadeInTopBottom(
            15,
            Column(
              children: <Widget>[
                sectionDivider(context),
                SizedBox(height: 4.0),
                DealBrand(deal),
                DealPlace(deal),
                DealMessages(deal),
                DealQuantity(deal),
                DealExpirationDate(deal),
                DealValue(deal),
              ],
            ),
            350,
            begin: -20.0,
          ),
        ],
        this.refresh,
      );
}