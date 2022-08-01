import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/ui/workspace/blocs/settings.dart';
import 'package:share/share.dart';

class WorkspaceSettings extends StatefulWidget {
  @override
  _WorkspaceSettingsState createState() => _WorkspaceSettingsState();
}

class _WorkspaceSettingsState extends State<WorkspaceSettings> {
  final WorkspaceSettingsBloc _bloc = WorkspaceSettingsBloc();

  final _codeStyle = TextStyle(
      fontSize: 28.0,
      letterSpacing: 2.0,
      wordSpacing: 1.0,
      fontWeight: FontWeight.w600);

  ThemeData _theme;
  Size _size;

  @override
  void initState() {
    super.initState();
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  Future<void> _refresh() async => _bloc.load(true);

  /// NOTE: not implemented yet
  // void _leaveTeam() => showSharedModalAlert(context, Text("Are you sure?"),
  //         content: Text(
  //             "This will remove all your access to this team and your assigned Instagram accounts"),
  //         actions: [
  //           ModalAlertAction(
  //               label: "Cancel", onPressed: () => Navigator.of(context).pop()),
  //           ModalAlertAction(
  //               label: "Leave Team",
  //               isDestructiveAction: true,
  //               onPressed: () {
  //                 Navigator.of(context).pop();
  //                 showSharedLoadingLogo(context);

  //                 // _bloc.leaveTeam().then((success){

  //                 // });
  //               })
  //         ]);

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);
    _size = MediaQuery.of(context).size;

    /// check if the current user is a workspace admin, if so we show links to requests & members
    /// if not then we have a button for the current user to remove themselves from the team
    final bool isWorkspaceAdmin =
        appState.currentWorkspace.role == WorkspaceRole.Admin;

    return Scaffold(
      backgroundColor: _theme.appBarTheme.color,
      appBar: AppBar(
        /*
        leading: Padding(
          padding: EdgeInsets.only(left: 0.0),
          child: IconButton(
            icon: Icon(AppIcons.infoCircle),
            color: _theme.hintColor,
            onPressed: () {},
          ),
        ),
        */
        leading: AppBarCloseButton(context),
        title: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(appState.currentWorkspace.name),
            Text(
              "Busines Pro",
              style: _theme.textTheme.caption.merge(
                TextStyle(
                  color: _theme.hintColor,
                ),
              ),
            ),
          ],
        ),
        centerTitle: true,
        elevation: 0.0,
      ),
      body: Column(children: <Widget>[
        Expanded(
            child: StreamBuilder<WorkspaceResponse>(
                stream: _bloc.workspaceResponse,
                builder: (context, snapshot) {
                  final Workspace workspace =
                      snapshot.data == null || snapshot.data.model == null
                          ? appState.currentWorkspace
                          : snapshot.data.model;

                  return RefreshIndicator(
                    displacement: 0.0,
                    backgroundColor: _theme.appBarTheme.color,
                    color: _theme.textTheme.bodyText2.color,
                    onRefresh: _refresh,
                    child: ListView(
                      children: <Widget>[
                        SizedBox(height: 16.0),
                        _buildCode(workspace),
                        SizedBox(height: 32.0),
                        Visibility(
                          visible:
                              isWorkspaceAdmin && workspace.accessRequests > 0,
                          child: Padding(
                            padding: EdgeInsets.symmetric(
                                horizontal: 16.0, vertical: 4.0),
                            child: SecondaryButton(
                              hasBadge: true,
                              context: context,
                              label: "Invites to Join Team",
                              badgeColor: _theme.primaryColor,
                              badgeCount: workspace.accessRequests,
                              onTap: () => Navigator.of(context)
                                  .pushNamed(AppRouting.getWorkspaceRequests),
                            ),
                          ),
                        ),
                        Visibility(
                          visible: isWorkspaceAdmin,
                          child: Padding(
                            padding: EdgeInsets.symmetric(
                                horizontal: 16.0, vertical: 4.0),
                            child: SecondaryButton(
                              context: context,
                              label: "Team Members",
                              onTap: () => Navigator.of(context)
                                  .pushNamed(AppRouting.getWorkspaceUsers),
                            ),
                          ),
                        ),
                      ],
                    ),
                  );
                })),

        /// NOTE: not implemented yet
        // Visibility(
        //     visible: !isWorkspaceAdmin,
        //     child: SafeArea(
        //         bottom: true,
        //         child: Padding(
        //             padding: EdgeInsets.symmetric(horizontal: 16),
        //             child: SecondaryButton(
        //               context: context,
        //               label: "Leave Team",
        //               fullWidth: true,
        //               onTap: _leaveTeam,
        //             )))),
      ]),
      /*
      bottomNavigationBar: BottomAppBar(
        color: dark
            ? _theme.canvasColor
            : _theme.scaffoldBackgroundColor,
        child: GestureDetector(
          onTap: () {},
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Container(
                padding: EdgeInsets.all(16),
                child: Center(
                  child: Text(
                    "Launch Admin Portal",
                    style: _theme.textTheme.bodyText1,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
      */
    );
  }

  Widget _buildCircle(bool gradient) => Container(
        width: gradient ? _size.width / 2 : (_size.width / 2) - 8,
        height: gradient ? _size.width / 2 : (_size.width / 2) - 8,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(_size.width),
          gradient: LinearGradient(
            colors: [
              gradient ? _theme.primaryColor : _theme.appBarTheme.color,
              gradient ? AppColors.successGreen : _theme.appBarTheme.color,
              gradient ? Colors.yellowAccent : _theme.appBarTheme.color,
            ],
            stops: [0.1, 0.5, 0.9],
            begin: Alignment.topRight,
            end: Alignment.bottomLeft,
          ),
          boxShadow: [
            BoxShadow(
              color: Colors.white.withOpacity(0.1),
              blurRadius: 16.0,
              spreadRadius: 0.0,
              offset: Offset(
                0.0,
                0.0,
              ),
            ),
          ],
        ),
      );

  Widget _buildCode(Workspace workspace) {
    /// after initially creating a new workspace, there's a chance we don't yet have an invite code
    /// generated, so in those cases don't show the invite code in the circle but rather a message
    /// asking the user to pull-refresh and retrieve the code
    final String inviteCode = workspace.inviteCode;
    final String shareMessage =
        "Use $inviteCode to join the ${workspace.name} RYDR team.";

    return GestureDetector(
      onTap: inviteCode != null
          ? () => Share.share(shareMessage,
              subject: "Join the ${workspace.name} Team")
          : null,
      child: Stack(
        alignment: Alignment.bottomCenter,
        overflow: Overflow.visible,
        children: <Widget>[
          Stack(
            alignment: Alignment.center,
            children: <Widget>[
              _buildCircle(true),
              _buildCircle(false),
              Padding(
                padding: EdgeInsets.only(top: 22.0),
                child:

                    /// if we don't yet have an invite code on the workspace settings
                    /// then show a message to refresh the drawer instead
                    Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      inviteCode != null
                          ? inviteCode.replaceAllMapped(
                              RegExp(r".{3}"), (match) => "${match.group(0)} ")
                          : "Invite code",
                      style: _codeStyle,
                      textAlign: TextAlign.center,
                    ),
                    Text(
                      inviteCode != null
                          ? "Share this code with\nothers to join team"
                          : "Pull to refresh",
                      style: _theme.textTheme.caption.merge(
                        TextStyle(
                          color: _theme.hintColor,
                        ),
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              ),
            ],
          ),
          inviteCode != null
              ? Positioned(
                  bottom: -20,
                  child: Container(
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(30),
                      boxShadow: AppShadows.elevation[0],
                      color: _theme.appBarTheme.color.withOpacity(0.95),
                    ),
                    child: IconButton(
                        icon: Icon(
                          AppIcons.shareSolid,
                          color: Color(0xFFa0dd37),
                        ),
                        onPressed: () => Share.share(shareMessage)),
                  ),
                )
              : Container()
        ],
      ),
    );
  }
}
