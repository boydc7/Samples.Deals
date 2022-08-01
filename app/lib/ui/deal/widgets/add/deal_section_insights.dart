import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_age.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_quantity.dart';

class DealAddThresholdInsightsSection extends StatelessWidget {
  final DealAddBloc bloc;

  DealAddThresholdInsightsSection(this.bloc);

  @override
  Widget build(BuildContext context) {
    final String title =
        "Based on Creators' insights, RYDR will determine who can see and request this RYDR. We will also determine if the Creator needs to post to their feed or on their story to meet the minimum reach.";

    return StreamBuilder<DealThresholdType>(
      stream: bloc.thresholdType,
      builder: (context, snapshot) {
        if (snapshot.data != null &&
            snapshot.data == DealThresholdType.Insights) {
          return Column(
            children: <Widget>[
              SizedBox(height: 16.0),
              DealInputQuantity(
                valueStream: bloc.quantity,
                handleUpdate: bloc.setQuantity,
              ),
              SizedBox(height: 16.0),
              Text(
                title,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                          color: Theme.of(context).textTheme.bodyText2.color),
                    ),
              ),
              SizedBox(height: 16),
              DealInputAge(
                valueStream: bloc.age,
                handleUpdate: bloc.setAge,
              ),
            ],
          );
        } else {
          return Container(height: 0);
        }
      },
    );
  }
}
