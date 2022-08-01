import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';

class DealValue extends StatelessWidget {
  final Deal deal;

  DealValue(this.deal);

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        SizedBox(height: 3.0),
        ListTile(
          leading: Container(
            width: 38.0,
            child: Center(
              child: Icon(Icons.monetization_on),
            ),
          ),
          title: Text('Cost of Goods',
              style: Theme.of(context).textTheme.bodyText2),
          trailing: Text(deal.valueFormatted,
              style: Theme.of(context).textTheme.bodyText1),
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
