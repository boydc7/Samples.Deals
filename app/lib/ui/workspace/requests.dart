import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/responses/workspaces.dart';
import 'package:rydr_app/models/workspace.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';
import 'package:rydr_app/ui/shared/widgets/workspace_user_avatar.dart';
import 'package:rydr_app/ui/workspace/blocs/requests.dart';

class WorkspaceRequests extends StatefulWidget {
  @override
  _WorkspaceRequestsState createState() => _WorkspaceRequestsState();
}

class _WorkspaceRequestsState extends State<WorkspaceRequests> {
  final _bloc = WorkspaceRequestsBloc();
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();

    _scrollController.addListener(_onScroll);

    _bloc.loadRequests();
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
      _bloc.loadRequests();
    }
  }

  void _handleAccept(WorkspaceAccessRequest request) {
    showSharedModalAlert(context, Text("Accept Request"),
        content: Text(
            "Accept ${request.user.name}'s request to join ${appState.currentWorkspace.name}?"),
        actions: [
          ModalAlertAction(
              label: "Cancel", onPressed: () => Navigator.of(context).pop()),
          ModalAlertAction(
            isDefaultAction: true,
            label: "Accept",
            onPressed: () => _updateRequest(request, false),
          ),
        ]);
  }

  void _handleDecline(WorkspaceAccessRequest request) {
    showSharedModalAlert(context, Text("Decline Request"),
        content: Text(
            "Decline ${request.user.name}'s request to join  ${appState.currentWorkspace.name}?"),
        actions: [
          ModalAlertAction(
              label: "Cancel", onPressed: () => Navigator.of(context).pop()),
          ModalAlertAction(
            label: "Decline",
            onPressed: () => _updateRequest(request, true),
          ),
        ]);
  }

  void _updateRequest(WorkspaceAccessRequest request, bool decline) {
    Navigator.of(context).pop();

    _bloc.updateRequest(request, decline).then((success) {
      if (!success) {
        showSharedModalError(context);
      }
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text("Invites to Join Team"),
              Text(
                "Accept or deny requests to join ${appState.currentWorkspace.name}",
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        color: Theme.of(context).hintColor,
                      ),
                    ),
              ),
            ],
          ),
        ),
        body: StreamBuilder<WorkspaceAccessRequestsResponse>(
            stream: _bloc.requestsResponse,
            builder: (context, snapshot) =>
                snapshot.connectionState == ConnectionState.waiting
                    ? _buildLoading()
                    : snapshot.data != null && snapshot.data.error == null
                        ? _buildList(snapshot.data)
                        : _buildError(snapshot.data)),
      );

  Widget _buildLoading() =>
      ListView(padding: EdgeInsets.all(16), children: [LoadingListShimmer()]);

  Widget _buildError(WorkspaceAccessRequestsResponse res) => Padding(
      padding: EdgeInsets.all(16),
      child: RetryError(
        error: res.error,
        onRetry: _bloc.loadRequests,
      ));

  Widget _buildList(WorkspaceAccessRequestsResponse res) => RefreshIndicator(
        displacement: 0.0,
        backgroundColor: Theme.of(context).appBarTheme.color,
        color: Theme.of(context).textTheme.bodyText2.color,
        onRefresh: () => _bloc.loadRequests(reset: true),
        child: res.models.isEmpty
            ? ListView(children: [_buildNoResults()])
            : ListView.separated(
                physics: AlwaysScrollableScrollPhysics(),
                controller: _scrollController,
                itemCount: res.models.length,
                itemBuilder: (BuildContext context, int index) {
                  final WorkspaceAccessRequest request = res.models[index];

                  return FadeInBottomTop(
                      (index * 1.5).toDouble(),
                      ListTileTheme(
                        textColor: Theme.of(context).textTheme.bodyText2.color,
                        child: ListTile(
                          leading: WorkspaceUserAvatar(request.user),
                          title: Text(
                            request.user.name,
                            style: TextStyle(fontWeight: FontWeight.w600),
                          ),
                          subtitle: Text(
                            "Requested ${request.requestedOnDisplay} ago",
                            style: TextStyle(
                                color: Theme.of(context).hintColor,
                                fontSize: 14.0),
                          ),
                          trailing: Container(
                            width: 100,
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.end,
                              mainAxisAlignment: MainAxisAlignment.end,
                              children: <Widget>[
                                Container(
                                  height: 40,
                                  width: 40,
                                  margin: EdgeInsets.only(right: 8.0),
                                  decoration: BoxDecoration(
                                      borderRadius: BorderRadius.circular(22),
                                      color: AppColors.errorRed),
                                  child: IconButton(
                                    iconSize: 20,
                                    color: AppColors.white,
                                    icon: Icon(AppIcons.times),
                                    onPressed: () => _handleDecline(request),
                                  ),
                                ),
                                Container(
                                  height: 40,
                                  width: 40,
                                  decoration: BoxDecoration(
                                      borderRadius: BorderRadius.circular(22),
                                      color: AppColors.successGreen),
                                  child: IconButton(
                                    iconSize: 20,
                                    color: AppColors.white,
                                    icon: Icon(AppIcons.check),
                                    onPressed: () => _handleAccept(request),
                                  ),
                                ),
                              ],
                            ),
                          ),
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
            Text("No Requests", style: Theme.of(context).textTheme.headline6),
            SizedBox(
              height: 8.0,
            ),
            Text(
              "There are no active requets to join this workspace",
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyText2.merge(
                    TextStyle(color: AppColors.grey300),
                  ),
            ),
          ],
        ),
      );
}
