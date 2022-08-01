import 'dart:async';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/services/workspaces.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/models/enums/workspace.dart';

class WorkspaceSheet extends StatelessWidget {
  /// we get the parents' context passed here so we can safely 'close' the sheet
  /// and still have context to then do navigation
  final BuildContext parentContext;

  WorkspaceSheet(this.parentContext);

  final Map<String, String> _pageContent = {
    "personal_profiles": "Personal Profiles",
    "join_workspace": "Join Workspace",
    "create_workspace": "Create Workspace",
  };

  void _goToWorkspaceJoin(BuildContext context) {
    Navigator.of(context).pop();
    Navigator.of(context).pushNamed(AppRouting.getWorkspaceJoin);
  }

  void _goToWorkspaceCreate(BuildContext context) async {
    Navigator.of(context).pop();

    await Future.delayed(
        Duration(milliseconds: 250),
        () => Navigator.of(this.parentContext)
            .pushNamed(AppRouting.getWorkspaceCreate));
  }

  void _switchToWorkspace(BuildContext context, Workspace workspace) async {
    /// if we're switching to a different workspace then reload
    /// the profiles list with the workspace
    if (appState.currentWorkspace.id != workspace.id) {
      showSharedLoadingLogo(context);

      final WorkspaceResponse workspaceResponse =
          await WorkspacesService.getWorkspace(workspace.id);

      /// close loading overlay
      Navigator.of(context).pop();

      if (workspaceResponse.hasError) {
        /// if this user is NOT the owner of a team workspace then likely this is
        /// because they don't have any linked profiles with active subscriptions
        if (workspace.type == WorkspaceType.Team &&
            workspace.role != WorkspaceRole.Admin) {
          /// alert the user that there are no active subscriptions for any of
          /// their linked profiles in this team workspace and that's why they can't switch to it
          showSharedModalError(context,
              title: "No Active Profiles",
              subTitle:
                  "There are no active subscriptions in this workspace.\n\nContact your team administrator to enable them.");
        } else {
          showSharedModalError(context,
              title: "Workspace Unavailable",
              subTitle:
                  "We are unable to switch to this workspace. Please try again in a few moments.");
        }
      } else {
        /// switch the workspace on the app state
        await appState.switchWorkspace(workspace);

        /// log this as a screen change
        AppAnalytics.instance.logScreen('workspace/switched');

        /// clear all routes and send the user back to pages
        Navigator.of(parentContext).pushNamedAndRemoveUntil(
            AppRouting.getConnectPages, (Route<dynamic> route) => false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final Workspace workspaceTeam = appState.workspaces.firstWhere(
        (Workspace w) =>
            w.type == WorkspaceType.Team && w.role == WorkspaceRole.Admin,
        orElse: () => null);

    /// determine if this user can create a workspace, as of now they can only be the owner of one
    /// so if that's the case then they can't create any additional ones
    final bool canCreateWorkspace = workspaceTeam == null;

    if (appState.workspaces.length > 1) {
      return Container(
        width: double.infinity,
        child: Column(
          children: <Widget>[
            Divider(height: 1),
            Column(
              children: appState.workspaces
                  .map((workspace) => _buildWorkspaceProfile(
                        context,
                        workspace,
                      ))
                  .toList(),
            ),
            Padding(
              padding: EdgeInsets.only(top: 20),
              child: _buildWorkspaceButtons(context, canCreateWorkspace),
            )
          ],
        ),
      );
    } else {
      return _buildWorkspaceButtons(context, canCreateWorkspace);
    }
  }

  Widget _buildWorkspaceButtons(BuildContext context, bool canCreateWorkspace) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        SizedBox(width: 32),
        Expanded(
          child: SecondaryButton(
            context: context,
            label: "JOIN TEAM",
            onTap: () => _goToWorkspaceJoin(context),
            primary: true,
          ),
        ),
        Visibility(visible: canCreateWorkspace, child: SizedBox(width: 8)),
        canCreateWorkspace
            ? Expanded(
                child: SecondaryButton(
                  context: context,
                  label: "CREATE NEW",
                  onTap: () => _goToWorkspaceCreate(context),
                  primary: true,
                ),
              )
            : Container(width: 0),
        SizedBox(width: 32),
      ],
    );
  }

  Widget _buildWorkspaceProfile(
    BuildContext context,
    Workspace workspace,
  ) {
    final String userNames = workspace.hasLinkedPublishers
        ? workspace.publisherAccountInfo
            .take(3)
            .map((PublisherAccount profile) {
              final int usernameLength = profile.userName.length;
              final int maxChar = 10;

              if (usernameLength <= maxChar) {
                return profile.userName;
              } else {
                return profile.userName.substring(0, maxChar);
              }
            })
            .toList()
            .join(', ')
        : "";

    /// if the user is an admin for this workspace then also
    /// get the count of pending invite requests
    final int inviteCount =
        workspace.role == WorkspaceRole.Admin ? workspace.accessRequests : 0;

    List<Widget> avatars = [];

    if (workspace.hasLinkedPublishers) {
      for (int x = 0; x < workspace.publisherAccountInfo.length; x++) {
        if (x < 3) {
          avatars.add(
            Positioned(
              right: x * 20.0,
              child: Stack(
                alignment: Alignment.center,
                children: <Widget>[
                  Container(
                    height: 44.0,
                    width: 44.0,
                    decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(30.0),
                        color: Theme.of(context).appBarTheme.color),
                  ),
                  UserAvatar(workspace.publisherAccountInfo[x],
                      showPaid: workspace.type == WorkspaceType.Team),
                ],
              ),
            ),
          );
        }
      }
    }

    return Column(
      children: <Widget>[
        ListTile(
          onTap: () => _switchToWorkspace(context, workspace),
          title: Row(
            children: <Widget>[
              Text(
                workspace.type == WorkspaceType.Personal
                    ? _pageContent["personal_profiles"]
                    : workspace.name,
                style: Theme.of(context).textTheme.bodyText1.merge(
                      TextStyle(
                        color: appState.currentWorkspace.id == workspace.id
                            ? Theme.of(context).primaryColor
                            : Theme.of(context).textTheme.bodyText2.color,
                      ),
                    ),
              ),

              /// if there are access join requests, then show badge
              Visibility(
                visible: inviteCount > 0,
                child: FadeInScaleUp(
                  10,
                  Padding(
                    padding: EdgeInsets.only(left: 8.0),
                    child: Badge(
                      elevation: 0.0,
                      color: Theme.of(context).primaryColor,
                      value:
                          inviteCount < 100 ? (inviteCount).toString() : "99+",
                    ),
                  ),
                ),
              )
            ],
          ),
          subtitle: Text(
            userNames == "" ? "No accounts assigned" : userNames,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
                color: appState.currentWorkspace.id == workspace.id
                    ? Theme.of(context).primaryColor
                    : Theme.of(context).hintColor,
                fontSize: 14.0),
          ),
          trailing: userNames == ""
              ? Icon(AppIcons.angleRight)
              : Container(
                  height: 44,
                  width: 80,
                  child: Stack(
                    overflow: Overflow.visible,
                    children: avatars,
                  ),
                ),
        ),
        Divider(height: 1),
      ],
    );
  }
}
