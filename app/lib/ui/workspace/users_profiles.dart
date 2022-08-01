import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/workspace/blocs/users_profiles.dart';

class WorkspaceUsersProfiles extends StatefulWidget {
  final WorkspaceUser user;

  WorkspaceUsersProfiles(this.user);

  @override
  _WorkspaceUsersProfilesState createState() => _WorkspaceUsersProfilesState();
}

class _WorkspaceUsersProfilesState extends State<WorkspaceUsersProfiles> {
  final GlobalKey<ScaffoldState> _scaffoldKey = new GlobalKey<ScaffoldState>();
  final _bloc = WorkspaceUsersProfilesBloc();
  final _scrollControllerAssigned = ScrollController();
  final _scrollControllerToAssign = ScrollController();

  @override
  void initState() {
    super.initState();

    _scrollControllerAssigned.addListener(_onScrollAssigned);
    _scrollControllerToAssign.addListener(_onScrollToAssign);

    _bloc.loadProfilesAssigned(widget.user.userId);
    _bloc.loadProfilesToAssign(widget.user.userId);
  }

  @override
  void dispose() {
    _bloc.dispose();
    _scrollControllerAssigned.dispose();
    _scrollControllerToAssign.dispose();

    super.dispose();
  }

  void _onScrollAssigned() {
    if (_scrollControllerAssigned.offset >=
            _scrollControllerAssigned.position.maxScrollExtent &&
        _bloc.hasMoreAssigned &&
        !_bloc.isLoadingAssigned) {
      _bloc.loadProfilesAssigned(widget.user.userId);
    }
  }

  void _onScrollToAssign() {
    if (_scrollControllerToAssign.offset >=
            _scrollControllerToAssign.position.maxScrollExtent &&
        _bloc.hasMoreToAssign &&
        !_bloc.isLoadingToAssign) {
      _bloc.loadProfilesToAssign(widget.user.userId);
    }
  }

  void _addProfile(PublisherAccount profile) async {
    showSharedModalAlert(context, Text("Are you sure?"),
        content: Text(
            "This will add ${profile.userName} to ${widget.user.name}'s list of accounts."),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            label: "Cancel",
            onPressed: () => Navigator.of(context).pop(false),
          ),
          ModalAlertAction(
              label: "Confirm",
              isDestructiveAction: true,
              onPressed: () {
                showSharedLoadingLogo(context);

                _bloc.linkProfileToUser(widget.user, profile).then((success) {
                  if (success) {
                    Navigator.of(context).pop(success);
                    Navigator.of(context).pop(success);
                  } else {
                    Navigator.of(context).pop();
                    Navigator.of(context).pop(success);
                    showSharedModalError(context,
                        title: "Unable to link this profile",
                        subTitle: "Please try again in a few moments");
                  }
                });
              }),
        ]);
  }

  Future<bool> _confirmRemove(PublisherAccount profile) async {
    return await showSharedModalAlert(context, Text("Are you sure?"),
        content: Text(
            "This will remove ${profile.userName} from ${widget.user.name}'s list of accounts."),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            label: "Cancel",
            onPressed: () => Navigator.of(context).pop(false),
          ),
          ModalAlertAction(
              label: "Remove",
              isDestructiveAction: true,
              onPressed: () {
                showSharedLoadingLogo(context);

                _bloc
                    .unlinkProfileFromUser(widget.user, profile)
                    .then((success) {
                  if (success) {
                    Navigator.of(context).pop(success);
                    Navigator.of(context).pop(success);
                  } else {
                    Navigator.of(context).pop();
                    Navigator.of(context).pop(success);
                    showSharedModalError(context,
                        title: "Unable to remove this profile",
                        subTitle: "Please try again in a few moments");
                  }
                });
              }),
        ]);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      key: _scaffoldKey,
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text(widget.user.name),
        actions: <Widget>[
          IconButton(
            icon: Icon(AppIcons.plus),
            onPressed: () => _scaffoldKey.currentState.openEndDrawer(),
          )
        ],
      ),
      body: StreamBuilder<WorkspacePublisherAccountInfoResponse>(
        stream: _bloc.profilesAssignedResponse,
        builder: (context, snapshot) {
          return snapshot.connectionState == ConnectionState.waiting
              ? _buildLoading()
              : snapshot.data != null && snapshot.data.error == null
                  ? _buildList(snapshot.data)
                  : _buildError(snapshot.data);
        },
      ),
      endDrawer: _buildDrawer(),
    );
  }

  Widget _buildLoading() =>
      ListView(padding: EdgeInsets.all(16), children: [LoadingListShimmer()]);

  Widget _buildError(WorkspacePublisherAccountInfoResponse res) => Padding(
      padding: EdgeInsets.all(16),
      child: RetryError(
        error: res.error,
        onRetry: () => _bloc.loadProfilesAssigned(widget.user.userId),
      ));

  Widget _buildList(WorkspacePublisherAccountInfoResponse res) => Column(
        children: <Widget>[
          Expanded(
            child: RefreshIndicator(
              displacement: 0.0,
              backgroundColor: Theme.of(context).appBarTheme.color,
              color: Theme.of(context).textTheme.bodyText2.color,
              onRefresh: () => _bloc.loadProfilesAssigned(
                widget.user.userId,
                reset: true,
                forceRefresh: true,
              ),
              child: res.models.isEmpty
                  ? ListView(children: <Widget>[_buildNoResults()])
                  : ListView.separated(
                      physics: AlwaysScrollableScrollPhysics(),
                      controller: _scrollControllerAssigned,
                      itemCount: res.models.length,
                      itemBuilder: (BuildContext context, int index) {
                        final PublisherAccount profile = res.models[index];

                        return FadeInBottomTop(
                            (index * 1.5).toDouble(),
                            Dismissible(
                              key: Key(index.toString()),
                              onDismissed: (direction) => null,
                              confirmDismiss:
                                  (DismissDirection direction) async =>
                                      await _confirmRemove(profile),
                              direction: DismissDirection.endToStart,
                              background: Container(
                                color: AppColors.errorRed,
                                padding: EdgeInsets.only(right: 16.0),
                                child: Align(
                                  alignment: Alignment.centerRight,
                                  child: Text(
                                    "Remove",
                                    style: TextStyle(
                                      color: AppColors.white,
                                    ),
                                  ),
                                ),
                              ),
                              child: ListTile(
                                leading: UserAvatar(profile),
                                title: Text(
                                  profile.userName,
                                ),
                              ),
                            ),
                            200);
                      },
                      separatorBuilder: (BuildContext context, int index) =>
                          FadeInBottomTop((index * 1.5).toDouble(),
                              Divider(height: 0), 200),
                    ),
            ),
          ),
        ],
      );

  Widget _buildNoResults() => Container(
        height: MediaQuery.of(context).size.height - 160,
        width: double.infinity,
        padding: EdgeInsets.symmetric(horizontal: 24.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.max,
          children: <Widget>[
            Text("No Profiles", style: Theme.of(context).textTheme.headline6),
            SizedBox(
              height: 8.0,
            ),
            Text(
              "This team member has no profiles assigned to them",
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyText2.merge(
                    TextStyle(color: AppColors.grey300),
                  ),
            ),
          ],
        ),
      );

  Widget _buildDrawer() {
    /// get first name of the team member
    final List<String> fullName = widget.user.name.split(" ");
    final String firstName = fullName[0];

    return ClipRRect(
      borderRadius: BorderRadius.only(
          topLeft: Radius.circular(16), bottomLeft: Radius.circular(16)),
      child: Drawer(
        child: Scaffold(
          backgroundColor: Theme.of(context).appBarTheme.color,
          appBar: AppBar(
            automaticallyImplyLeading: false,
            title: Text("Link Account(s) to $firstName"),
            centerTitle: false,
            backgroundColor: Theme.of(context).appBarTheme.color,
            elevation: 0.0,
          ),
          body: StreamBuilder<WorkspacePublisherAccountInfoResponse>(
            stream: _bloc.profilesToAssignResponse,
            builder: (context, snapshot) => snapshot.data == null
                ? ListView(
                    padding: EdgeInsets.all(16),
                    children: <Widget>[Text("Loading...")],
                  )
                : snapshot.data.error != null
                    ? Padding(
                        padding: EdgeInsets.symmetric(horizontal: 16.0),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          mainAxisSize: MainAxisSize.max,
                          children: <Widget>[
                            Icon(
                              AppIcons.exclamationTriangle,
                              size: 28.0,
                            ),
                            Padding(
                              padding: EdgeInsets.only(top: 16.0, bottom: 4.0),
                              child: Text("Unable to Load",
                                  style: Theme.of(context).textTheme.headline6),
                            ),
                            Text(
                              "We are unable to load your Business Pro accounts.\nPlease try again.",
                              textAlign: TextAlign.center,
                            ),
                            Container(height: kToolbarHeight),
                          ],
                        ),
                      )
                    : snapshot.data.models.length == 0
                        ? Padding(
                            padding: EdgeInsets.symmetric(horizontal: 16.0),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              mainAxisSize: MainAxisSize.max,
                              children: <Widget>[
                                Icon(
                                  AppIcons.link,
                                  size: 28.0,
                                ),
                                Padding(
                                  padding:
                                      EdgeInsets.only(top: 16.0, bottom: 4.0),
                                  child: Text("All Profiles Linked",
                                      style: Theme.of(context)
                                          .textTheme
                                          .headline6),
                                ),
                                Text(
                                  "${widget.user.name} is connected to all of your Business Pro accounts.",
                                  textAlign: TextAlign.center,
                                ),
                                Container(height: kToolbarHeight),
                              ],
                            ),
                          )
                        : RefreshIndicator(
                            displacement: 0.0,
                            backgroundColor:
                                Theme.of(context).appBarTheme.color,
                            color: Theme.of(context).textTheme.bodyText2.color,
                            onRefresh: () => _bloc.loadProfilesToAssign(
                              widget.user.userId,
                              reset: true,
                              forceRefresh: true,
                            ),
                            child: ListView(
                                children: snapshot.data.models
                                    .map((PublisherAccount profile) => ListTile(
                                          leading: UserAvatar(profile),
                                          title: Text(profile.userName),
                                          trailing: IconButton(
                                            icon: Icon(AppIcons.plus),
                                            onPressed: () =>
                                                _addProfile(profile),
                                          ),
                                        ))
                                    .toList()),
                          ),
          ),
        ),
      ),
    );
  }
}
