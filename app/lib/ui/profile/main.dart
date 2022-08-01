import 'package:flutter/material.dart';

import 'creator.dart';
import 'profile.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/state.dart';

class MainProfile extends StatelessWidget {
  final int profileId;
  final Deal deal;

  const MainProfile({
    this.profileId,
    this.deal,
  });

  @override
  Widget build(BuildContext context) {
    /// if we have the user him/herself, or an influencer looking
    /// at a profile page then use the profile, otherwise it would
    /// be a business looking at a creators page, so show the creator specific profile
    return appState.currentProfile.id == profileId ||
            appState.currentProfile.isCreator
        ? ProfilePage(profileId)
        : ProfileCreatorPage(deal);
  }
}
