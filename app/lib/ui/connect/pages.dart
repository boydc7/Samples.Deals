import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/workspace_features.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/ui/connect/business_finder.dart';
import 'package:rydr_app/ui/connect/widgets/pages_unlinked.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/workspace/settings.dart';
import 'package:rydr_app/ui/workspace/widgets/selector.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/connect/widgets/pages_linked.dart';
import 'package:rydr_app/ui/connect/blocs/pages.dart';
import 'package:rydr_app/models/enums/workspace.dart';

class ConnectPagesPage extends StatefulWidget {
  ConnectPagesPage({
    Key key,
  }) : super(key: key);

  @override
  State<StatefulWidget> createState() => _ConnectPagesPageState();
}

class _ConnectPagesPageState extends State<ConnectPagesPage> {
  /// NOTE: don't dispose the connect pages bloc
  /// its a singleton we use on other pages
  final ConnectPagesBloc _bloc = ConnectPagesBloc.instance;
  final GlobalKey<ScaffoldState> _scaffoldKey = GlobalKey<ScaffoldState>();

  @override
  void initState() {
    super.initState();

    /// set the flag that dictates if we should show connected vs connect new profile page
    /// based on if the current workspace has linked profiles or not
    _bloc.setShowingConnectedProfiles(appState.currentWorkspace != null &&
            appState.currentWorkspace.hasLinkedPublishers
        ? true
        : false);
  }

  void _showMasterAccountOptions(BuildContext context) {
    showSharedModalBottomActions(
      context,
      title: 'Account Options',
      actions: [
        ModalBottomAction(
            child: Text(appState.hasTeamsEnabled()
                ? "Refresh Profiles & Teams"
                : "Refresh Profiles"),
            onTap: () {
              _bloc.refreshPages();
              Navigator.of(context).pop();
            }),

        /// must have workspace feature enabled
        WorkspaceFeatures.hasBusinessFinder(
                appState.currentWorkspace.workspaceFeatures)
            ? ModalBottomAction(
                child: Text("Link Instagram Business"),
                onTap: () {
                  Navigator.of(context).pop();
                  Navigator.of(context).push(MaterialPageRoute(
                      fullscreenDialog: true,
                      builder: (context) => ConnectBusinessFinder()));
                })
            : null,

        ModalBottomAction(
            child: Text(
                "Logout ${appState.masterUser.authType == AuthType.Apple ? "of Apple" : "of Google"}"),
            onTap: () {
              Navigator.of(context).pop();
              showSharedModalBottomActions(context,
                  title: "Are you sure?",
                  actions: [
                    ModalBottomAction(
                        isDestructiveAction: true,
                        child: Text("Yes, log me out of RYDR"),
                        onTap: () {
                          Navigator.of(context).pop();

                          showSharedLoadingLogo(context);

                          AuthenticationService.instance()
                              .signOut(true)
                              .then((_) {
                            /// send the user back to the page that one would see if they were to install the app fresh
                            Navigator.of(context).pushNamedAndRemoveUntil(
                                AppRouting.getAuthenticate,
                                (Route<dynamic> route) => false);
                          });
                        })
                  ]);
            },
            icon: AppIcons.signOutAlt)
      ].where((i) => i != null).toList(),
    );
  }

  void _showWorkspaceOptions() {
    showSharedModalBottomInfo(
      context,
      title: appState.workspaces.length > 1
          ? "Business Pro Accounts"
          : "Business Pro",
      largeTitle: appState.workspaces.length > 1 ? false : true,
      subtitle: appState.workspaces.length > 1
          ? "Choose a team or select your personal profiles"
          : "Web access, team management, reporting, \nCreator invites, and more.",
      child: WorkspaceSheet(context),
      initialRatio: appState.workspaces.length > 1 ? 0.5 : 0.44,
      topWidget: appState.workspaces.length == 1
          ? Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Container(
                  width: 87,
                  height: 87,
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      colors: [
                        Theme.of(context).primaryColor,
                        AppColors.successGreen,
                        Colors.yellowAccent,
                      ],
                      stops: [0.1, 0.5, 0.9],
                      begin: Alignment.topRight,
                      end: Alignment.bottomLeft,
                    ),
                    borderRadius: BorderRadius.circular(80),
                  ),
                ),
                Container(
                  width: 82,
                  height: 82,
                  decoration: BoxDecoration(
                    color: Theme.of(context).appBarTheme.color,
                    borderRadius: BorderRadius.circular(80),
                  ),
                ),
                Container(
                  height: 50.0,
                  width: 50.0,
                  decoration: BoxDecoration(
                    image: DecorationImage(
                      image: AssetImage("assets/icons/pro-icon.png"),
                    ),
                  ),
                ),
              ],
            )
          : null,
    );
  }

  @override
  Widget build(BuildContext context) {
    /// is this a workspace that is a 'team' and not a personal workspace?
    final bool isWorkspace = appState.currentWorkspace != null &&
        appState.currentWorkspace.type == WorkspaceType.Team;

    return StreamBuilder<bool>(
        stream: _bloc.showingConnectedProfiles,
        builder: (context, snapshot) {
          final bool showingConnectedProfiles =
              snapshot.data == null || snapshot.data == true;

          return Scaffold(
            key: _scaffoldKey,
            appBar: _buildAppBar(isWorkspace, showingConnectedProfiles),
            body: Column(
              children: <Widget>[
                Expanded(
                  child: !showingConnectedProfiles
                      ? ConnectPagesUnLinked()
                      : ConnectPagesLinked(),
                ),
              ],
            ),
            bottomNavigationBar: showingConnectedProfiles
                ? BottomAppBar(
                    color: Theme.of(context).appBarTheme.color,
                    child: _buildFooter(),
                  )
                : null,
            endDrawer: isWorkspace
                ? ClipRRect(
                    borderRadius: BorderRadius.only(
                        topLeft: Radius.circular(16),
                        bottomLeft: Radius.circular(16)),
                    child: Drawer(child: WorkspaceSettings()))
                : null,
          );
        });
  }

  Widget _buildAppBar(bool isWorkspace, bool showingConnectedProfiles) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final bool hasTeamsEnabled = appState.hasTeamsEnabled();

    /// determine if we can 'go back', this is dependent on having a valid stack to 'pop'
    /// and a current profile in the app state, otherwise the user can't close out of this page
    final bool canPop =
        Navigator.of(context).canPop() && appState.currentProfile != null;

    /// get flag if we have linked publishers profiles from the current workspace the user is in
    /// this will help us determine what page we'll start them off with (e.g. existing linked, or 'link' a profille)
    /// thought this further depends on the type of workspace they are on and whether or not they're an admin on it
    final bool hasLinkedPublishers = appState.currentWorkspace != null
        ? appState.currentWorkspace.hasLinkedPublishers
        : false;

    /// is this a team workspace, and is the current logged in master user an admin on it
    final bool isWorkspaceAdmin =
        isWorkspace && appState.currentWorkspace.role == WorkspaceRole.Admin;

    /// does this workspace have any join requests?
    final bool workspaceHasRequests =
        isWorkspaceAdmin && appState.currentWorkspace.accessRequests > 0;

    /// this is the appbar title widget for when we're on the linked profiles page
    /// (not the connect 'additional' profile)
    final Widget profilesTitle = GestureDetector(
      onTap: hasTeamsEnabled ? _showWorkspaceOptions : null,
      child: Container(
        width: double.infinity,
        height: 48.0,
        color: Theme.of(context).appBarTheme.color,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(isWorkspace
                ? appState.currentWorkspace.name
                : "Personal Profiles"),
            hasTeamsEnabled
                ? Stack(
                    alignment: Alignment.center,
                    children: <Widget>[
                      Container(
                        margin: EdgeInsets.only(left: 4.0),
                        height: 22.0,
                        width: 22.0,
                        decoration: BoxDecoration(
                          gradient: LinearGradient(
                            colors: [
                              Theme.of(context).primaryColor,
                              AppColors.successGreen,
                              Colors.yellowAccent,
                            ],
                            stops: [0.1, 0.5, 0.9],
                            begin: Alignment.topRight,
                            end: Alignment.bottomLeft,
                          ),
                          color: Theme.of(context).primaryColor,
                          borderRadius: BorderRadius.circular(20.0),
                        ),
                      ),
                      Container(
                        margin: EdgeInsets.only(left: 4.0),
                        height: 19.0,
                        width: 19.0,
                        foregroundDecoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(20.0),
                          gradient: LinearGradient(
                            colors: [
                              dark
                                  ? Theme.of(context).primaryColor
                                  : Theme.of(context).appBarTheme.color,
                              dark
                                  ? AppColors.successGreen
                                  : Theme.of(context).appBarTheme.color,
                              dark
                                  ? Colors.yellowAccent
                                  : Theme.of(context).appBarTheme.color,
                            ],
                            stops: [0.0, 0.5, 1.0],
                            begin: Alignment.topRight,
                            end: Alignment.bottomLeft,
                          ),
                          backgroundBlendMode: BlendMode.multiply,
                        ),
                        decoration: BoxDecoration(
                          color: Theme.of(context).appBarTheme.color,
                          borderRadius: BorderRadius.circular(20.0),
                        ),
                        child: Center(
                          child: Icon(
                            AppIcons.plus,
                            color: dark
                                ? Theme.of(context).textTheme.bodyText1.color
                                : Theme.of(context).primaryColor,
                            size: 14.0,
                          ),
                        ),
                      ),
                    ],
                  )
                : hasTeamsEnabled ? Icon(AppIcons.angleDownReg) : Container(),
          ],
        ),
      ),
    );

    return AppBar(
      leading: !showingConnectedProfiles
          ? hasLinkedPublishers
              ? AppBarBackButton(
                  context,
                  onPressed: _bloc.toggleShowConnectedProfiles,
                )
              : Container()
          : canPop ? AppBarCloseButton(context) : Container(),
      title: showingConnectedProfiles
          ? profilesTitle
          : Text(hasLinkedPublishers ? "Add Accounts" : "Connect Profile"),
      actions: <Widget>[
        !showingConnectedProfiles
            ? Container(width: kMinInteractiveDimension)
            : isWorkspace
                ? Padding(
                    padding: EdgeInsets.only(right: 4.0),
                    child: Stack(
                      overflow: Overflow.visible,
                      children: <Widget>[
                        IconButton(
                          icon: Icon(AppIcons.cog),
                          onPressed: () =>
                              _scaffoldKey.currentState.openEndDrawer(),
                        ),
                        workspaceHasRequests
                            ? Positioned(
                                bottom: 18.0,
                                right: 28,
                                child: Badge(
                                  elevation: 0.0,
                                  color: Theme.of(context).primaryColor,
                                  value: appState
                                              .currentWorkspace.accessRequests >
                                          99
                                      ? "99+"
                                      : appState.currentWorkspace.accessRequests
                                          .toString(),
                                ),
                              )
                            : Container()
                      ],
                    ),
                  )
                : Container(width: kMinInteractiveDimension),
      ],
    );
  }

  Widget _buildFooter() => Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          ListTile(
            onTap: () => _showMasterAccountOptions(context),
            leading: appState.masterUser.authType == AuthType.Apple
                ? Icon(AppIcons.apple,
                    color: Theme.of(context).textTheme.bodyText2.color)
                : Container(
                    height: 24,
                    width: 24,
                    decoration: BoxDecoration(
                      image: DecorationImage(
                        fit: BoxFit.cover,
                        image: AssetImage(
                          'assets/icons/google-icon.png',
                        ),
                      ),
                    ),
                  ),
            title: Text(appState.masterUser.nameDisplay),
            trailing: Padding(
              padding: EdgeInsets.only(right: 4.0),
              child: Icon(
                AppIcons.ellipsisV,
                color: Theme.of(context).textTheme.bodyText1.color,
              ),
            ),
          ),
        ],
      );
}
