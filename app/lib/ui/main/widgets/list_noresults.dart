import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class ListNoResults extends StatelessWidget {
  final ListPageArguments arguments;

  ListNoResults(this.arguments);

  @override
  Widget build(BuildContext context) {
    Widget activeWidget = appState.currentProfile.isBusiness
        ? Container(
            margin: EdgeInsets.only(top: 16.0),
            child: PrimaryButton(
              hasShadow: true,

              /// NOTE! once we want to support events we'll want to send the user
              /// to interstitial page for choosing deal type here...
              onTap: () =>
                  Navigator.of(context).pushNamed(AppRouting.getDealAddDeal),
              label: 'Create a RYDR',
            ),
          )
        : null;

    final Map<String, String> titles = appState.currentProfile.isBusiness
        ? {
            "active_title": "No Active RYDRs",
            "active_message":
                "When you publish a RYDR to the\nMarketplace, you'll see it here.",
            "invited_title": "No pending invites...",
            "invited_message":
                "When you invite creators to a RYDR they will show here.",
            "pending_title": "Pending Requests on your RYDRs",
            "pending_title_filter_deal":
                "There are no Pending Requests for your RYDR ${arguments.filterDealName}",
            "pending_message":
                "When a creator requests a RYDR, \nyou'll see it here.",
            "pending_message_filter_deal":
                "Once a Creator has requested this RYDR it will show here...",
            "inprogress_title": "Your In-Progress RYDRs",
            "inprogress_message":
                "When you approve a request from a Creator and they have yet to redeem it, you'll see it here.",
            "inprogress_title_filter_deal":
                "No In-Progress RYDRs for ${arguments.filterDealName}",
            "inprogress_message_filter_deal":
                "Once a Creator is working on this RYDR it will show here",
            "redeemed_title": "Your Redeemed RYDRs",
            "redeemed_message":
                "When you or a creatore redeem a RYDR you'll see it here.",
            "redeemed_title_filter_deal":
                "No redeemed RYDRs for ${arguments.filterDealName}",
            "redeemed_message_filter_deal":
                "Once a request for this RYDR has been redeemed you'll see it here.",
            "deleted_title": "Your Deleted RYDRs",
            "deleted_message": "When you delete a RYDR, \nyou'll see it here.",
            "completed_title": "Your Completed RYDRs",
            "completed_message":
                "When a Creator completes a RYDR, \nyou'll see it here.",
            "completed_title_filter_deal":
                "No Completed RYDRs for ${arguments.filterDealName}",
            "completed_message_filter_deal":
                "Once a creator completes this RYDR it will show here.",
            "cancelled_title": "No Cancelled Requests",
            "cancelled_message": "All cancelled requests it will show here.",
            "denied_title": "No Declined Requests",
            "denied_message": "All declined requests it will show here.",
            "delinquent_title": "No Delinquent Requests",
            "delinquent_message": "All delinquent requests it will show here.",
          }
        : {
            "active_title": "No requests yet...",
            "active_message": "When you request a RYDR they will show here.",
            "pending_title": "No requests yet...",
            "pending_message": "When you request a RYDR they will show here.",
            "invited_title": "No invites yet...",
            "invited_message":
                "When a business invites you to a RYDR they will show here.",
            "inprogress_title": "No approved RYDRs...",
            "inprogress_message":
                "When a business approves, or you accept an invite to a RYDR, they will show here.",
            "redeemed_title": "No redeemed RYDRs...",
            "redeemed_message": "When a RYDR is redeemed it will show here.",
            "cancelled_title": "No Cancelled Requests",
            "cancelled_message": "All cancelled requests it will show here.",
            "denied_title": "No Declined Requests",
            "denied_message": "All declined requests it will show here.",
            "delinquent_title": "No Delinquent Requests",
            "delinquent_message": "All delinquent requests it will show here.",
          };

    String title = titles['active_title'];
    String message = titles['active_message'];

    /// if we have arguments that represent showing deal reqeusts
    /// then determine the title and message for when we have no results
    if (arguments != null) {
      if (arguments.filterRequestStatus != null) {
        /// each request status could have filters which would change the message
        /// from having no results to no results for the given filter param
        if (arguments.filterRequestStatus
            .contains(DealRequestStatus.requested)) {
          if (arguments.filterDealId != null) {
            title = titles['pending_title_filter_deal'];
            message = titles['pending_message_filter_deal'];
            activeWidget = null;
          } else {
            title = titles['pending_title'];
            message = titles['pending_message'];
            activeWidget = null;
          }
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.invited)) {
          title = titles['invited_title'];
          message = titles['invited_message'];
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.inProgress)) {
          if (arguments.filterDealId != null) {
            title = titles['inprogress_title_filter_deal'];
            message = titles['inprogress_message_filter_deal'];
            activeWidget = null;
          } else {
            title = titles['inprogress_title'];
            message = titles['inprogress_message'];
            activeWidget = null;
          }
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.redeemed)) {
          if (arguments.filterDealId != null) {
            title = titles['redeemed_title_filter_deal'];
            message = titles['redeemed_message_filter_deal'];
            activeWidget = null;
          } else {
            title = titles['redeemed_title'];
            message = titles['redeemed_message'];
            activeWidget = null;
          }
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.completed)) {
          if (arguments.filterDealId != null) {
            title = titles['completed_title_filter_deal'];
            message = titles['completed_message_filter_deal'];
            activeWidget = null;
          } else {
            title = titles['completed_title'];
            message = titles['completed_message'];
            activeWidget = null;
          }
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.cancelled)) {
          title = titles['cancelled_title'];
          message = titles['cancelled_message'];
          activeWidget = null;
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.denied)) {
          title = titles['denied_title'];
          message = titles['denied_message'];
          activeWidget = null;
        } else if (arguments.filterRequestStatus
            .contains(DealRequestStatus.delinquent)) {
          title = titles['delinquent_title'];
          message = titles['delinquent_message'];
          activeWidget = null;
        } else {
          title = "No RYDR Requests";
          activeWidget = null;
        }
      } else if (arguments.filterDealStatus != null) {
        if (arguments.filterDealStatus.contains(DealStatus.completed)) {
          title = "No Archived RYDRs";
          message = "Archived & Completed RYDRs will show here";
          activeWidget = null;
        } else if (arguments.filterDealStatus.contains(DealStatus.deleted)) {
          title = "No Deleted RYDRs";
          message = "Deleted RYDRs will show here";
          activeWidget = null;
        } else if (arguments.filterDealStatus.contains(DealStatus.paused)) {
          title = "No Paused RYDRs";
          message = "All paused RYDRs will show here";
          activeWidget = null;
        }
      }
    }

    return Container(
      height: MediaQuery.of(context).size.height - 160,
      padding: EdgeInsets.symmetric(horizontal: 24.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.max,
        children: <Widget>[
          Text(title,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.headline6),
          SizedBox(
            height: 8.0,
          ),
          Text(
            message,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyText2.merge(
                  TextStyle(color: Theme.of(context).hintColor),
                ),
          ),
          activeWidget != null ? activeWidget : Container(),
          SizedBox(height: kToolbarHeight),
        ],
      ),
    );
  }
}
