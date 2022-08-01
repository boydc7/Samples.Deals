import 'package:flutter/material.dart';

import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';

class DealPlace extends StatelessWidget {
  final Deal deal;
  final bool addDivider;

  DealPlace(this.deal, [this.addDivider = true]);

  @override
  Widget build(BuildContext context) {
    /// guard against not having a place, should not happen but can be the case
    /// with test data potentially so we bail if we don't have one
    if (deal.place == null || deal.place.name == null) {
      return Container(height: 0);
    }
    final bool isCancelled =
        deal.request?.status == DealRequestStatus.cancelled ||
            deal.request?.status == DealRequestStatus.denied ||
            deal.request?.status == DealRequestStatus.delinquent;

    final int lastSpace = deal.place.name.lastIndexOf(' ');
    final String placeName = lastSpace > -1
        ? deal.place.name.replaceFirst(RegExp(r' '), '\u00A0', lastSpace)
        : deal.place.name;

    return GestureDetector(
      onTap: !isCancelled
          ? () => Utils.launchMapsAction(context, deal.place)
          : null,
      child: Container(
        color: Colors.transparent,
        child: Column(
          children: <Widget>[
            SizedBox(height: 12.0),
            Row(
              mainAxisAlignment: MainAxisAlignment.start,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Container(
                    width: 72,
                    height: 40,
                    child: Icon(
                      AppIcons.mapMarkerAlt,
                      color: Theme.of(context).appBarTheme.iconTheme.color,
                    )),
                Expanded(
                  child: Padding(
                    padding: EdgeInsets.only(right: 16.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        SizedBox(
                            height: deal.place?.address?.address1 == null
                                ? 12
                                : 6.0),
                        Text(placeName,
                            style: Theme.of(context).textTheme.bodyText1),
                        SizedBox(
                          height: 4.0,
                        ),
                        deal.place?.address?.address1 == null
                            ? Container()
                            : Text(deal.place?.address?.address1 ?? "",
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyText2
                                    .merge(TextStyle(
                                        color: Theme.of(context).hintColor)))
                      ],
                    ),
                  ),
                ),
                Visibility(
                  visible: appState.currentProfile.isCreator && !isCancelled,
                  child: Container(
                    padding: EdgeInsets.only(right: 16.0),
                    height: 40,
                    child: Icon(
                      AppIcons.angleRight,
                      color: Theme.of(context).iconTheme.color,
                    ),
                  ),
                ),
              ],
            ),
            SizedBox(height: 20.0),
            addDivider
                ? Divider(
                    height: 1,
                    indent: 72.0,
                  )
                : Container(height: 0),
          ],
        ),
      ),
    );
  }
}
