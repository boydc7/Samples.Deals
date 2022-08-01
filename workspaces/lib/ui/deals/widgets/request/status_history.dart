import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:rydrworkspaces/app/utils.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/deal_request_status_change.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';

class RequestStatusHistory extends StatefulWidget {
  final Deal deal;

  RequestStatusHistory(this.deal);

  @override
  _RequestStatusHistoryState createState() => _RequestStatusHistoryState();
}

class _RequestStatusHistoryState extends State<RequestStatusHistory> {
  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    /// only show history if we have at least two status changes
    /// and if this request is completed then we must have completion media already
    if (widget.deal.request.statusChanges == null ||
        widget.deal.request.statusChanges.length < 2 ||
        (widget.deal.request.status == DealRequestStatus.completed &&
            widget.deal.request.completionMedia.length == 0)) {
      return Container(
        height: 0,
      );
    }

    final List<DealRequestStatusChange> statusChanges =
        widget.deal.request.statusChanges;
    List<Widget> _status = [];

    widget.deal.request.statusChanges
        .asMap()
        .forEach((int i, DealRequestStatusChange change) {
      /// if this was an auto approve deal then hide the first status change
      /// as the second will be the 'auto approved' one and we'll combine them into one
      final bool hideStatus = widget.deal.autoApproveRequests && i == 1;

      /// calculate the duration between this and the next status change
      /// if this was an auto approve deal and we're looking at the first change then
      /// skip over the next one as it would be the auto-apprpove change and we're combining the two
      final Duration diffToNext = i < statusChanges.length - 1
          ? statusChanges[
                  i + (i == 0 && widget.deal.autoApproveRequests ? 2 : 1)]
              .occurredOnDateTime
              .difference(statusChanges[i].occurredOnDateTime)
          : null;

      final String updatedBy = change.modifiedByPublisherAccountId ==
              widget.deal.request.publisherAccount.id
          ? widget.deal.request.publisherAccount.userName
          : widget.deal.publisherAccount.userName;

      final String status = change.toStatus == DealRequestStatus.requested
          ? widget.deal.autoApproveRequests
              ? "Requested and Auto Approved"
              : "Requested"
          : change.toStatus == DealRequestStatus.inProgress
              ? change.modifiedByPublisherAccountId ==
                      widget.deal.request.publisherAccount.id
                  ? "Invite Accepted"
                  : "Accepted"
              : change.toStatus == DealRequestStatus.completed
                  ? statusChanges
                              .where((DealRequestStatusChange status) =>
                                  status.fromStatus ==
                                  DealRequestStatus.redeemed)
                              .length ==
                          0
                      ? "Redeemed & Completed"
                      : "Completed"
                  : change.toStatus == DealRequestStatus.invited
                      ? "Invite Sent by $updatedBy"
                      : change.toStatus == DealRequestStatus.redeemed
                          ? "Redeemed by $updatedBy"
                          : change.toStatus == DealRequestStatus.denied
                              ? "Declilned by $updatedBy"
                              : change.toStatus == DealRequestStatus.cancelled
                                  ? "Cancelled by $updatedBy"
                                  : change.toStatus ==
                                          DealRequestStatus.delinquent
                                      ? "Marked delinquent by $updatedBy"
                                      : "";
      return _status.add(
        Visibility(
          visible: !hideStatus,
          child: Container(
            padding: EdgeInsets.only(
                left: 24.0, bottom: 24.0, right: 16, top: i == 0 ? 0.0 : 4.0),
            decoration: BoxDecoration(
              border: Border(
                left: BorderSide(
                    color: i == widget.deal.request.statusChanges.length - 1
                        ? Theme.of(context).scaffoldBackgroundColor
                        : Theme.of(context).dividerColor,
                    width: 1.0),
              ),
            ),
            child: Stack(
              overflow: Overflow.visible,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Stack(
                            overflow: Overflow.visible,
                            children: <Widget>[
                              Positioned(
                                left: -34.5,
                                top: -2.0,
                                child: Stack(
                                  alignment: Alignment.center,
                                  children: <Widget>[
                                    Container(
                                      height: 20.0,
                                      width: 20.0,
                                      decoration: BoxDecoration(
                                        borderRadius: BorderRadius.circular(20),
                                        color: Theme.of(context)
                                            .scaffoldBackgroundColor,
                                      ),
                                    ),
                                    Container(
                                      height: 10.0,
                                      width: 10.0,
                                      decoration: BoxDecoration(
                                        borderRadius: BorderRadius.circular(20),
                                        color:
                                            widget.deal.autoApproveRequests &&
                                                    i == 0
                                                ? Theme.of(context).primaryColor
                                                : Utils.getRequestStatusColor(
                                                    change.toStatus, dark),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              Text(status),
                            ],
                          ),
                          SizedBox(height: 4.0),
                          Text(
                            change.occurredOnDisplay,
                            style: Theme.of(context).textTheme.caption.merge(
                                  TextStyle(color: Theme.of(context).hintColor),
                                ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                Positioned(
                  top: 28.0,
                  left: -40,
                  child: Visibility(
                    visible: i != widget.deal.request.statusChanges.length - 1,
                    child: Container(
                      width: 30,
                      padding: EdgeInsets.all(4.0),
                      color: Theme.of(context).scaffoldBackgroundColor,
                      child: Text(
                        diffToNext != null
                            ? diffToNext.inHours != 0
                                ? "${diffToNext.inDays}d"
                                : diffToNext.inMinutes != 0
                                    ? "${diffToNext.inMinutes}m"
                                    : "${diffToNext.inSeconds}s"
                            : "",
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.caption.merge(
                              TextStyle(
                                color: Theme.of(context)
                                    .hintColor
                                    .withOpacity(0.65),
                                fontSize: 10.0,
                              ),
                            ),
                      ),
                    ),
                  ),
                )
              ],
            ),
          ),
        ),
      );
    });

    return Column(
      children: <Widget>[
        SizedBox(
          height: 16.0,
        ),
        Row(
          mainAxisAlignment: MainAxisAlignment.start,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
              width: 72,
              height: 40,
              child: Icon(Icons.history),
            ),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(
                    height: 11.0,
                  ),
                  Text('Complete RYDR History',
                      style: Theme.of(context).textTheme.bodyText2),
                  SizedBox(
                    height: 16.0,
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: _status,
                  )
                ],
              ),
            ),
          ],
        ),
        SizedBox(height: 8.0),
        Divider(height: 1, indent: 72.0),
      ],
    );
  }
}
