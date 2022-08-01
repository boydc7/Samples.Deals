import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/workspace.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class ProfileHeader extends StatelessWidget {
  final PublisherAccount user;

  ProfileHeader(this.user);

  @override
  Widget build(BuildContext context) {
    return Container(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Visibility(
            visible: !(appState.currentProfile.isCreator && user.isBusiness) &&
                user.isAccountFull,
            child: Column(
              children: <Widget>[
                Container(
                  padding:
                      EdgeInsets.symmetric(horizontal: 16.0, vertical: 10.0),
                  child: Center(
                    child: Text(
                      user.lastSyncedOnDisplay == null
                          ? "Syncing Instagram media and insights..."
                          : 'Last sync: ${user.lastSyncedOnDisplay}',
                      style: Theme.of(context).textTheme.caption.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                  ),
                ),
                Divider(
                  height: 1,
                ),
              ],
            ),
          ),
          Padding(
            padding: EdgeInsets.only(left: 16.0, top: 16.0, bottom: 16.0),
            child: Row(
              children: <Widget>[
                Container(
                  child: UserAvatar(
                    user,
                    width: 72.0,
                    showPaid:
                        appState.currentWorkspace.type == WorkspaceType.Team,
                  ),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(40.0),
                  ),
                ),
                Expanded(
                  child: Padding(
                    padding: EdgeInsets.symmetric(horizontal: 16.0),
                    child: !user.isAccountSoft
                        ? _buildStats(context)
                        : Container(),
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 16.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  user.nameDisplay,
                  style: Theme.of(context).textTheme.bodyText2.merge(
                        TextStyle(fontWeight: FontWeight.w600),
                      ),
                ),
                SizedBox(height: 8.0),
                user.description != null
                    ? Text(user.descriptionDisplay,
                        style: Theme.of(context).textTheme.bodyText2)
                    : Container(
                        height: 0,
                        width: 0,
                      ),
                Visibility(
                  visible: user.website != "",
                  child: SizedBox(height: 8.0),
                ),
                Visibility(
                  visible: user.websiteLink != "",
                  child: Text(
                    user.websiteLink,
                    style: TextStyle(
                        color: Theme.of(context).brightness == Brightness.dark
                            ? Color(0xFFDEEFFF)
                            : AppColors.black,
                        fontWeight: FontWeight.w500),
                  ),
                ),
                SizedBox(height: 16.0),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStats(BuildContext context) {
    final TextStyle statStyle = Theme.of(context).textTheme.bodyText1.merge(
          TextStyle(fontSize: 16.0),
        );

    Widget stat(String label, String value) => Expanded(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.start,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Text(value == "" ? "N/A" : value, style: statStyle),
              SizedBox(height: 4.0),
              Text(
                label,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyText2.merge(
                      TextStyle(height: 1.0),
                    ),
              )
            ],
          ),
        );

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        stat("Posts", user.publisherMetrics.postsDisplay),
        stat("Followers", user.publisherMetrics.followedByDisplay),
        stat("Following", user.publisherMetrics.followsDisplay),
      ],
    );
  }
}
