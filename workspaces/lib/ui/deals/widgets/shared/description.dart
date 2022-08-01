import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/deal_request.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';

class DealDescription extends StatelessWidget {
  final Deal deal;
  final int maxLines;

  DealDescription(this.deal, {this.maxLines = 9999});

  @override
  Widget build(BuildContext context) {
    final DealRequest request = deal != null ? deal.request : null;

    final int lastSpace = deal.description.lastIndexOf(' ');
    final String description = lastSpace > -1
        ? deal.description.replaceFirst(RegExp(r' '), '\u00A0', lastSpace)
        : deal.description;

    if ((request != null && request.status == DealRequestStatus.completed) ||
        (request != null && request.status == DealRequestStatus.requested) ||
        (request == null)) {
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
                  child: Icon(
                    Icons.format_align_left,
                  )),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    SizedBox(
                      height: 4.0,
                    ),
                    Text("Full Description"),
                    SizedBox(
                      height: 6.0,
                    ),
                    Padding(
                      padding: EdgeInsets.only(right: 16.0),
                      child: Text(description,
                          style: Theme.of(context).textTheme.bodyText1),
                    )
                  ],
                ),
              )
            ],
          ),
          SizedBox(
            height: 20.0,
          ),
          Divider(
            height: 1,
            indent: 72.0,
          )
        ],
      );
    }

    return Padding(
        padding: EdgeInsets.only(left: 16, right: 16, bottom: 8.0),
        child: Text(description,
            textAlign: TextAlign.left,
            maxLines: maxLines,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodyText1));
  }
}
