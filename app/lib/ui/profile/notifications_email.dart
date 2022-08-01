import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/notification.dart';
import 'package:rydr_app/models/responses/notifications.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

import 'blocs/notifications.dart';

class ProfileSettingsNotificationsEmailPage extends StatefulWidget {
  @override
  _ProfileSettingsNotificationsEmailPageState createState() =>
      _ProfileSettingsNotificationsEmailPageState();
}

class _ProfileSettingsNotificationsEmailPageState
    extends State<ProfileSettingsNotificationsEmailPage> {
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
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text("Email Notifications"),
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
  }

  Widget _buildLoadingBody() {
    return Padding(
      child: LoadingListShimmer(reversed: true),
      padding: EdgeInsets.all(16),
    );
  }

  Widget _buildErrorBody(NotificationSubscriptionResponse response) {
    return Padding(
      child: RetryError(
        error: response.error,
        onRetry: _bloc.loadData,
      ),
      padding: EdgeInsets.all(16),
    );
  }

  Widget _buildSuccessBody(NotificationSubscriptionResponse response) {
    final String email = appState.currentProfile.email != null
        ? appState.currentProfile.email
        : "no email";
    return SafeArea(
      bottom: true,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: ListView(
              children: <Widget>[
                Visibility(
                  visible: appState.currentProfile.isCreator,
                  child: Column(
                    children: <Widget>[
                      _buildCategory("RYDR Activity", false),
                      _buildTopic(
                        "Invited",
                        "Get updated when you receive a personal invite.",
                        AppNotificationType.emailInvitations,
                        invite: true,
                      ),
                    ],
                  ),
                ),
                _buildCategory("From RYDR",
                    appState.currentProfile.isCreator ? true : false),
                _buildTopic(
                  "Reminders",
                  "Get notifications you may have missed.",
                  AppNotificationType.emailReminders,
                ),
                _buildTopic(
                  "Product Announcements",
                  "Get tips about RYDR's tools.",
                  AppNotificationType.emailProductAnnouncements,
                ),
                _buildTopic(
                  "Feedback",
                  "Give feedback on RYDR.",
                  AppNotificationType.emailFeedback,
                ),
                // _buildTopic(
                //   "Monthly Summary",
                //   "Get an aggregated summary of your accounts.",
                //   AppNotificationType.emailMonthlySummary,
                // ),
              ],
            ),
          ),
          InkWell(
            onTap: () {},
            child: Container(
              padding: EdgeInsets.symmetric(vertical: 8.0),
              width: double.infinity,
              color: Theme.of(context).scaffoldBackgroundColor,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.center,
                children: <Widget>[
                  Text("You'll receive emails at $email"),
                  Text(
                    "Tap to change",
                    style: Theme.of(context)
                        .textTheme
                        .caption
                        .merge(TextStyle(color: Theme.of(context).hintColor)),
                  )
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTopic(String title, String subTitle, AppNotificationType topic,
      {bool invite = false}) {
    return ListTile(
      title: Text(title),
      subtitle: Text(subTitle,
          style: Theme.of(context).textTheme.caption.merge(
              TextStyle(fontSize: 11, color: Theme.of(context).hintColor))),
      trailing: Container(
        width: 80,
        alignment: Alignment.centerRight,
        child: StreamBuilder<AppNotificationType>(
          stream: _bloc.updatingType,
          builder: (context, snapshot) {
            return ToggleButton(
                color: Theme.of(context).accentColor,
                value: _bloc.getSetting(topic),
                onChanged: (value) => _bloc.saveSettings(value, topic));
          },
        ),
      ),
    );
  }

  Widget _buildCategory(String label, bool divider) {
    return Column(
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
  }
}
