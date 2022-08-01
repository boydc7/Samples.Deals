import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/creator_details.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/messages.dart';
import 'package:rydrworkspaces/ui/deals/widgets/request/value.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/expiration_date.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/place.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/quantity.dart';
import 'package:rydrworkspaces/ui/deals/widgets/shared/receive_type_listItem.dart';

class RequestRequestedInvited extends StatelessWidget {
  final Deal deal;

  RequestRequestedInvited(this.deal);

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        RequestCreatorDetails(deal),
        DealReceiveTypeListItem(deal),
        DealPlace(deal),
        DealMessages(deal),
        DealQuantity(deal),
        DealExpirationDate(deal),
        DealValue(deal),
      ],
    );
  }
}
