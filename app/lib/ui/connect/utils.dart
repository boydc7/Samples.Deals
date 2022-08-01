import 'dart:async';

import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/services/authenticate.dart';
import 'package:rydr_app/services/publisher_account.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/analytics.dart';

class ConnectUtils {
  static Future<PublisherAccount> linkUser(
    PublisherAccount userToLink,
    PublisherType publisherType,
    RydrAccountType linkAsType,
  ) async {
    final PublisherAccount linkedUserResponse =
        await PublisherAccountService.linkProfile(
            userToLink: userToLink,
            publisherType: publisherType,
            rydrType: linkAsType);

    /// get the linked user (which would be null if not successful)
    final PublisherAccount linkedUser = linkedUserResponse;

    /// if we've successuflly linked the account to the masteruser
    /// then refresh our workspaces
    if (linkedUser != null) {
      await AuthenticationService.instance().loadWorkspaces();

      /// switch to the new profile
      await appState.switchProfile(linkedUser.id);

      /// log as a screen in analytics
      AppAnalytics.instance.logScreen(linkAsType == RydrAccountType.business
          ? 'connect/linked/business'
          : 'connect/linked/creator');
    }

    return linkedUser;
  }
}
