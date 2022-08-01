import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

import 'package:rydr_app/models/dialog_message.dart';
import 'package:rydr_app/app/theme.dart';

class DialogListItem extends StatelessWidget {
  final DialogMessage lastMessage;
  final DialogMessage message;
  final Deal deal;

  DialogListItem({
    @required this.lastMessage,
    @required this.message,
    this.deal,
  });

  @override
  Widget build(BuildContext context) {
    final bool isMe = message.from.id == appState.currentProfile.id;

    /// if the sender of the last message is the same as the current message
    /// then we don't need to re-display the senders info and date time
    /// unless there's been more than x-minutes elapsed
    final bool hideSentInfo = lastMessage != null &&
        (lastMessage.from.id == message.from.id &&
            message.sentOn
                    .toLocal()
                    .difference(lastMessage.sentOn.toLocal())
                    .inMinutes <
                10);

    return Column(children: <Widget>[
      _buildDateHeader(context),
      Container(
        alignment: Alignment.centerLeft,
        margin: EdgeInsets.only(top: !hideSentInfo ? 16 : 0),
        padding:
            EdgeInsets.only(left: 16.0, right: 16.0, top: 4.0, bottom: 4.0),
        child: GestureDetector(
            onLongPress: () {
              HapticFeedback.lightImpact();
              Clipboard.setData(ClipboardData(text: message.message));
            },
            child: Tooltip(
                preferBelow: false,
                message: "Copied",
                excludeFromSemantics: true,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    GestureDetector(
                      onTap: isMe
                          ? null
                          : () {
                              Navigator.of(context).pushNamed(
                                AppRouting.getProfileRoute(
                                    appState.currentProfile.isBusiness
                                        ? deal.request.publisherAccount.id
                                        : deal.publisherAccount.id),
                                arguments: deal,
                              );
                            },
                      child: hideSentInfo
                          ? Container(
                              width: 40,
                            )
                          : UserAvatar(
                              message.from,
                              width: 40.0,
                            ),
                    ),
                    SizedBox(width: 16.0),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          hideSentInfo
                              ? Container()
                              : Padding(
                                  padding: EdgeInsets.only(bottom: 4),
                                  child: RichText(
                                      text: TextSpan(
                                          style: TextStyle(
                                            color: AppColors.grey400,
                                          ),
                                          children: <TextSpan>[
                                        TextSpan(
                                            text: message.from.userName,
                                            style: Theme.of(context)
                                                .textTheme
                                                .bodyText1
                                                .merge(TextStyle(
                                                    fontWeight:
                                                        FontWeight.w600))),
                                        TextSpan(
                                            text: !message.isDelivering &&
                                                    !message.isDelivered
                                                ? "  Unable to deliver..."
                                                : message.isDelivered
                                                    ? "  ${message.time}"
                                                    : "  sending",
                                            style: TextStyle(fontSize: 12.0))
                                      ])),
                                ),
                          SelectableText(message.message,
                              style: Theme.of(context).textTheme.bodyText2),
                        ],
                      ),
                    ),
                  ],
                ))),
      )
    ]);
  }

  Widget _buildDateHeader(BuildContext context) {
    String dateText;

    /// if we don't have a last message then display the date for the current message
    /// as the header / dividier for the list
    if (lastMessage == null) {
      dateText = message.sentOnDateDisplay;
    } else {
      /// if the day from the last message differs from the current message
      /// then display the header / divider now
      if (lastMessage.sentOn.toLocal().day != message.sentOn.toLocal().day) {
        dateText = message.sentOnDateDisplay;
      }
    }

    return dateText == null
        ? Container()
        : Container(
            padding: EdgeInsets.symmetric(vertical: 16),
            child: Center(
              child: Text(dateText,
                  style: Theme.of(context)
                      .textTheme
                      .caption
                      .merge(TextStyle(color: AppColors.grey300))),
            ));
  }
}
