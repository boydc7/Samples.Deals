import 'package:flutter/material.dart';
import 'package:rydr_app/app/utils.dart';

import 'package:rydr_app/models/deal.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';

class DealNotice extends StatelessWidget {
  final Deal deal;

  DealNotice(this.deal);

  @override
  Widget build(BuildContext context) {
    /// completed or deleted deals have no notices as they'll show in a list where it makes no sense
    /// to show a notice above the title of the deal
    if (deal.status == DealStatus.completed ||
        deal.status == DealStatus.deleted) {
      return Container(height: 0, width: 0);
    }

    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final bool isExpired = deal.expirationInfo.isExpired == true;
    final bool isInvite = deal.isInvited ?? false;
    final bool isEvent = deal.dealType == DealType.Event;

    final bool isExpiringOrFewRemaining =
        (deal.maxApprovalsRemaining != 0 && deal.maxApprovalsRemaining < 10) ||
            deal.expirationInfo.isExpiringSoon;

    final bool isCloseToYou = appState.currentProfile.isCreator &&
        deal.distanceInMiles != null &&
        deal.distanceInMiles <= 3;

    final Widget invite = Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Icon(
          AppIcons.starsSolid,
          size: 13.0,
          color: Utils.getRequestStatusColor(DealRequestStatus.invited, dark),
        ),
        SizedBox(width: 4.0),
        Padding(
          padding: EdgeInsets.only(
            top: 3.0,
          ),
          child: Text(
            "PRIVATE INVITE",
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                    color: Utils.getRequestStatusColor(
                        DealRequestStatus.invited, dark),
                    fontWeight: FontWeight.w700,
                    fontSize: 11.0,
                  ),
                ),
          ),
        ),
      ],
    );

    final Widget eventNoInvite = Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Icon(
          AppIcons.calendarStarSolid,
          size: 13.0,
          color: Theme.of(context).primaryColor,
        ),
        SizedBox(width: 4.0),
        Padding(
          padding: EdgeInsets.only(
            top: 3.0,
          ),
          child: Text(
            "EVENT",
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                    color: Theme.of(context).primaryColor,
                    fontWeight: FontWeight.w700,
                    fontSize: 10.5,
                  ),
                ),
          ),
        ),
      ],
    );

    final Widget eventInvite = Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Icon(
          AppIcons.calendarStarSolid,
          size: 13.0,
          color: Utils.getRequestStatusColor(DealRequestStatus.invited, dark),
        ),
        SizedBox(width: 4.0),
        Padding(
          padding: EdgeInsets.only(
            top: 3.0,
          ),
          child: Text(
            "EVENT INVITE",
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                    color: Utils.getRequestStatusColor(
                        DealRequestStatus.invited, dark),
                    fontWeight: FontWeight.w700,
                    fontSize: 10.5,
                  ),
                ),
          ),
        ),
      ],
    );

    final Widget expiringOrFewRemaining = isExpiringOrFewRemaining
        ? RichText(
            text: TextSpan(
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                        color: Colors.deepOrange,
                        fontWeight: FontWeight.w500,
                        fontSize: 11.0),
                  ),
              children: [
                deal.maxApprovalsRemaining <= 5 &&
                        deal.maxApprovalsRemaining != 0 &&
                        deal.maxApprovalsRemaining != -1
                    ? TextSpan(
                        text: 'ONLY ' +
                            deal.maxApprovalsRemaining.toString() +
                            ' REMAINING')
                    : deal.maxApprovalsRemaining != 0 &&
                            deal.maxApprovalsRemaining != -1
                        ? TextSpan(
                            text: deal.maxApprovalsRemaining.toString() +
                                ' REMAINING')
                        : TextSpan(text: ''),
                deal.maxApprovalsRemaining != 0 &&
                        deal.maxApprovalsRemaining != -1 &&
                        deal.expirationInfo.displayTimeLeft != null
                    ? TextSpan(text: ' · ')
                    : TextSpan(text: ''),
                deal.expirationInfo.displayTimeLeft != null
                    ? TextSpan(
                        text: deal.expirationInfo.displayTimeLeft.toUpperCase())
                    : TextSpan(text: ''),
              ],
            ),
          )
        : Container();

    /// Build out the expiring widget if we indeed have a deal
    /// that is already expired - this is eligible to show for both business and creators
    final Widget expired = isExpired
        ? Text(
            'EXPIRED',
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(color: Theme.of(context).hintColor, fontSize: 11.0),
                ),
          )
        : Container();

    /// Build the "close to you" widget for creators and only if the deal
    /// is within close proximit to their last known location
    final Widget closeToYou = isCloseToYou
        ? Text(
            'CLOSE TO YOU',
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(
                      color: Theme.of(context).primaryColor, fontSize: 11.0),
                ),
          )
        : Container();

    if (isEvent && isInvite) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: eventInvite,
      );
    } else if (isInvite) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: invite,
      );
    } else if (isEvent && isInvite) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: eventInvite,
      );
    } else if (isEvent) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: eventNoInvite,
      );
    } else if (isExpired) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: expired,
      );
    } else if (isExpiringOrFewRemaining) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: expiringOrFewRemaining,
      );
    } else if (isCloseToYou) {
      return Padding(
        padding: EdgeInsets.only(bottom: 2.0, right: 8),
        child: closeToYou,
      );
    } else {
      return Container();
    }
  }
}
