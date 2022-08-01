import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/notification_settings.dart';
import 'package:rydr_app/models/responses/notifications.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/notifications_check.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

import 'blocs/notifications.dart';

class ProfileSettingsNotificationsPage extends StatefulWidget {
  @override
  _ProfileSettingsNotificationsPageState createState() =>
      _ProfileSettingsNotificationsPageState();
}

class _ProfileSettingsNotificationsPageState
    extends State<ProfileSettingsNotificationsPage> {
  NotificationsBloc _bloc = NotificationsBloc();

  @override
  void initState() {
    super.initState();

    _bloc.loadData();
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Text("Push Notifications"),
        ),
        body: StreamBuilder<NotificationSubscriptionResponse>(
          stream: _bloc.notificationSubscriptionResponse,
          builder: (context, snapshot) {
            return snapshot.connectionState == ConnectionState.waiting
                ? _buildLoadingBody()
                : snapshot.data.error != null
                    ? _buildErrorBody(snapshot.data)
                    : _buildSuccessBody(snapshot.data);
          },
        ),
      );

  Widget _buildLoadingBody() => ListView(
        children: [LoadingListShimmer(reversed: true)],
        padding: EdgeInsets.all(16),
      );

  Widget _buildErrorBody(NotificationSubscriptionResponse response) => Padding(
        child: RetryError(
          error: response.error,
          onRetry: _bloc.loadData,
        ),
        padding: EdgeInsets.all(16),
      );

  Widget _buildSuccessBody(NotificationSubscriptionResponse response) => Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: appState.currentProfile.isBusiness
                ? buildBusinessTopics(response.models)
                : buildInfluencerTopics(response.models),
          ),
        ],
      );

  Widget _buildTopic(
    String title,
    String subTitle,
    AppNotificationType topic, {
    Color toggleColor,
  }) =>
      ListTile(
        title: Text(
          title,
          style: TextStyle(fontWeight: FontWeight.w500),
        ),
        subtitle: Text(subTitle,
            style: Theme.of(context).textTheme.caption.merge(
                TextStyle(fontSize: 11, color: Theme.of(context).hintColor))),
        trailing: Container(
          width: 100,
          alignment: Alignment.centerRight,
          child: StreamBuilder<AppNotificationType>(
              stream: _bloc.updatingType,
              builder: (context, snapshot) => ToggleButton(
                  color: toggleColor,
                  value: _bloc.getSetting(topic),
                  onChanged: (value) => _bloc.saveSettings(value, topic))),
        ),
      );

  Widget _buildCategory(String label, [bool divider = true]) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          divider
              ? Column(
                  children: <Widget>[
                    SizedBox(
                      height: 4.0,
                    ),
                    Divider(
                      height: 1,
                    )
                  ],
                )
              : Container(),
          Padding(
            padding: EdgeInsets.only(
              top: 16.0,
              left: 16.0,
              bottom: 8.0,
            ),
            child: Text(
              label,
              style: Theme.of(context).textTheme.bodyText1,
            ),
          ),
        ],
      );

  Widget buildInfluencerTopics(List<NotificationSetting> settings) => ListView(
        children: <Widget>[
          NotificationsCheck(
            canDismiss: false,
          ),
          _buildTopic("RYDR Match", "Here's a RYDR we think you'll love!",
              AppNotificationType.dealMatched),
          _buildCategory("RYDR Activity"),
          _buildTopic(
            "Invited",
            "gravitymedia sent you an invite",
            AppNotificationType.dealInvited,
            toggleColor: Theme.of(context).accentColor,
          ),
          _buildTopic("Approved", "gravitymedia accepted your request",
              AppNotificationType.dealRequestApproved),
          _buildTopic("Declined", "gravitymedia declined your request",
              AppNotificationType.dealRequestDenied),
          _buildTopic("Cancelled", "gravitymedia cancelled your RYDR",
              AppNotificationType.dealRequestCancelled),
          _buildTopic("Delinquent", "gravitymedia marked a RYDR delinquent",
              AppNotificationType.dealRequestDelinquent),
          _buildCategory("Posts and Stories"),
          _buildTopic("Detection", "Post detected. Tap to complete.",
              AppNotificationType.dealCompletionMediaDetected),
          _buildCategory("Direct Messages"),
          _buildTopic("Message", "gravitymedia sent you a message",
              AppNotificationType.message),
        ],
      );

  Widget buildBusinessTopics(List<NotificationSetting> settings) => ListView(
        children: <Widget>[
          NotificationsCheck(
            canDismiss: false,
          ),
          _buildCategory("RYDR Activity", false),

          /// invite-accept notifications are only applicable to when this profile
          /// has a subscription other than the default free "none" type
          Visibility(
            visible: appState.isBusinessPro,
            child: _buildTopic(
              "Accepted",
              "handstandman accepted your invite",
              AppNotificationType.dealRequestApproved,
              toggleColor: Theme.of(context).accentColor,
            ),
          ),
          _buildTopic("Requested", "handstandman requested a RYDR",
              AppNotificationType.dealRequested),
          _buildTopic("Completed", "handstandman completed a RYDR",
              AppNotificationType.dealRequestCompleted),
          _buildTopic("Cancelled", "handstandman cancelled a RYDR",
              AppNotificationType.dealRequestCancelled),
          _buildCategory("Direct Messages"),
          _buildTopic("Message", "handstandman sent you a message",
              AppNotificationType.message),
        ],
      );
}
