import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_age.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_auto_approve.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_engagement_rating.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_follower_count.dart';
import 'package:rydr_app/ui/deal/widgets/form/input_quantity.dart';
import 'package:rydr_app/ui/deal/widgets/shared/threshold_info.dart';

class DealAddThresholdRestrictionsSection extends StatelessWidget {
  final DealAddBloc bloc;

  DealAddThresholdRestrictionsSection(this.bloc);

  @override
  Widget build(BuildContext context) => StreamBuilder<DealThresholdType>(
        stream: bloc.thresholdType,
        builder: (context, snapshot) {
          if (snapshot.data == null) {
            return Container(height: 0);
          } else if (snapshot.data == DealThresholdType.Restrictions) {
            return Column(
              children: <Widget>[
                SizedBox(height: 4.0),
                DealInputFollowerCount(
                  valueStream: bloc.followerCount,
                  handleUpdate: bloc.setFollowerCount,
                ),
                DealInputEngagementRating(
                  valueStream: bloc.engagementRating,
                  handleUpdate: bloc.setEngagementRating,
                ),
                DealThresholdInfo(
                  engagementRatingStream: bloc.engagementRating,
                  followerCountStream: bloc.followerCount,
                ),
                Divider(height: 1),
                SizedBox(height: 20.0),
                DealInputQuantity(
                  valueStream: bloc.quantity,
                  handleUpdate: bloc.setQuantity,
                ),
                DealInputAutoApprove(
                  valueStream: bloc.autoApprove,
                  quantityStream: bloc.quantity,
                  handleUpdate: bloc.setAutoApprove,
                ),
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
