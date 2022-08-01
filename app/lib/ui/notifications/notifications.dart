import 'dart:async';
import 'package:flutter/material.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/notifications.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/enums/workspace.dart';

import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/models/responses/notifications.dart';
import 'package:rydr_app/models/workspace.dart';

import 'package:rydr_app/services/notifications.dart';
import 'package:rydr_app/ui/notifications/blocs/notifications.dart';
import 'package:rydr_app/ui/notifications/widgets/inprogress_tile.dart';
import 'package:rydr_app/ui/notifications/widgets/invitations_tile.dart';
import 'package:rydr_app/ui/notifications/widgets/pending_tile.dart';
import 'package:rydr_app/ui/notifications/widgets/redeemed_tile.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/list_helper.dart';
import 'package:rydr_app/ui/shared/widgets/notifications_check.dart';

import 'widgets/no_results.dart';

class ListNotifications extends StatefulWidget {
  ListNotifications();

  @override
  _ListNotificationsState createState() => _ListNotificationsState();
}

class _ListNotificationsState extends State<ListNotifications>
    with AutomaticKeepAliveClientMixin {
  final _bloc = NotificationsBloc();
  final _scrollController = ScrollController();

  /// for now, if you are in a 'team' workspace, then we'll set it
  /// to show notifications from all linked accounts for the given workspace...
  final bool _showForAll = appState.currentWorkspace.type == WorkspaceType.Team;

  ThemeData _theme;
  bool _isBusiness;
  ListHelper listHelper = ListHelper();
  StreamSubscription _subUnreadNotifications;

  @override
  bool get wantKeepAlive => true;

  @override
  void initState() {
    super.initState();

    _bloc.loadList(_showForAll);

    _scrollController.addListener(_onScroll);

    _subUnreadNotifications =
        appState.currentProfileUnreadNotifications.listen((val) {
      /// set a delay for 2 seconds, then mark all as read
      /// if we have unread notifications for the given profile
      if (val != null && val > 0) {
        Future.delayed(const Duration(seconds: 2), () {
          /// clear all notifications in UI for the current profile
          appState.clearNotificationsCount();

          /// mark all notifications as read on the server
          NotificationService.markAsRead();
        });
      }
    });
  }

  @override
  void dispose() {
    _subUnreadNotifications?.cancel();

    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  void _handleTap(AppNotification notification) {
    /// only mark read if still unread
    if (!notification.isRead) {
      /// mark the notification as read in memory
      notification.isRead = true;
    }

    /// process the tap
    appNotifications.processLocalListTap(notification);
  }

  void _onScroll() {
    if (_scrollController.offset >=
            _scrollController.position.maxScrollExtent &&
        _bloc.hasMore &&
        !_bloc.isLoading) {
      _bloc.loadList(_showForAll);
    }
  }

  Future<void> _refresh() async {
    /// refresh app state content stats for the current profile
    appState.loadProfileStats();

    /// refrsh the list
    _bloc.loadList(_showForAll, true);
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);
    _isBusiness = appState.currentProfile.isBusiness;

    return StreamBuilder<NotificationsResponse>(
      stream: _bloc.notificationsResponse,
      builder: (context, snapshot) {
        return snapshot.connectionState == ConnectionState.waiting
            ? _buildLoading()
            : snapshot.data != null && snapshot.data.error == null
                ? snapshot.data.models.isNotEmpty
                    ? _buildList(snapshot.data)
                    : _buildNoResults()
                : _buildError(snapshot.data);
      },
    );
  }

  Widget _buildLoading() =>
      ListView(padding: EdgeInsets.all(16), children: [LoadingListShimmer()]);

  Widget _buildError(NotificationsResponse res) => Column(children: [
        Expanded(
            child: Padding(
                padding: EdgeInsets.all(16),
                child: RetryError(
                  error: res.error,
                  onRetry: () => _bloc.loadList(_showForAll, true),
                )))
      ]);

  Widget _buildNoResults() => Column(children: [
        Expanded(
            child: RefreshIndicator(
                displacement: 0.0,
                backgroundColor: _theme.appBarTheme.color,
                color: _theme.textTheme.bodyText2.color,
                onRefresh: _refresh,
                child: ListView(children: [NotificationsNoResults()])))
      ]);

  Widget _buildList(NotificationsResponse res) => Column(children: [
        /// add the notifications check only if we have notifications already
        res.models.isNotEmpty ? NotificationsCheck() : Container(),
        Expanded(
            child: RefreshIndicator(
          displacement: 0.0,
          backgroundColor: _theme.appBarTheme.color,
          color: _theme.textTheme.bodyText2.color,
          onRefresh: _refresh,
          child: ListView.builder(
              physics: AlwaysScrollableScrollPhysics(),
              controller: _scrollController,
              itemCount: res.models.length,
              itemBuilder: (BuildContext context, int index) {
                final AppNotification notification = res.models[index];

                /// get the last notification from state
                final AppNotification lastNotification =
                    index > 0 ? res.models[index - 1] : null;

                /// prefix the message with username of what profile it is intended for
                /// include indicator of its for a profile in a team workspace
                final bool isToTeam = notification.workspaceId > 0 &&
                    appState.workspaces != null &&
                    appState.workspaces.isNotEmpty &&
                    appState.workspaces
                            .where((Workspace ws) =>
                                ws.id == notification.workspaceId &&
                                ws.type == WorkspaceType.Team)
                            .length >
                        0;

                /// format this based on paid vs. non-paid
                final String fromName =
                    notification.fromPublisherAccount != null
                        ? isToTeam
                            ? '${notification.fromPublisherAccount.userName}*'
                            : '${notification.fromPublisherAccount.userName}'
                        : 'RYDR';
                final String toName = notification.toPublisherAccount != null
                    ? isToTeam
                        ? '${notification.toPublisherAccount.userName}*'
                        : '${notification.toPublisherAccount.userName}'
                    : 'RYDR';

                final String recordName = notification.forRecordName ?? "";

                final String subTitle = notification.type == AppNotificationType.message
                    ? " · ${notification.body ?? 'sent a message'}"
                    : notification.type == AppNotificationType.dealMatched
                        ? 'We matched you with a RYDR · "$recordName"'
                        : notification.type == AppNotificationType.dealRequested
                            ? ' requested RYDR "$recordName"'
                            : notification.type == AppNotificationType.dealInvited
                                ? ' invited you · $recordName'
                                : notification.type ==
                                        AppNotificationType.dealRequestApproved
                                    ? ' ${_isBusiness ? "accepted your RYDR invite" : "approved your request for"} "$recordName"'
                                    : notification.type == AppNotificationType.dealRequestDenied
                                        ? ' ${_isBusiness ? "declined your RYDR invite" : "declined your request for"} "$recordName"'
                                        : notification.type == AppNotificationType.dealRequestCancelled
                                            ? ' cancelled "$recordName"'
                                            : notification.type == AppNotificationType.dealRequestCompleted
                                                ? ' completed "$recordName"'
                                                : notification.type ==
                                                        AppNotificationType
                                                            .dealRequestDelinquent
                                                    ? ' marked "$recordName" as delinquent'
                                                    : notification.type ==
                                                            AppNotificationType
                                                                .dealRequestRedeemed
                                                        ? ' redeemed "$recordName"'
                                                        : notification.type == AppNotificationType.dealCompletionMediaDetected
                                                            ? 'Post Detected · ${notification.route != null ? "Tap to complete RYDR for $recordName" : "One of your RYDRs can likely be completed..."}'
                                                            : notification.type == AppNotificationType.accountAttention
                                                                ? 'Account Issue: ${notification.body}'
                                                                : notification.body ?? "";

                final bool noTitle = notification.type ==
                        AppNotificationType.dealMatched ||
                    notification.type ==
                        AppNotificationType.dealCompletionMediaDetected ||
                    notification.type == AppNotificationType.accountAttention;

                return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      /// if this is the first tile, then potentially inject
                      /// pending, invited, and in progress reminder tiles
                      index == 0 ? RedeemedTile() : Container(),
                      index == 0 ? InvitationsTile() : Container(),
                      index == 0 ? InProgressTile() : Container(),
                      index == 0 ? PendingTile() : Container(),
                      index == 0 && 1 == 1 ? Divider(height: 1) : Container(),

                      listHelper.addDateHeader(
                        context,
                        lastNotification != null
                            ? lastNotification.occurredOn
                            : null,
                        notification.occurredOn,
                        index,
                      ),
                      ListTileTheme(
                        textColor: _theme.textTheme.bodyText2.color,
                        child: ListTile(
                          contentPadding: EdgeInsets.symmetric(
                              vertical: 8.0, horizontal: 16.0),
                          onTap: notification.route != null
                              ? () => _handleTap(notification)
                              : null,
                          leading: UserAvatar(
                            notification.fromPublisherAccount,
                            isPostDetected: notification.type ==
                                AppNotificationType.dealCompletionMediaDetected,
                          ),
                          selected: !notification.isRead,
                          title: RichText(
                            text: TextSpan(
                                style: _theme.textTheme.bodyText2,
                                children: <TextSpan>[
                                  TextSpan(
                                      text: noTitle
                                          ? ""
                                          : _showForAll
                                              ? "[$toName] $fromName"
                                              : "$fromName",
                                      style: TextStyle(
                                          fontWeight: FontWeight.w600)),
                                  TextSpan(text: subTitle),
                                  TextSpan(
                                      text:
                                          ' ${notification.occurredOnDisplay}',
                                      style:
                                          TextStyle(color: AppColors.grey300))
                                ]),
                          ),
                        ),
                      ),
                    ]);
              }),
        ))
      ]);
}
