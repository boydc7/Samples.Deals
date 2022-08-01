import 'package:flutter/material.dart';

import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/workspace.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';

class ProfileButton extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return appState.currentProfile == null
        ? Container()
        : InkWell(
            borderRadius: BorderRadius.circular(80),
            onTap: () {
              Navigator.of(context).pushNamed(AppRouting.getProfileMeRoute);
            },
            child: Container(
              width: 56.0,
              color: Colors.transparent,
              child: CircleAvatar(
                radius: 28,
                backgroundColor: Colors.transparent,
                child: Stack(
                  overflow: Overflow.visible,
                  children: <Widget>[
                    UserAvatar(
                      appState.currentProfile,
                      showPaid:
                          appState.currentWorkspace.type == WorkspaceType.Team,
                    ),
                    StreamBuilder<int>(
                      stream: appState.currentProfileUnreadNotifications,
                      builder: (context, snapshot) {
                        final int count = snapshot.data ?? 0;

                        return count > 0
                            ? Positioned(
                                top: -5.0,
                                left: count > 9 ? -7.5 : -4.0,
                                child: Badge(
                                  elevation: 0.0,
                                  color: Theme.of(context).primaryColor,
                                  valueColor:
                                      Theme.of(context).appBarTheme.color,
                                  value: count.toString(),
                                ),
                              )
                            : Container(width: 0, height: 0);
                      },
                    ),
                  ],
                ),
              ),
            ),
          );
  }
}
