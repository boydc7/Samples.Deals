import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';

class DealValue extends StatelessWidget {
  final Deal deal;

  DealValue(this.deal);

  String get valueFormatted => this.deal.value == null
      ? "0"
      : NumberFormat.compactCurrency(decimalDigits: 2, symbol: "\$")
          .format(this.deal.value);

  @override
  Widget build(BuildContext context) {
    /// only applicable to the business looking at a request
    if (appState.currentProfile.isCreator || deal.request == null) {
      return Container(
        height: 1,
      );
    }

    return Column(
      children: <Widget>[
        SizedBox(height: 3.0),
        ListTile(
          leading: Container(
            width: 38.0,
            child: Center(
              child: Icon(
                AppIcons.userTag,
                color: Theme.of(context).appBarTheme.iconTheme.color,
              ),
            ),
          ),
          title: Text('Cost of Goods',
              style: Theme.of(context).textTheme.bodyText1),
          trailing: Text(valueFormatted,
              style: Theme.of(context).textTheme.bodyText2),
        ),
        SizedBox(height: 4.0),
        Divider(
          height: 1,
          indent: 72.0,
        )
      ],
    );
  }
}
