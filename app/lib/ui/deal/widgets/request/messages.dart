import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/models/enums/deal.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/ui/deal/request_dialog.dart';

class DealMessages extends StatelessWidget {
  final Deal deal;

  DealMessages(this.deal);

  void goToDialog(BuildContext context, bool autofocus) =>
      Navigator.of(context).push(
        MaterialPageRoute(
          builder: (BuildContext context) => RequestDialogPage(
            deal: deal,
            autofocus: autofocus,
          ),
          settings: AppAnalytics.instance.getRouteSettings('request/messages'),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final DealRequestStatus status = deal.request.status;
    final bool isBusiness = appState.currentProfile.isBusiness;
    final bool isInvite = deal.request.status == DealRequestStatus.invited;

    var timeCompleted;
    var timeCompletedPlus24;
    var today = DateTime.now();

    int timeLeftHours = 0;
    int timeLeftMinutes = 0;

    String messageSubtitle;

    /// calculate hours/mins left to send messages
    /// if we have a completed request
    if (status == DealRequestStatus.completed) {
      timeCompleted =
          deal.request.statusChanges.last.occurredOnDateTime.toLocal();
      timeCompletedPlus24 = timeCompleted.add(Duration(hours: 24));

      timeLeftHours = timeCompletedPlus24.difference(today).inHours;
      timeLeftMinutes = timeCompletedPlus24.difference(today).inMinutes;
    }

    if (status == DealRequestStatus.invited ||
        status == DealRequestStatus.requested) {
      messageSubtitle =
          "You will be able to direct message ${isBusiness ? deal.request.publisherAccount.userName : deal.publisherAccount.userName} if this ${isInvite ? "invite" : "request"} is ${isInvite ? "accepted" : "approved"}.";
    }

    if (status == DealRequestStatus.cancelled ||
        status == DealRequestStatus.denied ||
        status == DealRequestStatus.delinquent) {
      messageSubtitle = "Messages can only be sent on active requests.";
    }

    /// messages are only applicable to requests that are not invited, requested, or delinquent
    if (status == DealRequestStatus.invited ||
        status == DealRequestStatus.requested ||
        status == DealRequestStatus.denied) {
      return Column(
        children: <Widget>[
          SizedBox(height: 8.0),
          Row(
            mainAxisAlignment: MainAxisAlignment.start,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Container(
                width: 72,
                height: 40,
                child: Icon(
                  AppIcons.commentAltSlash,
                  color: Theme.of(context).appBarTheme.iconTheme.color,
                ),
              ),
              Expanded(
                child: Padding(
                  padding: EdgeInsets.only(right: 16.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      SizedBox(
                        height: 6.0,
                      ),
                      Text('Direct Message',
                          style: Theme.of(context).textTheme.bodyText1),
                      SizedBox(
                        height: 4.0,
                      ),
                      Text(messageSubtitle,
                          style: Theme.of(context).textTheme.bodyText2.merge(
                              TextStyle(color: Theme.of(context).hintColor)))
                    ],
                  ),
                ),
              ),
            ],
          ),
          SizedBox(height: 20.0),
          Divider(
            height: 1,
            indent: 72.0,
          ),
        ],
      );
    }

    /// if we can't send messages anymore and we've never actually had
    /// any messages then there's nothing to display here either
    if (!deal.request.canSendMessages && deal.request.lastMessage == null) {
      return Container();
    }

    return GestureDetector(
      onTap: () => goToDialog(context, true),
      child: Column(
        children: <Widget>[
          SizedBox(height: 16.0),
          Row(
            mainAxisAlignment: MainAxisAlignment.start,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Container(
                  width: 72,
                  height: 40,
                  child: Icon(
                    deal.request.canSendMessages
                        ? AppIcons.commentAltLines
                        : AppIcons.commentAltSlash,
                    color: Theme.of(context).appBarTheme.iconTheme.color,
                  )),
              Expanded(
                child: deal.request.lastMessage != null
                    ? _buildLastMessage(context)
                    : _buildSendMessage(context),
              ),
            ],
          ),
          Visibility(
            visible: deal.request.canSendMessages &&
                deal.request.status == DealRequestStatus.completed,
            child: Row(
              children: <Widget>[
                SizedBox(width: 72.0),
                Expanded(
                  child: Padding(
                    padding: EdgeInsets.only(
                        top: 8.0,
                        right: 32.0,
                        left: deal.request.lastMessage != null ? 16.0 : 0.0),
                    child: Text(
                      'Your ability to direct message regarding this RYDR will expire in ${timeLeftHours}h and ${timeLeftMinutes - (timeLeftHours * 60)}m.',
                      textAlign: deal.request.lastMessage != null
                          ? TextAlign.center
                          : TextAlign.left,
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          SizedBox(height: deal.request.lastMessage != null ? 12.0 : 16.0),
          Divider(
            height: 1,
            indent: 72.0,
          ),
        ],
      ),
    );
  }

  /// we've never sent a message in relation to this request
  /// so we show a tile here where we tell the relevant party to start a conversation
  Widget _buildSendMessage(BuildContext context) => Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                SizedBox(height: 6.0),
                Text('Start a Conversation...',
                    style: Theme.of(context).textTheme.bodyText1),
                SizedBox(
                  height: 4.0,
                ),
                Text(
                  appState.currentProfile.isBusiness
                      ? 'Send a message to ${deal.request.publisherAccount.userName}'
                      : 'Send a message to ${deal.publisherAccount.userName}',
                  style: Theme.of(context).textTheme.bodyText2.merge(
                        TextStyle(color: Theme.of(context).hintColor),
                      ),
                ),
                SizedBox(height: 4.0),
              ],
            ),
          ),
          Container(
            padding: EdgeInsets.only(right: 16.0),
            height: 40,
            child: Icon(
              AppIcons.angleRight,
              color: Theme.of(context).appBarTheme.iconTheme.color,
            ),
          ),
        ],
      );

  Widget _buildLastMessage(BuildContext context) {
    final DialogMessage message = deal.request.lastMessage;

    return Padding(
      padding: EdgeInsets.only(
        right: 16,
        bottom: 8,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(height: 4.0),
          Row(
            children: <Widget>[
              Expanded(
                child: Text(message.from.userName,
                    style: Theme.of(context)
                        .textTheme
                        .bodyText1
                        .merge(TextStyle(fontWeight: FontWeight.w600))),
              ),
              Text(
                message.sentOnDateDisplay,
                style: TextStyle(fontSize: 12.0, color: AppColors.grey300),
              ),
            ],
          ),
          SizedBox(height: 6.0),
          Padding(
            padding: EdgeInsets.only(right: 16.0),
            child: Text(message.message,
                style: Theme.of(context).textTheme.bodyText2),
          ),
          SizedBox(height: 16.0),
          Row(
            children: <Widget>[
              /// if we can no longer send messages then change the label on the button
              /// indicating that they can still view history
              Expanded(
                child: SecondaryButton(
                  label: deal.request.canSendMessages
                      ? 'All Messages'
                      : 'Message History',
                  onTap: () => goToDialog(context, false),
                ),
              ),
              SizedBox(width: 8.0),

              /// don't show the reply button if we can no longer send messages
              /// because the request was completed and the time to send more messages has expired
              Visibility(
                visible: deal.request.canSendMessages,
                child: Expanded(
                  child: SecondaryButton(
                    label: 'Reply',
                    onTap: () => goToDialog(context, true),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
