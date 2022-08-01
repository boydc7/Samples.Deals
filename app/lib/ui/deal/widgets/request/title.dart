import 'package:flutter/material.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';

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

    final titleStyle = Theme.of(context).textTheme.bodyText2.merge(
          TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 24.0,
              height: 1.8,
              color: Theme.of(context).textTheme.bodyText2.color),
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
            maxLines: appState.currentProfile.isBusiness ? 3 : 9999,
            style: Theme.of(context).textTheme.bodyText2,
            strutStyle: StrutStyle(
              height: scaleFactor == 1
                  ? Theme.of(context).textTheme.bodyText2.height
                  : 1.5 * scaleFactor,
              forceStrutHeight: true,
            ),
          ),
          deal.dealType == DealType.Virtual
              ? Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Container(
                      height: 18,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(18),
                        color: Colors.deepOrange,
                      ),
                      margin: EdgeInsets.only(top: 8),
                      padding: EdgeInsets.symmetric(horizontal: 6),
                      child: Center(
                        child: Text(
                          "VIRTUAL",
                          style: Theme.of(context).textTheme.caption.merge(
                                TextStyle(
                                  fontSize: 10,
                                  color:
                                      Theme.of(context).scaffoldBackgroundColor,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                        ),
                      ),
                    ),
                  ],
                )
              : Container()
        ],
      ),
    );
  }
}
