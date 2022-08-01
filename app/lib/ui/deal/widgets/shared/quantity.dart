import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';

class DealQuantity extends StatelessWidget {
  final Deal deal;
  final bool isLast;

  DealQuantity(this.deal, {this.isLast = false});

  /// figure out if we should even show the quantity
  bool _showIt() {
    /// if this is the business viewing an active deal (not a request)
    /// then we always show it no matter what...
    if (deal.request == null && appState.currentProfile.isBusiness) {
      return true;
    }

    /// if we have a request.
    if (deal.request != null) {
      /// if we have a request with unlimited quantity then we don't need to show it
      if (deal.maxApprovals == 0) {
        return false;
      }

      /// if the creator is viewing a pending request then hide quantity
      if (appState.currentProfile.isCreator) {
        return false;
      }

      /// if we have a request thats not requested or invited then no need to show it
      if (deal.request.status != DealRequestStatus.requested &&
          deal.request.status != DealRequestStatus.invited) {
        return false;
      }
    }

    return true;
  }

  @override
  Widget build(BuildContext context) {
    final iconColor = Theme.of(context).appBarTheme.iconTheme.color;
    final titleStyle = Theme.of(context).textTheme.bodyText1;

    return !_showIt()
        ? Container(height: 0)
        : deal.maxApprovals == 0
            ? _buildUnlimited(context, iconColor, titleStyle)
            : _buildRemaining(context, iconColor, titleStyle);
  }

  Widget _buildRemaining(
    BuildContext context,
    Color iconColor,
    TextStyle titleStyle,
  ) {
    /// the business should see both the original quantity and the remaining
    /// if they are different and we originally had more than one...
    /// furthermore, if the deal is expired then we show "unused" vs. "remaining"
    final String quantity = appState.currentProfile.isBusiness
        ? "${deal.maxApprovalsRemaining} of ${deal.maxApprovals} ${deal.expirationInfo.isExpired ? 'unused' : 'remaining'}"
        : "${deal.maxApprovalsRemaining} remaining";

    /// indicator / urgency coloring
    final Color color =
        deal.expirationDate != null && deal.expirationInfo.isExpired
            ? AppColors.grey300
            : deal.maxApprovalsRemaining <= 3
                ? Colors.red.shade800
                : deal.maxApprovalsRemaining <= 5
                    ? Colors.orange.shade800
                    : deal.maxApprovalsRemaining <= 10
                        ? Colors.orange.shade700
                        : Theme.of(context).primaryColor;

    return Column(
      children: <Widget>[
        SizedBox(height: 16.0),
        Row(
          mainAxisAlignment: MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
                width: 72,
                height: 40,
                child: Icon(AppIcons.hashtag, color: iconColor)),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(height: 6.0),
                  Padding(
                    padding: EdgeInsets.only(right: 16.0),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text('Quantity Available', style: titleStyle),
                        Padding(
                          padding: EdgeInsets.only(top: 3.5),
                          child: Text(
                            quantity,
                            style: Theme.of(context).textTheme.caption.merge(
                                  TextStyle(
                                    fontWeight: FontWeight.w500,
                                    height: 1.0,
                                    color: color,
                                  ),
                                ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  SizedBox(height: 6.0),
                  Padding(
                    padding:
                        EdgeInsets.only(right: 16.0, bottom: 16.0, top: 12.0),
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(8.0),
                      child: LinearProgressIndicator(
                          backgroundColor: Theme.of(context).canvasColor,
                          valueColor: AlwaysStoppedAnimation<Color>(color),
                          value: deal.maxApprovalsRemaining /
                                      deal.maxApprovals ==
                                  0
                              ? 0.02
                              : deal.maxApprovalsRemaining / deal.maxApprovals),
                    ),
                  ),
                ],
              ),
            )
          ],
        ),
        SizedBox(height: 12.0),
        Visibility(
          visible: !isLast,
          child: Divider(
            height: 1,
            indent: 72.0,
          ),
        ),
      ],
    );
  }

  Widget _buildUnlimited(
    BuildContext context,
    Color iconColor,
    TextStyle titleStyle,
  ) {
    return Column(
      children: <Widget>[
        SizedBox(height: 3.0),
        ListTile(
          leading: Container(
            width: 38.0,
            child: Center(child: Icon(AppIcons.hashtag, color: iconColor)),
          ),
          title: Text("Quantity Available", style: titleStyle),
          trailing:
              Text('Unlimited', style: Theme.of(context).textTheme.bodyText2),
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
