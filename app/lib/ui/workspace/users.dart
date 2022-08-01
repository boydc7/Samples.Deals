import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/ui/shared/widgets/workspace_user_avatar.dart';
import 'package:rydr_app/ui/workspace/blocs/users.dart';
import 'package:rydr_app/ui/workspace/users_profiles.dart';

class WorkspaceUsers extends StatefulWidget {
  @override
  _WorkspaceUsersState createState() => _WorkspaceUsersState();
}

class _WorkspaceUsersState extends State<WorkspaceUsers> {
  final _bloc = WorkspaceUsersBloc();
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();

    _scrollController.addListener(_onScroll);

    _bloc.loadUsers();
  }

  @override
  void dispose() {
    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.loadUsers();
    }
  }

  void _goToUserProfiles(WorkspaceUser user) =>
      Navigator.of(context).push(MaterialPageRoute(
          builder: (BuildContext context) => WorkspaceUsersProfiles(user),
          settings: AppAnalytics.instance
              .getRouteSettings('workspace/users/profiles')));

  Future<bool> _confirmRemove(WorkspaceUser user) async {
    return await showSharedModalAlert(context, Text("Are you sure?"),
        content: Text(
            "This will remove ${user.name} from the ${appState.currentWorkspace.name} team and disconnect them from all Instagram accounts."),
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

                _bloc.removeUser(user).then((success) {
                  if (success) {
                    Navigator.of(context).pop(success);
                    Navigator.of(context).pop(success);
                  } else {
                    Navigator.of(context).pop();
                    Navigator.of(context).pop(success);
                    showSharedModalError(context,
                        title: "Unable to remove this user",
                        subTitle: "Please try again in a few moments");
                  }
                });
              }),
        ]);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text("Team Members"),
              Text(
                appState.currentWorkspace.name,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        color: Theme.of(context).hintColor,
                      ),
                    ),
              ),
            ],
          ),
        ),
        body: StreamBuilder<WorkspaceUsersResponse>(
          stream: _bloc.usersResponse,
          builder: (context, snapshot) {
            return snapshot.connectionState == ConnectionState.waiting
                ? _buildLoading()
                : snapshot.data != null && snapshot.data.error == null
                    ? _buildList(snapshot.data)
                    : _buildError(snapshot.data);
          },
        ),
      );

  Widget _buildLoading() => ListView(
      padding: EdgeInsets.all(16), children: <Widget>[LoadingListShimmer()]);

  Widget _buildError(WorkspaceUsersResponse res) => Padding(
      padding: EdgeInsets.all(16),
      child: RetryError(
        error: res.error,
        onRetry: _bloc.loadUsers,
      ));

  Widget _buildList(WorkspaceUsersResponse res) => RefreshIndicator(
        displacement: 0.0,
        backgroundColor: Theme.of(context).appBarTheme.color,
        color: Theme.of(context).textTheme.bodyText2.color,
        onRefresh: () => _bloc.loadUsers(forceRefresh: true),
        child: res.models.isEmpty
            ? ListView(children: <Widget>[_buildNoResults()])
            : ListView.separated(
                physics: AlwaysScrollableScrollPhysics(),
                controller: _scrollController,
                itemCount: res.models.length,
                itemBuilder: (BuildContext context, int index) {
                  final WorkspaceUser user = res.models[index];

                  return user.workspaceRole == WorkspaceRole.Admin
                      ? FadeInBottomTop(
                          (index * 1.5).toDouble(),
                          ListTile(
                            leading: WorkspaceUserAvatar(user),
                            title: Text(
                              user.name,
                              style: TextStyle(fontWeight: FontWeight.w600),
                            ),
                            subtitle: Text(
                              workspaceRoleToString(user.workspaceRole),
                              style: TextStyle(
                                  color: Theme.of(context).hintColor,
                                  fontSize: 14.0),
                            ),
                            onTap: () => _goToUserProfiles(user),
                          ),
                          200)
                      : FadeInBottomTop(
                          (index * 1.5).toDouble(),
                          Dismissible(
                            key: Key(index.toString()),
                            onDismissed: (direction) => null,
                            confirmDismiss:
                                (DismissDirection direction) async =>
                                    await _confirmRemove(user),
                            direction: DismissDirection.endToStart,
                            background: Container(
                              color: AppColors.errorRed,
                              padding: EdgeInsets.only(right: 16.0),
                              child: Align(
                                alignment: Alignment.centerRight,
                                child: Text(
                                  "Remove",
                                  style: TextStyle(color: AppColors.white),
                                ),
                              ),
                            ),
                            child: ListTile(
                              leading: WorkspaceUserAvatar(user),
                              title: Text(
                                user.name,
                                style: TextStyle(fontWeight: FontWeight.w600),
                              ),
                              subtitle: Text(
                                workspaceRoleToString(user.workspaceRole),
                                style: TextStyle(
                                    color: Theme.of(context).hintColor,
                                    fontSize: 14.0),
                              ),
                              onTap: () => _goToUserProfiles(user),
                            ),
                          ),
                          200);
                },
                separatorBuilder: (BuildContext context, int index) =>
                    FadeInBottomTop(
                        (index * 1.5).toDouble(), Divider(height: 0), 200),
              ),
      );

  Widget _buildNoResults() => Container(
        height: MediaQuery.of(context).size.height - 160,
        padding: EdgeInsets.symmetric(horizontal: 24.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.max,
          children: <Widget>[
            Text("No Team Members",
                style: Theme.of(context).textTheme.headline6),
            SizedBox(
              height: 8.0,
            ),
            Text(
              "There are no active team members in this workspace",
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyText2.merge(
                    TextStyle(color: AppColors.grey300),
                  ),
            ),
          ],
        ),
      );
}
