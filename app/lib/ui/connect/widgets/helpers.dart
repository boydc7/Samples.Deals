import 'package:flutter/material.dart';

import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

Widget onboardUserHeader(
  BuildContext context,
) {
  if (appState.currentProfile == null) {
    return Container(height: 0);
  }

  return Column(
    mainAxisAlignment: MainAxisAlignment.center,
    crossAxisAlignment: CrossAxisAlignment.center,
    children: <Widget>[
      UserAvatar(appState.currentProfile),
      SizedBox(height: 8.0),
      Text(
        appState.currentProfile.userName,
        style: Theme.of(context).textTheme.bodyText1,
      ),
      SizedBox(height: 2.0),
      appState.currentProfile.publisherMetrics.followedBy != null
          ? Text(
              appState.currentProfile.publisherMetrics.followedByDisplay +
                  ' Followers',
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(color: Theme.of(context).hintColor),
                  ),
            )
          : Container(),
      SizedBox(height: 32.0),
    ],
  );
}

Widget onboardFadeInTile({
  @required BuildContext context,
  @required double delay,
  String leadingIndex,
  IconData leadingIcon,
  @required String title,
  @required String subTitle,
}) {
  return Column(
    children: <Widget>[
      FadeInRightLeft(
          delay,
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              leadingIndex != null
                  ? SizedBox(
                      width: 16.0,
                      child: Text(
                        "$leadingIndex.",
                        style: Theme.of(context).textTheme.bodyText2.merge(
                              TextStyle(fontSize: 16.0),
                            ),
                      ),
                    )
                  : SizedBox(
                      width: 22,
                      child: Align(
                        alignment: Alignment.centerLeft,
                        child: Icon(
                          leadingIcon,
                          color: Theme.of(context).textTheme.bodyText2.color,
                          size: 20.0,
                        ),
                      ),
                    ),
              SizedBox(
                width: 12.0,
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(fontSize: 16.0),
                          ),
                    ),
                    SizedBox(
                      height: 8.0,
                    ),
                    Text(
                      subTitle,
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(
                              color: Theme.of(context).hintColor,
                            ),
                          ),
                    ),
                  ],
                ),
              )
            ],
          ),
          300),
    ],
  );
}
