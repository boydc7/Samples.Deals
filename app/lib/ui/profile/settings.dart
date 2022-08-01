import 'package:flutter/material.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/ui/profile/blocs/settings.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';

class ProfileSettingsPage extends StatefulWidget {
  @override
  _ProfileSettingsPageState createState() => _ProfileSettingsPageState();
}

class _ProfileSettingsPageState extends State<ProfileSettingsPage> {
  final _bloc = SettingsBloc();

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Text("Profile Options"),
        ),
        body: SafeArea(
          bottom: true,
          child: ListTileTheme(
            textColor: AppColors.grey800,
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Expanded(
                  child: ListView(
                    children: <Widget>[
                      Visibility(
                        visible: appState.currentProfile.isBusiness,
                        child: _settingsTile(
                            context: context,
                            icon: AppIcons.mapMarkerAlt,
                            label: 'Locations',
                            onTap: () => Navigator.of(context)
                                .pushNamed(AppRouting.getProfilePlacesRoute)),
                      ),
                      StreamBuilder(
                        stream: _bloc.showNotice,
                        builder: (context, snapshot) {
                          bool showNotice =
                              snapshot.data != null && snapshot.data == true;

                          return _settingsTile(
                              context: context,
                              icon: showNotice
                                  ? AppIcons.bellSlash
                                  : AppIcons.bell,
                              label: 'Push Notifications',
                              subTitle: showNotice
                                  ? "Disabled. Re-enable in your Settings"
                                  : null,
                              alert: showNotice,
                              onTap: () => Navigator.of(context).pushNamed(
                                  AppRouting.getProfileNotificationsRoute));
                        },
                      ),
                      /*
                    _settingsTile(
                        context: context,
                        icon: AppIcons.bells,
                        label: 'Email Notifications',
                        onTap: () => Navigator.of(context).pushNamed(
                            AppRouting.getProfileEmailNotificationsRoute)),
                    */
                      _settingsTile(
                          context: context,
                          icon: AppIcons.userCircle,
                          label: 'Account',
                          onTap: () => Navigator.of(context)
                              .pushNamed(AppRouting.getProfileAccountRoute)),
                      Divider(),
                      _settingsTile(
                        context: context,
                        icon: AppIcons.lifeRing,
                        label: 'Help Center',
                        onTap: () => Utils.launchUrl(
                          context,
                          AppLinks.supportUrl,
                          trackingName: 'support',
                        ),
                      ),
                      _settingsTile(
                          context: context,
                          icon: AppIcons.infoCircle,
                          label: 'About',
                          onTap: () => Navigator.of(context)
                              .pushNamed(AppRouting.getProfileAboutRoute)),
                      AppConfig.debugEnabled()
                          ? _settingsTile(
                              context: context,
                              icon: AppIcons.bug,
                              label: 'Debug Settings',
                              onTap: () => Navigator.of(context)
                                  .pushNamed(AppRouting.getProfileDebugRoute))
                          : Container(),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      );

  Widget _settingsTile({
    BuildContext context,
    bool alert = false,
    IconData icon,
    String label,
    String subTitle,
    Function onTap,
  }) =>
      ListTile(
        onTap: onTap,
        title: Text(
          label,
          style: Theme.of(context).textTheme.bodyText1.merge(
                TextStyle(
                  fontWeight: alert ? FontWeight.w600 : FontWeight.w400,
                  color: alert
                      ? AppColors.errorRed
                      : Theme.of(context).textTheme.bodyText2.color,
                ),
              ),
        ),
        subtitle: subTitle != null
            ? Text(
                subTitle,
                style: TextStyle(color: AppColors.errorRed),
              )
            : null,
        leading: Icon(
          icon,
          color: alert ? AppColors.errorRed : Theme.of(context).iconTheme.color,
        ),
        trailing: Icon(AppIcons.angleRight, color: Theme.of(context).hintColor),
      );
}
