import 'dart:math';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';

/// A tile representing a deal-request which is available to both the business who owns the deal
/// and the influencer who made the request
class ListRequest extends StatelessWidget {
  final Deal deal;
  final Function onTap;

  ListRequest(this.deal, this.onTap);

  void _goToDetails() => onTap(deal,
      AppRouting.getRequestRoute(deal.id, deal.request.publisherAccount.id));

  void _goToRedeem() => onTap(
      deal,
      AppRouting.getRequestRedeemRoute(
          deal.id, deal.request.publisherAccount.id));

  void _goToMessage() => onTap(
      deal,
      AppRouting.getRequestDialogRoute(
          deal.id, deal.request.publisherAccount.id));

  void _goToComplete() => onTap(
      deal,
      AppRouting.getRequestCompleteRoute(
          deal.id, deal.request.publisherAccount.id));

  void _addTimeToComplete() => onTap(
      deal,
      AppRouting.getRequestRoute(
        deal.id,
        deal.request.publisherAccount.id,
      ));

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final bool dark = theme.brightness == Brightness.dark;
    final bool isBusiness = appState.currentProfile.isBusiness;
    final PublisherAccount userToDisplay =
        isBusiness ? deal.request.publisherAccount : deal.publisherAccount;
    final DealRequestStatus status = deal.request.status;
    final String statusString = dealRequestStatusToString(status);
    final Map<String, String> title = isBusiness
        ? {
            "requested":
                "${deal.request.publisherAccount.userName} · ${Utils.formatDoubleForDisplay(deal.request.publisherAccount.publisherMetrics.followedBy)} followers",
            "inProgress": deal.request.publisherAccount.userName,
            "redeemed": deal.request.publisherAccount.userName,
            "completed": deal.request.publisherAccount.userName,
            "invited": deal.request.publisherAccount.isAccountSoft
                ? "${deal.request.publisherAccount.userName}"
                : "${deal.request.publisherAccount.userName} · ${Utils.formatDoubleForDisplay(deal.request.publisherAccount.publisherMetrics.followedBy)} followers",
            "cancelled": deal.request.publisherAccount.userName,
            "denied": deal.request.publisherAccount.userName,
            "delinquent": deal.request.publisherAccount.userName,
          }
        : {
            "requested": deal.publisherAccount.userName,
            "inProgress": deal.publisherAccount.userName,
            "redeemed": deal.publisherAccount.userName,
            "completed": deal.publisherAccount.userName,
            "invited": deal.publisherAccount.userName,
            "cancelled": deal.publisherAccount.userName,
            "denied": deal.publisherAccount.userName,
            "delinquent": deal.publisherAccount.userName,
          };

    final Map<String, String> subtitle = isBusiness
        ? {
            "requested": deal.title,
            "inProgress": deal.request.lastMessage?.message ?? deal.title,
            "redeemed":
                '${deal.request.timeRemainingToCompleteDisplay} remaining to complete',
            "completed": deal.request.lastMessage?.message ?? deal.title,
            "invited": deal.title,
            "cancelled": deal.request.lastStatusChange
                        ?.modifiedByPublisherAccountId ==
                    appState.currentProfile.id
                ? 'Cancelled · ${deal.title}'
                : 'Cancelled by ${deal.publisherAccount.userName} · ${deal.title}',
            "denied": deal.title,
            "delinquent": deal.title,
          }
        : {
            "requested": "Pending · ${deal.title}",
            "inProgress":
                'Active · ${deal.request.lastMessage != null ? deal.request.lastMessage.message : deal.title}',
            "redeemed": deal.request.lastMessage != null
                ? '${deal.request.timeRemainingToCompleteDisplay} · ${deal.request.lastMessage.message}'
                : '${deal.request.timeRemainingToCompleteDisplay} remaining to complete',
            "completed": deal.request.lastMessage != null
                ? 'Complete · ${deal.request.lastMessage.message}'
                : 'Complete',
            "invited": deal.title,
            "cancelled":
                deal.request.lastStatusChange?.modifiedByPublisherAccountId ==
                        appState.currentProfile.id
                    ? 'Cancelled'
                    : 'Cancelled by ${deal.publisherAccount.userName}',
            "denied":
                deal.request.lastStatusChange?.modifiedByPublisherAccountId ==
                        appState.currentProfile.id
                    ? 'Request Declined'
                    : 'Request declined by ${deal.publisherAccount.userName}',
            "delinquent": "Delinquent",
          };

    final leading = Stack(
      alignment: Alignment.bottomRight,
      children: <Widget>[
        UserAvatar(userToDisplay),
        Stack(
          alignment: Alignment.center,
          children: <Widget>[
            Container(
              height: 12.0,
              width: 12.0,
              decoration: BoxDecoration(
                color: theme.scaffoldBackgroundColor,
                borderRadius: BorderRadius.circular(10.0),
              ),
            ),
            Container(
              height: 8.0,
              width: 8.0,
              decoration: BoxDecoration(
                color: Utils.getRequestStatusColor(status, dark),
                borderRadius: BorderRadius.circular(10.0),
              ),
            )
          ],
        ),
      ],
    );

    final Color colorRemaining = deal.request.daysRemainingToComplete >= 5
        ? theme.primaryColor
        : deal.request.daysRemainingToComplete >= 3
            ? Utils.getRequestStatusColor(DealRequestStatus.inProgress, dark)
            : AppColors.errorRed;

    final trailing = status == DealRequestStatus.inProgress
        ? isBusiness
            ? IconButton(
                highlightColor: Colors.transparent,
                icon: Icon(AppIcons.commentAltLines),
                onPressed: _goToMessage,
              )
            : IconButton(
                highlightColor: Colors.transparent,
                icon: Transform.rotate(
                  angle: 180 / pi,
                  child: Icon(AppIcons.ticketAlt),
                ),
                onPressed: _goToRedeem,
              )
        : status == DealRequestStatus.redeemed
            ? GestureDetector(
                onTap: isBusiness ? _addTimeToComplete : _goToComplete,
                child: Padding(
                  padding: EdgeInsets.only(top: 2.0),
                  child: Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      CircularProgressIndicator(
                        backgroundColor: theme.canvasColor,
                        value: (1 / deal.request.daysUntilDelinquent) *
                            deal.request.daysRemainingToComplete,
                        strokeWidth: 2.0,
                        valueColor:
                            AlwaysStoppedAnimation<Color>(colorRemaining),
                      ),
                      Icon(
                        isBusiness ? AppIcons.plusReg : AppIcons.arrowRightReg,
                        size: isBusiness ? 18 : 16,
                        color: colorRemaining,
                      ),
                    ],
                  ),
                ),
              )
            : status == DealRequestStatus.completed && isBusiness
                ? IconButton(
                    highlightColor: Colors.transparent,
                    icon: Icon(AppIcons.analytics),
                    onPressed: _goToDetails,
                  )
                : Container(width: 0);
    final suffix = status == DealRequestStatus.requested ||
            status == DealRequestStatus.invited
        ? ""
        : deal.title;

    return basicListItem(
      context: context,
      normalPaddingHorizontal: true,
      noPaddingVertical: true,
      isEvent: deal.dealType == DealType.Event,
      isInvite: statusString == "invited",
      wasInvite: deal.request.wasInvited,
      isVirtual: deal.dealType == DealType.Virtual,
      isDelinquent: statusString == "delinquent",
      onTap: _goToDetails,
      leading: leading,
      title: title[statusString],
      titleSuffix: suffix,
      subtitle: subtitle[statusString],
      trailing: trailing,
    );
  }
}
