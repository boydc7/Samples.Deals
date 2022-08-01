import 'package:flutter/material.dart';
import 'package:rydr_app/ui/profile/blocs/about.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/app/utils.dart';

class ProfileSettingsAboutPage extends StatefulWidget {
  @override
  _ProfileSettingsAboutPageState createState() =>
      _ProfileSettingsAboutPageState();
}

class _ProfileSettingsAboutPageState extends State<ProfileSettingsAboutPage> {
  final _bloc = AboutBloc();

  @override
  void initState() {
    super.initState();

    _bloc.getVersion();
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
        title: Text("About"),
      ),
      body: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: ListView(
              children: <Widget>[
                _settingsTile(
                  context: context,
                  icon: AppIcons.bookUser,
                  label: 'Terms of Use',
                  onTap: () => Utils.launchUrl(context, AppLinks.termsUrl,
                      trackingName: 'terms'),
                ),
                _settingsTile(
                  context: context,
                  icon: AppIcons.userSecret,
                  label: 'Privacy Policy',
                  onTap: () => Utils.launchUrl(context, AppLinks.privacyUrl,
                      trackingName: 'privacy'),
                ),
              ],
            ),
          ),
          SafeArea(
            bottom: true,
            child: Container(
              padding: EdgeInsets.all(16.0),
              child: Column(
                children: <Widget>[
                  StreamBuilder(
                      stream: _bloc.version,
                      builder: (context, snapshot) {
                        return snapshot.data == null || snapshot.data == ''
                            ? Text('')
                            : Text(
                                'RYDR v${snapshot.data}',
                                style: Theme.of(context)
                                    .textTheme
                                    .caption
                                    .merge(
                                      TextStyle(color: Colors.grey.shade400),
                                    ),
                              );
                      }),
                  Text(
                    'Made in Fort Lauderdale  🌴',
                    style: Theme.of(context).textTheme.caption.merge(
                          TextStyle(color: Colors.grey.shade400),
                        ),
                  ),
                ],
              ),
            ),
          )
        ],
      ),
    );
  }

  Widget _settingsTile({
    BuildContext context,
    IconData icon,
    String label,
    String trailing = '',
    Function onTap,
  }) {
    return Column(
      children: <Widget>[
        GestureDetector(
          onTap: onTap,
          child: Container(
            height: 56.0,
            padding: EdgeInsets.symmetric(horizontal: 16.0, vertical: 8.0),
            child: Row(
              children: <Widget>[
                Container(
                  alignment: Alignment.centerLeft,
                  width: 40.0,
                  child: Icon(
                    icon,
                    size: 20.0,
                    color: Theme.of(context).iconTheme.color,
                  ),
                ),
                Expanded(
                  child:
                      Text(label, style: Theme.of(context).textTheme.bodyText2),
                ),
                Visibility(
                  visible: onTap != null,
                  child: Container(
                    width: 20.0,
                    alignment: Alignment.centerRight,
                    child: Icon(
                      AppIcons.angleRight,
                      color: Theme.of(context).iconTheme.color,
                    ),
                  ),
                ),
                Visibility(
                  visible: onTap == null && trailing != '',
                  child: Text(trailing),
                )
              ],
            ),
          ),
        ),
      ],
    );
  }
}
