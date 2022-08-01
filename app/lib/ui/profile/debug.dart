import 'dart:io' show Platform;
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/ui/profile/blocs/debug.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class ProfileSettingsDebugPage extends StatefulWidget {
  @override
  _ProfileSettingsDebugPageState createState() =>
      _ProfileSettingsDebugPageState();
}

class _ProfileSettingsDebugPageState extends State<ProfileSettingsDebugPage> {
  final _bloc = ProfileDebugBloc();

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
        title: Text("Debug Settings"),
      ),
      body: ListView(children: <Widget>[
        ListTile(
          title: Text("Cache"),
          subtitle: Text("Tap to clear all cached responses"),
          trailing: IconButton(
            icon: Icon(AppIcons.trash),
            onPressed: () => AppApi.instance.clearCache(),
          ),
        ),
        Divider(height: 1),
        Platform.isIOS
            ? Column(
                children: [
                  ListTile(
                    title: Text("iOS Push Notification Settings"),
                    subtitle: StreamBuilder<String>(
                      stream: _bloc.iOSPushNotificationSettings,
                      builder: (context, snapshot) {
                        return Text(snapshot.data == null
                            ? "Loading..."
                            : snapshot.data);
                      },
                    ),
                    trailing: IconButton(
                      icon: Icon(AppIcons.trash),
                      onPressed: _bloc.clearIOsSettings,
                    ),
                  ),
                  Divider(height: 1),
                ],
              )
            : Container(height: 0),
        ListTile(
          title: Text("Onboard Settings"),
          subtitle: StreamBuilder<String>(
            stream: _bloc.onboardSettings,
            builder: (context, snapshot) {
              return Text(snapshot.data == null ? "Loading..." : snapshot.data);
            },
          ),
          trailing: IconButton(
            icon: Icon(AppIcons.trash),
            onPressed: _bloc.clearOnboardingSettings,
          ),
        ),
        Divider(height: 1),
        ListTile(
          title: Text("Device Info"),
          subtitle: StreamBuilder<String>(
            stream: _bloc.deviceInfo,
            builder: (context, snapshot) {
              return Text(snapshot.data == null ? "Loading..." : snapshot.data);
            },
          ),
          trailing: IconButton(
            icon: Icon(AppIcons.trash),
            onPressed: _bloc.clearDeviceInfo,
          ),
        ),
        Divider(height: 1),
        ListTile(
          title: Text("Usage Info"),
          subtitle: StreamBuilder<String>(
            stream: _bloc.usageInfo,
            builder: (context, snapshot) {
              return Text(snapshot.data == null ? "Loading..." : snapshot.data);
            },
          ),
          trailing: IconButton(
            icon: Icon(AppIcons.trash),
            onPressed: _bloc.clearUsageInfo,
          ),
        ),
      ]),
    );
  }
}
