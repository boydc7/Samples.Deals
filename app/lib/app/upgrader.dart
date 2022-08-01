/*
 * Copyright (c) 2018 Larry Aasen. All rights reserved.
 */

import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:package_info/package_info.dart';
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:version/version.dart';

/// Signature of callbacks that have no arguments and return bool.
typedef BoolCallback = bool Function();

/// A singleton class to configure the upgrade dialog.
class Upgrader {
  final log = getLogger('Upgrader');
  static Upgrader _singleton = Upgrader._internal();

  /// Days until alerting user again
  final int daysUntilAlertAgain = 3;

  /// For debugging, always force the upgrade to be available.
  /// or just one time then honor the users selection, and log messages

  /// for when wanting to test locally
  bool debugDisplayAlways = false;
  bool debugDisplayOnce = false;

  /// Called when the ignore button is tapped or otherwise activated.
  /// Return false when the default behavior should not execute.
  BoolCallback onIgnore;

  /// Called when the later button is tapped or otherwise activated.
  /// Return false when the default behavior should not execute.
  BoolCallback onLater;

  /// Called when the update button is tapped or otherwise activated.
  /// Return false when the default behavior should not execute.
  BoolCallback onUpdate;

  bool _displayed = false;
  bool _initCalled = false;
  PackageInfo _packageInfo;

  String _installedVersion;
  String _appStoreVersion;
  String _appStoreListingURL;
  String _appStoreUpdateDescription;
  String _updateAvailable;
  DateTime _lastTimeAlerted;
  String _lastVersionAlerted;
  String _userIgnoredVersion;
  bool _hasAlerted = false;

  factory Upgrader() => _singleton;

  Upgrader._internal();

  Future<bool> initialize() async {
    clearSavedSettings();
    if (_initCalled) {
      return true;
    }

    _initCalled = true;

    await _getSavedPrefs();

    if (_packageInfo == null) {
      _packageInfo = await PackageInfo.fromPlatform();
      log.d('upgrader: package info packageName: ${_packageInfo.packageName}');
      log.d('upgrader: package info version: ${_packageInfo.version}');
    }

    await _updateVersionInfo();

    _installedVersion = _packageInfo.version;

    return true;
  }

  Future<bool> _updateVersionInfo() async {
    var remoteConfig = await AppConfig.instance.remoteConfig;

    if (remoteConfig == null) {
      log.e(
          '_updateVersionInfo | RemoteConfig unavailable - it is initialized');

      return false;
    }

    try {
      final Map<String, dynamic> versionInfo =
          json.decode(remoteConfig.getString('version_info'));

      _appStoreVersion = versionInfo['version'];

      if (Platform.isIOS) {
        _appStoreListingURL = versionInfo['ios_download_url'];
      } else if (Platform.isAndroid) {
        _appStoreListingURL = versionInfo['android_download_url'];
      } else {
        _appStoreListingURL = versionInfo['ios_download_url'];
      }

      _appStoreUpdateDescription = versionInfo['description'];
    } catch (exception) {
      log.e(
          '_updateVersion | Unable to use remote config. Cached or default values will be used',
          exception);

      return false;
    }

    return true;
  }

  void checkVersion({@required BuildContext context}) {
    if (!_displayed) {
      if (shouldDisplayUpgrade()) {
        _displayed = true;
        Future.delayed(Duration(milliseconds: 0), () {
          _showDialog(context);
        });
      }
    }
  }

  bool shouldDisplayUpgrade() {
    if (debugDisplayAlways || (debugDisplayOnce && !_hasAlerted)) {
      return true;
    }

    if (isTooSoon() || alreadyIgnoredThisVersion() || !isUpdateAvailable()) {
      return false;
    }
    return true;
  }

  bool isTooSoon() {
    if (_lastTimeAlerted == null) {
      return false;
    }

    final lastAlertedDuration = DateTime.now().difference(_lastTimeAlerted);
    return lastAlertedDuration.inDays < daysUntilAlertAgain;
  }

  bool alreadyIgnoredThisVersion() =>
      _userIgnoredVersion != null && _userIgnoredVersion == _appStoreVersion;

  bool isUpdateAvailable() {
    if (_appStoreVersion == null || _installedVersion == null) {
      return false;
    }

    if (_updateAvailable == null) {
      final appStoreVersion = Version.parse(_appStoreVersion);
      final installedVersion = Version.parse(_installedVersion);

      final available = appStoreVersion > installedVersion;
      _updateAvailable = available ? _appStoreVersion : null;

      log.d('isUpdateAvailable: appStoreVersion: $_appStoreVersion');
      log.d(
          'isUpdateAvailable: appStoreDescription: $_appStoreUpdateDescription');
      log.d('isUpdateAvailable: installedVersion: $_installedVersion');
      log.d('isUpdateAvailable: isUpdateAvailable: $available');
    }
    return _updateAvailable != null;
  }

  void onUserIgnored(BuildContext context, bool shouldPop) {
    // If this callback has been provided, call it.
    var doProcess = true;
    if (onIgnore != null) {
      doProcess = onIgnore();
    }

    if (doProcess) {
      _saveIgnored();
    }

    if (shouldPop) {
      _pop(context);
    }
  }

  void onUserLater(BuildContext context, bool shouldPop) {
    // If this callback has been provided, call it.
    var doProcess = true;
    if (onLater != null) {
      doProcess = onLater();
    }

    if (doProcess) {}

    if (shouldPop) {
      _pop(context);
    }
  }

  void onUserUpdated(BuildContext context, bool shouldPop) {
    // If this callback has been provided, call it.
    var doProcess = true;
    if (onUpdate != null) {
      doProcess = onUpdate();
    }

    if (doProcess) {
      _sendUserToAppStore(context);
    }

    if (shouldPop) {
      _pop(context);
    }
  }

  Future<bool> clearSavedSettings() async {
    var prefs = await SharedPreferences.getInstance();
    await prefs.remove('userIgnoredVersion');
    await prefs.remove('lastTimeAlerted');
    await prefs.remove('lastVersionAlerted');

    _userIgnoredVersion = null;
    _lastTimeAlerted = null;
    _lastVersionAlerted = null;

    return true;
  }

  static void resetSingleton() => _singleton = Upgrader._internal();

  void _pop(BuildContext context) {
    Navigator.of(context).pop();
    _displayed = false;
  }

  Future<bool> _saveIgnored() async {
    var prefs = await SharedPreferences.getInstance();

    _userIgnoredVersion = _appStoreVersion;
    await prefs.setString('userIgnoredVersion', _userIgnoredVersion);
    return true;
  }

  Future<bool> saveLastAlerted() async {
    var prefs = await SharedPreferences.getInstance();
    _lastTimeAlerted = DateTime.now();
    await prefs.setString('lastTimeAlerted', _lastTimeAlerted.toString());

    _lastVersionAlerted = _appStoreVersion;
    await prefs.setString('lastVersionAlerted', _lastVersionAlerted);

    _hasAlerted = true;
    return true;
  }

  Future<bool> _getSavedPrefs() async {
    var prefs = await SharedPreferences.getInstance();
    final lastTimeAlerted = prefs.getString('lastTimeAlerted');
    if (lastTimeAlerted != null) {
      _lastTimeAlerted = DateTime.parse(lastTimeAlerted);
    }

    _lastVersionAlerted = prefs.getString('lastVersionAlerted');

    _userIgnoredVersion = prefs.getString('userIgnoredVersion');

    return true;
  }

  void _sendUserToAppStore(BuildContext context) async {
    if (_appStoreListingURL == null || _appStoreListingURL.isEmpty) {
      log.w('_sendUserToAppStore: empty _appStoreListingURL');
      return;
    }

    log.d('_sendUserToAppStore: launching: $_appStoreListingURL');

    Utils.launchUrl(context, _appStoreListingURL, trackingName: 'app_update');
  }

  void _showDialog(context) {
    // Save the date/time as the last time alerted.
    saveLastAlerted();

    showDialog(
      barrierDismissible: false,
      context: context,
      builder: (BuildContext context) {
        return AlertDialog(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(8),
          ),
          backgroundColor: Colors.white,
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Stack(
                alignment: Alignment.center,
                children: <Widget>[
                  Container(
                    height: 56,
                    width: 56,
                    decoration: BoxDecoration(
                        border: Border.all(
                            width: 2,
                            color: Theme.of(context).brightness ==
                                    Brightness.dark
                                ? Theme.of(context).scaffoldBackgroundColor
                                : Theme.of(context).textTheme.bodyText2.color),
                        borderRadius: BorderRadius.circular(80)),
                  ),
                  Icon(AppIcons.wrench,
                      size: 26,
                      color: Theme.of(context).brightness == Brightness.dark
                          ? Theme.of(context).scaffoldBackgroundColor
                          : Theme.of(context).textTheme.bodyText2.color),
                ],
              ),
              Padding(
                padding: EdgeInsets.only(top: 16.0, bottom: 2),
                child: Text("Update App",
                    style: Theme.of(context).textTheme.headline6.merge(
                        TextStyle(
                            color:
                                Theme.of(context).brightness == Brightness.dark
                                    ? Theme.of(context).scaffoldBackgroundColor
                                    : Theme.of(context)
                                        .textTheme
                                        .bodyText2
                                        .color))),
              ),
              Text(
                "RYDR ($_appStoreVersion) is available!",
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(
                        color: Theme.of(context).hintColor,
                      ),
                    ),
              ),
              Padding(
                padding: EdgeInsets.only(top: 32.0, bottom: 2),
                child: Text("What's New",
                    style: Theme.of(context).textTheme.bodyText1.merge(
                        TextStyle(
                            color:
                                Theme.of(context).brightness == Brightness.dark
                                    ? Theme.of(context).scaffoldBackgroundColor
                                    : Theme.of(context)
                                        .textTheme
                                        .bodyText2
                                        .color))),
              ),
              Text("$_appStoreUpdateDescription",
                  style: Theme.of(context).textTheme.bodyText2.merge(TextStyle(
                      color: Theme.of(context).brightness == Brightness.dark
                          ? Theme.of(context).scaffoldBackgroundColor
                          : Theme.of(context).textTheme.bodyText2.color))),
            ],
          ),
          actions: <Widget>[
            FlatButton(
                child: Text("Ignore"),
                textColor: Theme.of(context).hintColor,
                onPressed: () => onUserIgnored(context, true)),
            FlatButton(
                child: Text("Later"),
                textColor: Theme.of(context).hintColor,
                onPressed: () => onUserLater(context, true)),
            FlatButton(
                child: Text("Update"),
                textColor: Theme.of(context).primaryColor,
                onPressed: () => onUserUpdated(context, true)),
          ],
        );
      },
    );
  }
}

class UpgraderCheck extends StatelessWidget {
  @override
  Widget build(BuildContext context) => FutureBuilder(
      future: Upgrader().initialize(),
      builder: (BuildContext context, AsyncSnapshot<bool> processed) {
        if (processed.connectionState == ConnectionState.done) {
          Upgrader().checkVersion(context: context);
        }
        return Container();
      });
}
