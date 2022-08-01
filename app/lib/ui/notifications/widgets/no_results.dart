import 'package:flutter/material.dart';

import 'package:rydr_app/app/theme.dart';

class NotificationsNoResults extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      height: MediaQuery.of(context).size.height - 160,
      padding: EdgeInsets.symmetric(horizontal: 24.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.max,
        children: <Widget>[
          Text('No notifications yet...',
              style: Theme.of(context).textTheme.headline6),
          SizedBox(
            height: 8.0,
          ),
          Text(
            "All notifications like status updates and\n messages will end up here.",
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyText2.merge(
                  TextStyle(color: AppColors.grey300),
                ),
          ),
        ],
      ),
    );
  }
}
