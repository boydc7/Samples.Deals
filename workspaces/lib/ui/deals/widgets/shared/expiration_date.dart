import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';

class DealExpirationDate extends StatelessWidget {
  final Deal deal;

  DealExpirationDate(this.deal);

  @override
  Widget build(BuildContext context) {
    /// figure out if we should even show the epxiration date
    bool showIt = false;
    bool expires = deal.expirationDate != null;

    /// if this is the business viewing the deal without a request
    /// meaning in 'edit' mode, then always show the expiration date even if 'never' expires
    if (deal.request == null) {
      showIt = true;
    } else {
      /// if we don't have a request, then show it if it expires
      if (deal.request == null) {
        showIt = expires;
      } else {
        /// if we have a request then only show it if it expires
        /// and the request is in a 'pending' state still
        if (deal.request.status == DealRequestStatus.invited ||
            deal.request.status == DealRequestStatus.requested) {
          showIt = expires;
        }
      }
    }

    if (!showIt) {
      return Container();
    }

    if (!deal.expirationInfo.neverExpires && deal.expirationInfo.daysLeft < 5) {
      return _buildDetailListTile(
        context: context,
        icon: Icons.timelapse,
        title: 'Expiration',
        trailing: Container(
          width: 120,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: <Widget>[
              Container(
                height: 28.0,
                alignment: Alignment.center,
                margin: EdgeInsets.only(left: 2.0),
                padding: EdgeInsets.symmetric(horizontal: 10.0, vertical: 4.0),
                decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(32.0),
                    color: deal.expirationInfo.daysLeft != null &&
                            deal.expirationInfo.daysLeft <= 0
                        ? Theme.of(context).hintColor
                        : deal.expirationInfo.daysLeft == 1
                            ? Colors.deepOrange.shade100
                            : Colors.amber.shade100),
                child: Text(
                  deal.expirationInfo.daysLeft != null &&
                          deal.expirationInfo.daysLeft <= 0
                      ? deal.expirationDate.difference(DateTime.now()).inHours <
                              1
                          ? 'Expired'
                          : '${deal.expirationDate.difference(DateTime.now()).inHours}h left'
                      : deal.expirationInfo.daysLeft == 1
                          ? '1d left'
                          : '${deal.expirationInfo.daysLeft.toString()}d left',
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(
                          fontWeight: FontWeight.w500,
                          height: 1.0,
                          color: deal.expirationInfo.daysLeft != null &&
                                  deal.expirationInfo.daysLeft <= 0
                              ? Theme.of(context).appBarTheme.color
                              : deal.expirationInfo.daysLeft == 1
                                  ? Colors.deepOrange.shade700
                                  : Colors.orange.shade700,
                        ),
                      ),
                ),
              ),
            ],
          ),
        ),
      );
    } else {
      return _buildDetailListTile(
        context: context,
        icon: Icons.timelapse,
        title: 'Expiration',
        trailing: Text(
          deal.expirationInfo.simpleDisplay,
          style: Theme.of(context).textTheme.bodyText1,
        ),
      );
    }
  }

  Widget _buildDetailListTile(
      {BuildContext context, String title, IconData icon, Widget trailing}) {
    return Column(
      children: <Widget>[
        SizedBox(height: 3.0),
        ListTile(
          leading: Container(
            width: 38.0,
            child: Center(child: Icon(icon)),
          ),
          title: Text(title),
          trailing: trailing,
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