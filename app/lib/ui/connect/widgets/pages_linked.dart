import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/ui/connect/blocs/pages.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class ConnectPagesLinked extends StatefulWidget {
  ConnectPagesLinked();

  @override
  ConnectPagesLinkedState createState() => ConnectPagesLinkedState();
}

class ConnectPagesLinkedState extends State<ConnectPagesLinked> {
  final _bloc = ConnectPagesBloc.instance;
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();

    /// load linked profiles to the workspace we're in now
    /// this could error out, mostly in team workspaces if a non-owner of a team workspace
    /// is linked to profiles but none of them have any subscriptions that are valid
    _bloc.loadLinkedProfiles(reset: true);

    /// attach scroll listerner to show large/small fab
    /// and detect when near bottom to trigger loading more linked profiles if available
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.offset > 10) {
      _bloc.setShowSmallFab(true);
    } else {
      _bloc.setShowSmallFab(false);
    }

    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.loadLinkedProfiles();
    }
  }

  void _navigateToUser(BuildContext context, PublisherAccount profile) async {
    showSharedLoadingLogo(context);

    /// switch to the desired user, this updated client app state
    /// and saves the current user id in device settings
    final bool switched = await appState.switchProfile(profile.id);

    /// close loading overlay
    Navigator.of(context).pop();

    /// show an error if we're unable to swtich to this profile
    if (!switched) {
      showSharedModalError(
        context,
        title: "Profile Unavailable",
        subTitle:
            "Unable to switch to this profile, please try again in a few moments",
      );

      return;
    }

    /// log this as a screen change
    AppAnalytics.instance.logScreen('profile/switched');

    /// if we've never onboarded this type of user then send them through
    /// to the onboarding flow, otherwise go to their respective homepage
    Navigator.of(context).pushNamedAndRemoveUntil(
        appState.getInitialRoute(), (Route<dynamic> route) => false);
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    /// if we don't have a current workspace in app state then bail out
    /// while this should relistically not happen, there may be instances on re-builds
    if (appState.currentWorkspace == null ||
        appState.workspaces == null ||
        appState.workspaces.length == 0) {
      return Container(height: 0);
    }

    /// show a list of linked profiles to the current workspace (personal or team)
    return Stack(
      children: <Widget>[
        RefreshIndicator(
          displacement: 0.0,
          backgroundColor: Theme.of(context).appBarTheme.color,
          color: Theme.of(context).textTheme.bodyText2.color,
          onRefresh: _bloc.refreshPages,
          child: StreamBuilder<WorkspacePublisherAccountInfoResponse>(
            stream: _bloc.linkedProfilesResponse,
            builder: (context, snapshot) {
              /// total number of linked profiles now in the response (which is paged)
              /// so this would be ever increasing as we add more pages while the user scrolls
              final int total = snapshot.data?.models?.length ?? 0;

              if (snapshot.data != null && snapshot.data.hasError) {
                return RetryError(
                  error: snapshot.data.error,
                  onRetry: _bloc.refreshPages,
                );
              }

              /// if we have no linked profiles yet
              if (snapshot.data != null && total == 0) {
                return _buildNoProfiles(context, dark);
              }

              /// show list while and after loading
              return ListView.separated(
                controller: _scrollController,
                physics: AlwaysScrollableScrollPhysics(),
                separatorBuilder: (context, index) => Divider(height: 0),
                itemCount: total,
                itemBuilder: (BuildContext context, int index) =>
                    _buildLinkedProfile(
                  context,
                  dark,
                  snapshot.data.models[index],
                  index,
                  total,
                ),
              );
            },
          ),
        ),
        _buildAddAccount(context, dark),
      ],
    );
  }

  Widget _buildNoProfiles(BuildContext context, bool dark) {
    final bool isTeam =
        appState.currentWorkspace.type != WorkspaceType.Personal;

    return Container(
      width: MediaQuery.of(context).size.width,
      height: MediaQuery.of(context).size.height - 160,
      padding: EdgeInsets.symmetric(horizontal: 24.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        mainAxisAlignment: MainAxisAlignment.center,
        mainAxisSize: MainAxisSize.max,
        children: <Widget>[
          Text(isTeam ? "No Accounts Assigned" : "No Connected Profiles",
              style: Theme.of(context).textTheme.headline6),
          SizedBox(height: 8.0),
          Text(
            isTeam
                ? "Team accounts assigned to you will show here."
                : "Tap + Add to connect your Instagram profile(s)",
            textAlign: TextAlign.center,
            style: Theme.of(context)
                .textTheme
                .bodyText2
                .merge(TextStyle(color: Theme.of(context).hintColor)),
          ),
          SizedBox(height: 16.0),
          isTeam
              ? SecondaryButton(label: 'Refresh', onTap: _bloc.refreshPages)
              : Container()
        ],
      ),
    );
  }

  /// list item of each linked profiles
  Widget _buildLinkedProfile(
    BuildContext context,
    bool dark,
    PublisherAccount profile,
    int index,
    int total,
  ) {
    return ListTileTheme(
      textColor: Theme.of(context).textTheme.bodyText2.color,
      contentPadding: EdgeInsets.only(left: 16, top: 4, bottom: 4, right: 16),
      child: ListTile(
        leading: UserAvatar(profile,
            showPaid: appState.currentWorkspace.type == WorkspaceType.Team),
        title: Text(profile.userName,
            style: TextStyle(
                fontWeight: FontWeight.w600,
                color: appState.currentProfile?.id == profile.id
                    ? Theme.of(context).primaryColor
                    : Theme.of(context).textTheme.headline4.color)),
        subtitle: Row(
          children: <Widget>[
            Text(
              profile.isBusiness
                  ? appState.currentWorkspace.type == WorkspaceType.Team
                      ? "Business Pro"
                      : "Business"
                  : "Creator",
              style: TextStyle(
                  color: appState.currentProfile?.id == profile.id
                      ? Theme.of(context).primaryColor
                      : Theme.of(context).hintColor,
                  fontSize: 14.0),
            ),

            /// don't show followers for soft-linked accounts
            profile.isAccountSoft
                ? Container()
                : Text(
                    ' · ${profile.publisherMetrics.followedByDisplay} followers',
                    style: TextStyle(
                        color: appState.currentProfile?.id == profile.id
                            ? Theme.of(context).primaryColor
                            : Theme.of(context).hintColor,
                        fontSize: 14.0))
          ],
        ),
        trailing: Visibility(
          visible: profile.unreadNotifications > 0,
          child: FadeInScaleUp(
            10,
            Badge(
              elevation: 0.0,
              large: true,
              color: Theme.of(context).primaryColor,
              valueColor: Theme.of(context).scaffoldBackgroundColor,
              value: profile.unreadNotifications.toString(),
            ),
          ),
        ),
        onTap: () => _navigateToUser(context, profile),
      ),
    );
  }

  Widget _buildAddAccount(BuildContext context, bool dark) => Positioned(
        right: 16,
        bottom: MediaQuery.of(context).padding.bottom + 16,
        child: FadeInScaleUp(
          10,
          StreamBuilder<bool>(
            stream: _bloc.smallFab,
            builder: (context, snapshot) => InkWell(
              borderRadius: BorderRadius.circular(40),
              onTap: _bloc.toggleShowConnectedProfiles,
              child: AnimatedContainer(
                duration: Duration(milliseconds: 250),
                height: 56,
                width:
                    snapshot.data != null && snapshot.data == true ? 56 : 110,
                decoration: BoxDecoration(
                  boxShadow: AppShadows.elevation[1],
                  borderRadius: BorderRadius.circular(40),
                  color: Theme.of(context).textTheme.bodyText1.color,
                ),
                child: Align(
                  alignment: Alignment.center,
                  child: ListView(
                    padding: EdgeInsets.only(left: 16),
                    scrollDirection: Axis.horizontal,
                    children: <Widget>[
                      Icon(
                        AppIcons.plus,
                        color: Theme.of(context).appBarTheme.color,
                      ),
                      Container(
                        height: 56,
                        padding: EdgeInsets.only(left: 8),
                        child: Center(
                          child: AnimatedOpacity(
                            duration: Duration(milliseconds: 250),
                            opacity:
                                snapshot.data != null && snapshot.data == true
                                    ? 0
                                    : 1,
                            child: Text(
                              "ADD",
                              style: Theme.of(context)
                                  .textTheme
                                  .bodyText1
                                  .merge(
                                    TextStyle(
                                      fontSize: 17,
                                      color:
                                          Theme.of(context).appBarTheme.color,
                                      letterSpacing: 0.7,
                                    ),
                                  ),
                            ),
                          ),
                        ),
                      )
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      );
}
