import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/workspace.dart';
import 'package:rydr_app/ui/profile/insights_media_analysis.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';

import 'blocs/account.dart';

class ProfileSettingsAccountPage extends StatefulWidget {
  @override
  _ProfileSettingsAccountPageState createState() =>
      _ProfileSettingsAccountPageState();
}

class _ProfileSettingsAccountPageState
    extends State<ProfileSettingsAccountPage> {
  final _bloc = AccountBloc();

  final Map<String, String> _pageContent = {
    "ai_basic_title": "Selfie Vision™",
    "ai_basic_subtitle": "Instagram Post & Story Analysis",
    "ai_basic_content":
        "\nThis is only available to Instagram professional profiles connected to RYDR through Facebook.",
    "view": "View",
    "cancel": "Cancel",
    "on": "On",
    "off": "Off",
    "turn_on": "Turn On",
    "turn_off": "Turn Off",
    "account": "Account",
    "instagram_username": "Instagram Username",
    "account_type": "Account Type",
    "business": "Business",
    "creator": "Creator",
    "unread_notifications": "Total Unread Notifications",
    "mark_read": "Tap to mark all notifications as read",
    "archived_completed": "Archived & Completed RYDRs",
    "deleted": "Deleted RYDRs",
    "cancelled": "Cancelled Requests",
    "declined": "Declined Requests",
    "delinquent": "Delinquent Requests",
    "media_analysis_title": "Selfie Vision™",
    "media_analysis_subtitle": "Instagram Post & Story Analysis",
    "media_analysis_legal":
        "By continuing, you are agreeing to allow any qualifying business to view these results for approving requests.",
    "id_title": "RYDR Identifiers",
    "id_subtitle": "Tap to copy identifiers",
    "id_master": "Token ID:",
    "id_workspace": "Container ID:",
    "id_profile": "Profile ID:",
  };

  @override
  void initState() {
    super.initState();
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  void _goToMediaAnalysis() => Navigator.of(context).push(
        MaterialPageRoute(
          builder: (BuildContext context) =>
              ProfileInsightsMediaAnalysis(appState.currentProfile),
          settings:
              AppAnalytics.instance.getRouteSettings('profile/insights/ai'),
        ),
      );

  void _showMediaAnalysisOptInOptions() =>
      appState.currentProfile.optInToAi == true
          ? showSharedModalAlert(
              context, Text(_pageContent["media_analysis_title"]),
              content: Text(_pageContent["media_analysis_subtitle"]),
              actions: [
                  ModalAlertAction(
                    isDefaultAction: true,
                    label: _pageContent["view"],
                    onPressed: () {
                      Navigator.of(context).pop();
                      _goToMediaAnalysis();
                    },
                  ),
                  ModalAlertAction(
                      isDestructiveAction: true,
                      label: _pageContent["turn_off"],
                      onPressed: () {
                        Navigator.of(context).pop();
                        _bloc.setOptInToAi(false);
                      }),
                  ModalAlertAction(
                      label: _pageContent["cancel"],
                      onPressed: () => Navigator.of(context).pop()),
                ])
          : _goToMediaAnalysis();

  void _showBasicAIAlert() =>
      showSharedModalAlert(context, Text(_pageContent["ai_basic_title"]),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(_pageContent["ai_basic_subtitle"]),
              Text(_pageContent["ai_basic_content"]),
            ],
          ),
          actions: [
            ModalAlertAction(
                label: _pageContent["cancel"],
                onPressed: () => Navigator.of(context).pop()),
          ]);

  void _unlink() async {
    Navigator.of(context).pop();

    showSharedLoadingLogo(
      context,
      content: "Removing Account",
    );

    /// make the call to unlink the user
    final bool success = await _bloc.unLink();

    /// close the "updating alert"
    Navigator.of(context).pop();

    if (success) {
      /// upon successfully unlinking an instagram user we'll want to update
      /// the container state which will let us know if we have any other users left
      /// and based on that either send the user to the profile of the newly active user
      /// or to the onboard page where they can choose to add pages
      final bool hasUsersLeft = await appState.removeCurrentProfile();

      AppAnalytics.instance.logScreen('profile/settings/account/unlinked');

      Navigator.of(context).pushNamedAndRemoveUntil(
          hasUsersLeft
              ? AppRouting.getProfileMeRoute
              : AppRouting.getConnectPages,
          (Route<dynamic> route) => false);
    } else {
      showSharedModalError(
        context,
        title: 'Account Removal Error',
        subTitle:
            'We are unable to complete this action. Please try again in a few moments.',
      );
    }
  }

  void _showBottomModal() => showSharedModalBottomActions(
        context,
        title: 'Account Options',
        actions: [
          _bloc.showIdentifiers.value == false
              ? ModalBottomAction(
                  child: Text("Show RYDR Identifiers"),
                  onTap: () {
                    Navigator.of(context).pop();
                    _bloc.setShowIdentifiers();
                  })
              : null,
          ModalBottomAction(
              child: Text(
                appState.currentProfile.isBusiness
                    ? "Switch to Creator Account"
                    : "Switch to Business Account",
              ),
              onTap: _switchAccount),
          ModalBottomAction(
            child: Text(
              "Remove Account",
              style: TextStyle(color: AppColors.errorRed),
            ),
            onTap: _unlinkUser,
          ),
        ].where((element) => element != null).toList(),
      );

  void _unlinkUser() async =>
      showSharedModalAlert(context, Text("Are you sure?"),
          content: Text("You will be able to relink your account at any time."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
              label: "Cancel",
              onPressed: () => Navigator.of(context).pop(),
            ),
            ModalAlertAction(
              label: "Remove",
              isDestructiveAction: true,
              onPressed: () => _unlink(),
            ),
          ]);

  void _switchAccount() =>
      showSharedModalAlert(context, Text("Convert Account"),
          content: appState.currentProfile.isBusiness
              ? Text(
                  "All of your active RYDRs in the Marketplace and RYDR requests will be cancelled.")
              : Text(
                  "All of your in-progress and pending RYDRs will be cancelled."),
          actions: <ModalAlertAction>[
            ModalAlertAction(
              label: "Cancel",
              onPressed: () => Navigator.of(context).pop(),
            ),
            ModalAlertAction(
              label: "Convert",
              onPressed: () => _switchAccountType(context),
            ),
          ]);

  void _switchAccountType(BuildContext context) async {
    final RydrAccountType toAccountType = appState.currentProfile.isBusiness
        ? RydrAccountType.influencer
        : RydrAccountType.business;

    Navigator.of(context).pop();

    showSharedLoadingLogo(
      context,
      content: "Updating Account",
    );

    /// make the call to switch account types
    final bool success = await _bloc.switchAccount(toAccountType);

    /// close the loading overlay
    Navigator.of(context).pop();

    if (success) {
      AppAnalytics.instance.logScreen('profile/settings/account/typeswitched');

      /// update the account type for the current user in appState
      appState.currentProfile.rydrPublisherType = toAccountType;

      Navigator.of(context).pushNamedAndRemoveUntil(
          appState.getInitialRoute(), (Route<dynamic> route) => false);
    } else {
      showSharedModalError(
        context,
        title: 'Unable to switch account',
        subTitle:
            'We were unable to switch your account. Please try again in a few moments...',
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final TextStyle title = Theme.of(context).textTheme.bodyText2;

    return Scaffold(
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text(_pageContent["account"]),
        actions: <Widget>[
          /// only available when in a personal workspace
          appState.currentWorkspace.type == WorkspaceType.Personal
              ? IconButton(
                  icon: Icon(AppIcons.ellipsisV),
                  onPressed: _showBottomModal,
                )
              : Container(width: kMinInteractiveDimension)
        ],
      ),
      body: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: ListView(
              children: <Widget>[
                ListTile(
                  title: Text(_pageContent["instagram_username"], style: title),
                  trailing:
                      Text(appState.currentProfile.userName, style: title),
                ),
                ListTile(
                  title: Text(_pageContent["account_type"], style: title),
                  trailing: Text(
                      appState.currentProfile.isBusiness
                          ? _pageContent["business"]
                          : _pageContent["creator"],
                      style: title),
                ),
                StreamBuilder(
                  stream: _bloc.showIdentifiers,
                  builder: (context, snapshot) => snapshot.data == true
                      ? Column(
                          children: <Widget>[
                            Divider(),
                            ListTile(
                                title: Text(_pageContent['id_title'],
                                    style: title),
                                subtitle: Text(_pageContent['id_subtitle'],
                                    style:
                                        Theme.of(context).textTheme.caption)),
                            ListTile(
                              onTap: () {
                                Clipboard.setData(ClipboardData(
                                    text: appState.masterUser.accountId));

                                Scaffold.of(context).showSnackBar(
                                    SnackBar(content: Text("Copied!")));
                              },
                              title:
                                  Text(_pageContent["id_master"], style: title),
                              trailing: Text(appState.masterUser.accountId,
                                  style: title),
                            ),
                            ListTile(
                              onTap: () {
                                Clipboard.setData(ClipboardData(
                                    text: appState.currentWorkspace.id
                                        .toString()));

                                Scaffold.of(context).showSnackBar(
                                    SnackBar(content: Text("Copied!")));
                              },
                              title: Text(_pageContent["id_workspace"],
                                  style: title),
                              trailing: Text(
                                  appState.currentWorkspace.id.toString(),
                                  style: title),
                            ),
                            ListTile(
                              onTap: () {
                                Clipboard.setData(ClipboardData(
                                    text:
                                        appState.currentProfile.id.toString()));

                                Scaffold.of(context).showSnackBar(
                                    SnackBar(content: Text("Copied!")));
                              },
                              title: Text(_pageContent["id_profile"],
                                  style: title),
                              trailing: Text(
                                  appState.currentProfile.id.toString(),
                                  style: title),
                            ),
                          ],
                        )
                      : Container(),
                ),
                StreamBuilder(
                  stream: _bloc.unreadNotifications,
                  builder: (context, snapshot) {
                    final bool hasNotifications =
                        snapshot.data != null && snapshot.data > 0;

                    return hasNotifications
                        ? ListTile(
                            title: Text(_pageContent["unread_notifications"],
                                style: title),
                            subtitle:
                                appState.currentProfile.unreadNotifications > 0
                                    ? Text(_pageContent["mark_read"],
                                        style: Theme.of(context)
                                            .textTheme
                                            .caption
                                            .merge(TextStyle(
                                                color: Theme.of(context)
                                                    .hintColor)))
                                    : Container(),
                            trailing: Text(
                              appState.currentProfile.unreadNotifications
                                  .toString(),
                              style: title,
                            ),
                            onTap: _bloc.markNotificationsAsRead,
                          )
                        : Container();
                  },
                ),
                Visibility(
                  visible: _bloc.showAiSettings,
                  child: Divider(),
                ),
                Visibility(
                  visible: _bloc.showAiSettings,
                  child: ListTile(
                    onTap: appState.currentProfile.isAccountFull
                        ? _showMediaAnalysisOptInOptions
                        : _showBasicAIAlert,
                    title: Text(_pageContent["media_analysis_title"],
                        style: title),
                    subtitle: Text(
                      _pageContent["media_analysis_subtitle"],
                      style: Theme.of(context).textTheme.caption,
                    ),
                    trailing: Container(
                      width: 120,
                      alignment: Alignment.centerRight,
                      child: StreamBuilder<bool>(
                        stream: appState.currentProfileOptInToAi,
                        builder: (context, snapshot) => ToggleButton(
                          value: snapshot.data == true,
                          onChanged: appState.currentProfile.isAccountFull
                              ? (bool) => _showMediaAnalysisOptInOptions()
                              : null,
                        ),
                      ),
                    ),
                  ),
                ),
                Divider(),
                Visibility(
                  visible: appState.currentProfile.isBusiness,
                  child: ListTile(
                    title:
                        Text(_pageContent["archived_completed"], style: title),
                    trailing: Icon(AppIcons.angleRight),
                    onTap: () => Navigator.of(context)
                        .pushNamed(AppRouting.getDealsArchived),
                  ),
                ),
                Visibility(
                  visible: appState.currentProfile.isBusiness,
                  child: ListTile(
                    title: Text(_pageContent["deleted"], style: title),
                    trailing: Icon(AppIcons.angleRight),
                    onTap: () => Navigator.of(context)
                        .pushNamed(AppRouting.getDealsDeleted),
                  ),
                ),
                ListTile(
                  title: Text(_pageContent["cancelled"], style: title),
                  trailing: Icon(AppIcons.angleRight),
                  onTap: () => Navigator.of(context)
                      .pushNamed(AppRouting.getRequestsCancelled),
                ),
                ListTile(
                  title: Text(_pageContent["declined"], style: title),
                  trailing: Icon(AppIcons.angleRight),
                  onTap: () => Navigator.of(context)
                      .pushNamed(AppRouting.getRequestsDenied),
                ),
                ListTile(
                  title: Text(_pageContent["delinquent"], style: title),
                  trailing: Icon(AppIcons.angleRight),
                  onTap: () => Navigator.of(context)
                      .pushNamed(AppRouting.getRequestsDelinquent),
                ),
              ],
            ),
          ),
          SafeArea(
            bottom: true,
            child: Container(
              padding: EdgeInsets.all(16.0),
              child: Center(
                child: Text(
                  'Joined RYDR on ' +
                      Utils.formatDateFull(appState.currentProfile.createdOn) +
                      '  🎉',
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(color: Colors.grey.shade400),
                      ),
                ),
              ),
            ),
          )
        ],
      ),
    );
  }
}
