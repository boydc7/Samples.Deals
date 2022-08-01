import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class InfluencerAppBar extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return SafeArea(
        top: true,
        child: Container(
          padding: EdgeInsets.symmetric(horizontal: 8.0),
          width: MediaQuery.of(context).size.width,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: <Widget>[
              Stack(
                overflow: Overflow.visible,
                children: <Widget>[
                  AnimatedContainer(
                    duration: Duration(milliseconds: 250),
                    height: 48.0,
                    width: 48.0,
                    decoration: BoxDecoration(
                        color: Theme.of(context).appBarTheme.color,
                        borderRadius: BorderRadius.circular(100.0),
                        boxShadow: AppShadows.elevation[0]),
                    child: GestureDetector(
                      child: UserAvatar(
                        appState.currentProfile,
                        hideBorder: true,
                        width: 48.0,
                      ),
                      onTap: () => Navigator.of(context)
                          .pushNamed(AppRouting.getProfileMeRoute),
                    ),
                  ),
                  StreamBuilder<int>(
                    stream: appState.currentProfileUnreadNotifications,
                    builder: (context, snapshot) {
                      final int count = snapshot.data ?? 0;

                      return count > 0
                          ? Positioned(
                              bottom: -1.0,
                              left: count > 9 ? -7.5 : -4.0,
                              child: Badge(
                                elevation: 0.0,
                                color: Theme.of(context).primaryColor,
                                valueColor:
                                    Theme.of(context).scaffoldBackgroundColor,
                                value: count > 99 ? "99+" : count.toString(),
                              ),
                            )
                          : Container(width: 0, height: 0);
                    },
                  ),
                ],
              ),
            ],
          ),
        ));
  }
}
