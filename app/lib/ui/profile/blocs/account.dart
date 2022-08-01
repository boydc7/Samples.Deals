import 'dart:async';

import 'package:rxdart/rxdart.dart';

import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';

import 'package:rydr_app/services/publisher_account.dart';
import 'package:rydr_app/services/notifications.dart';

class AccountBloc {
  final _showIdentifiers = BehaviorSubject<bool>.seeded(false);
  final unreadNotifications =
      BehaviorSubject<int>.seeded(appState.currentProfile.unreadNotifications);

  bool _showAiSettings;

  AccountBloc() {
    /// determine if this account should see ai settings
    /// they either have to be a creator, or a paid business
    _showAiSettings =
        appState.currentProfile.isCreator || appState.isBusinessPro;
  }

  dispose() {
    _showIdentifiers.close();
    unreadNotifications.close();
  }

  bool get showAiSettings => _showAiSettings;
  BehaviorSubject<bool> get showIdentifiers => _showIdentifiers.stream;

  void setShowIdentifiers() => _showIdentifiers.sink.add(true);

  Future<bool> setOptInToAi(bool optIn) async {
    final res = await PublisherAccountService.optInToAi(optIn);

    if (res.error == null) {
      appState.setCurrentProfileOptInToAi(optIn);

      AppAnalytics.instance.logScreen(
        optIn
            ? 'profile/settings/account/aioptin'
            : 'profile/settings/account/aioptout',
      );

      /// update the current profile in state
      appState.currentProfile.optInToAi = optIn;
      return true;
    }

    return false;
  }

  Future<bool> unLink() async {
    // Delink the profile from this user/workspace, then re-subscribe notifications on this device
    final unlinkUserResponse = await PublisherAccountService.unLinkProfile(
      appState.currentWorkspace.id,
      appState.currentProfile,
    );

    return unlinkUserResponse.error == null;
  }

  void markNotificationsAsRead() async {
    NotificationService.markAsRead();

    appState.clearNotificationsCount();

    unreadNotifications.add(0);
  }

  Future<bool> switchAccount(RydrAccountType toType) async {
    final res = await PublisherAccountService.switchAccountType(toType);

    return res.error == null;
  }
}
