import 'package:flutter/material.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/add/deal_invites.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_quantity.dart';

class DealAddInviteOnlySection extends StatelessWidget {
  final DealAddBloc bloc;

  DealAddInviteOnlySection(this.bloc);

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        SizedBox(height: 16),
        DealInputQuantity(
          valueStream: bloc.quantity,
          handleUpdate: bloc.setQuantity,
        ),
        SizedBox(height: 4),
        Container(
          alignment: Alignment.center,
          padding: EdgeInsets.symmetric(horizontal: 16.0),
          child: Text(
            'How many RYDRs do you want to give away?\nInvite more Creators than you have RYDRs available\nto create urgency.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(color: Theme.of(context).hintColor),
                ),
          ),
        ),
        SizedBox(height: 16.0),
        DealAddInvitesSection(bloc),
      ],
    );
  }
}
