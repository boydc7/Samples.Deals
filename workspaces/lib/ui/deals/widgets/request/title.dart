import 'package:flutter/material.dart';
import 'package:rydrworkspaces/models/deal.dart';

class RequestTitle extends StatelessWidget {
  final Deal deal;

  RequestTitle(
    this.deal,
  );

  @override
  Widget build(BuildContext context) {
    final double scaleFactor = MediaQuery.of(context).textScaleFactor;
    final int lastSpaceDescription = deal.description.lastIndexOf(' ');
    final String description = lastSpaceDescription > -1
        ? deal.description
            .replaceFirst(RegExp(r' '), '\u00A0', lastSpaceDescription)
        : deal.description;

    final int lastSpace = deal.title.lastIndexOf(' ');
    final String title = lastSpace > -1
        ? deal.title.replaceFirst(RegExp(r' '), '\u00A0', lastSpace)
        : deal.title;

    final titleStyle = Theme.of(context).textTheme.bodyText1.merge(
          TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 24.0,
              height: 1.8,
              color: Theme.of(context).textTheme.bodyText1.color),
        );
    return Container(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisSize: MainAxisSize.max,
        children: <Widget>[
          Text(
            title,
            textAlign: TextAlign.center,
            style: titleStyle,
            strutStyle: StrutStyle(
              height: titleStyle.height * scaleFactor,
              forceStrutHeight: true,
            ),
          ),
          SizedBox(height: 6.0),
          Text(
            description,
            textAlign: TextAlign.center,
            maxLines: 9999,
            style: Theme.of(context).textTheme.bodyText1,
            strutStyle: StrutStyle(
              height: scaleFactor == 1
                  ? Theme.of(context).textTheme.bodyText1.height
                  : 1.5 * scaleFactor,
              forceStrutHeight: true,
            ),
          ),
        ],
      ),
    );
  }
}
