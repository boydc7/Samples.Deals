import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/workspace.dart';

import 'package:rydr_app/models/notification.dart';
import 'package:rydr_app/models/workspace.dart';

import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class AppNotificationOverlay extends StatefulWidget {
  final AppNotification message;
  final bool showError;
  final Function onTap;
  final Function onDismiss;

  AppNotificationOverlay({
    @required this.message,
    @required this.showError,
    @required this.onTap,
    @required this.onDismiss,
  });

  @override
  _AppNotificationOverlayState createState() => _AppNotificationOverlayState();
}

class _AppNotificationOverlayState extends State<AppNotificationOverlay>
    with SingleTickerProviderStateMixin {
  AnimationController animationController;
  Animation<double> animation;

  @override
  void initState() {
    HapticFeedback.mediumImpact();
    animationController = new AnimationController(
        duration: const Duration(milliseconds: 500), vsync: this);
    animation = new Tween(begin: 0.0, end: 40.0).animate(new CurvedAnimation(
        parent: animationController, curve: new Interval(0.0, 0.5)))
      ..addListener(() {
        if (mounted) {
          setState(() {
            // Refresh
          });
        }
      });
    animationController.forward().orCancel;

    super.initState();
  }

  @override
  void dispose() {
    animationController.dispose();

    super.dispose();
  }

  Widget _buildMessageBody(AppNotification message) {
    /// prefix the message with username of what profile it is intended for
    /// include indicator of its for a profile in a team workspace
    final bool isToTeam = message.workspaceId > 0 &&
        appState.workspaces != null &&
        appState.workspaces.isNotEmpty &&
        appState.workspaces
                .where((Workspace ws) =>
                    ws.id == message.workspaceId &&
                    ws.type == WorkspaceType.Team)
                .length >
            0;

    final String toName = message.toPublisherAccount != null
        ? isToTeam
            ? '[${message.toPublisherAccount.userName}*]'
            : '[${message.toPublisherAccount.userName}]'
        : '[RYDR]';

    return ListTile(
      leading: message.fromPublisherAccount == null
          ? null
          : UserAvatar(message.fromPublisherAccount),
      title: Text('$toName ${message.title}',
          style: Theme.of(context).textTheme.bodyText1),
      subtitle: Text(
        message.body,
        overflow: TextOverflow.ellipsis,
      ),
      onTap: widget.onTap,
    );
  }

  Widget _buildErrorBody(AppNotification message) {
    return ListTile(
      title: Text("Target user not on device"),
      subtitle: Text(
        "This notification was intended for a user that is no longer connected on this device",
        overflow: TextOverflow.ellipsis,
      ),
      onTap: widget.onTap,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Positioned(
        top: animation.value,
        width: MediaQuery.of(context).size.width,
        child: Dismissible(
          key: Key(widget.message.hashCode.toString()),
          direction: DismissDirection.up,
          onDismissed: (_) => widget.onDismiss(),
          child: Card(
              margin: EdgeInsets.only(
                left: 16,
                right: 16,
              ),
              child: Container(
                child: widget.showError
                    ? _buildErrorBody(widget.message)
                    : _buildMessageBody(widget.message),
              )),
        ));
  }
}
