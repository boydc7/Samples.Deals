import 'package:flutter/material.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/ui/profile/insights_profile.dart';
import 'package:rydr_app/ui/profile/insights_posts.dart';
import 'package:rydr_app/ui/profile/insights_followers.dart';
import 'package:rydr_app/ui/profile/insights_story.dart';
import 'package:rydr_app/ui/profile/insights_media_analysis.dart';

class ProfileSummarySections extends StatelessWidget {
  final PublisherAccount profile;
  final Deal deal;

  final String brainIconUrl = 'assets/icons/brain-icon.svg';
  final String postIconUrl = 'assets/icons/post-icon.svg';
  final String storyIconUrl = 'assets/icons/story-icon.svg';

  ProfileSummarySections(this.profile, [this.deal]);

  void showBasicModal(BuildContext context) {
    /// TODO: Brian, if this is someone viewing another persons' profile page
    /// like a business viewing a creator, then we should change the message
    /// in the bottom modal below, or should we remove the summary sections
    /// and instead have a message there "this is a basic IG profile"
    ///
    /// you would use the flag below...
    final bool isMe = profile.id == appState.currentProfile.id;

    showSharedModalBottomInfo(
      context,
      initialRatio: 0.45,
      topWidget: Container(
        height: 56,
        width: 56,
        decoration: BoxDecoration(
          image: DecorationImage(
            fit: BoxFit.cover,
            image: AssetImage(
              'assets/icons/instagram-logo-gradient.png',
            ),
          ),
        ),
      ),
      title: "Detailed Instagram Insights",
      subtitle: !profile.isAccountFull
          ? profile.isPrivate
              ? "Your Instagram profile is private."
              : profile.isBusiness
                  ? "Log in with Facebook to pay with stories."
                  : "Your Instagram profile is not a professional profile."
          : "You are one step away!",
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              "Instagram only allows us to see detailed insights (like story impressions and reach over time, follower growth, etc.) if you have an Instagram professional account connected to a Facebook Page. Continue below to get converted.",
              textAlign: TextAlign.center,
            ),
            Padding(
              padding: EdgeInsets.only(top: 16.0),
              child: PrimaryButton(
                label: profile.isBusiness && !profile.isAccountFull
                    ? "How to Link Facebook Page"
                    : "How to Convert Your Account",
                onTap: () => Utils.launchUrl(
                  context,
                  profile.isBusiness && !profile.isAccountFull
                      ? "https://getrydr.com/support/no-instagram-professional-profiles-linked-to-your-facebook-account/#linking-account"
                      : "https://getrydr.com/support/no-instagram-professional-profiles-linked-to-your-facebook-account/#no-profile",
                ),
              ),
            )
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    /// only available to basic and full, though for basic we show locked icon
    /// and information overlay for how to get more stats
    if (profile.isAccountSoft) {
      return Container();
    }

    /// check if we should show the ai section, this should show if either
    /// its already enabled by the profile and the current viewer (business) is allowed to see it
    /// or if we have a creator at which point we'd show it even if they've not yet enabled it
    final bool mediaAnalysisIsAvailable =
        appState.isAiAvailable(profile) || appState.currentProfile.isCreator;

    return Column(
      children: <Widget>[
        SizedBox(height: 4.0),
        rydrListItem(
          context: context,
          iconSvgUrl: storyIconUrl,
          iconSvgWidth: 17.5,
          title: 'Instagram Story Insights',
          subtitle: 'Story impressions, reach, and engagement',
          subtitleIsHint: true,
          onTap: profile.isAccountBasic
              ? () => showBasicModal(context)
              : () => Navigator.of(context).push(
                    MaterialPageRoute(
                        builder: (BuildContext context) =>
                            ProfileInsightsStory(profile),
                        settings: AppAnalytics.instance
                            .getRouteSettings('profile/insights/stories')),
                  ),
          loading: profile.isAccountFull && profile.lastSyncedOnDisplay == null,
          isBasic: profile.isAccountBasic,
        ),
        rydrListItem(
          context: context,
          iconSvgUrl: postIconUrl,
          iconSvgWidth: 22.0,
          title: 'Instagram Post Insights',
          subtitle: 'Post impressions, reach, and engagement',
          subtitleIsHint: true,
          onTap: profile.isAccountBasic
              ? () => showBasicModal(context)
              : () => Navigator.of(context).push(
                    MaterialPageRoute(
                        builder: (BuildContext context) =>
                            ProfileInsightsPosts(profile),
                        settings: AppAnalytics.instance
                            .getRouteSettings('profile/insights/posts')),
                  ),
          loading: profile.isAccountFull && profile.lastSyncedOnDisplay == null,
          isBasic: profile.isAccountBasic,
        ),
        rydrListItem(
          context: context,
          icon: AppIcons.chartLine,
          title: 'Instagram Follower Insights',
          subtitle: 'Location, growth, age, and gender',
          subtitleIsHint: true,
          onTap: profile.isAccountBasic
              ? () => showBasicModal(context)
              : () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (BuildContext context) =>
                          ProfileInsightsFollowers(profile, deal),
                      settings: AppAnalytics.instance
                          .getRouteSettings('profile/insights/followers'),
                    ),
                  ),
          loading: profile.isAccountFull && profile.lastSyncedOnDisplay == null,
          isBasic: profile.isAccountBasic,
        ),
        rydrListItem(
          context: context,
          icon: AppIcons.userCircle,
          title: 'Profile Interactions and Discovery',
          subtitle: 'Views, impressions, reach, and clicks',
          subtitleIsHint: true,
          onTap: profile.isAccountBasic
              ? () => showBasicModal(context)
              : () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (BuildContext context) =>
                          ProfileInteractionsDiscovery(profile),
                      settings: AppAnalytics.instance
                          .getRouteSettings('profile/insights/profile'),
                    ),
                  ),
          loading: profile.isAccountFull && profile.lastSyncedOnDisplay == null,
          isBasic: profile.isAccountBasic,
        ),
        mediaAnalysisIsAvailable
            ? rydrListItem(
                context: context,
                iconSvgUrl: brainIconUrl,
                title: 'Selfie Vision™',
                subtitle: 'Instagram Media & Content Analysis',
                subtitleIsHint: true,
                onTap: profile.isAccountBasic
                    ? () => showBasicModal(context)
                    : () => Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (BuildContext context) =>
                                ProfileInsightsMediaAnalysis(profile),
                            settings: AppAnalytics.instance
                                .getRouteSettings('profile/insights/media'),
                          ),
                        ),
                loading: profile.isAccountFull &&
                    profile.lastSyncedOnDisplay == null,
                isBasic: profile.isAccountBasic,
                lastInList: true,
              )
            : Container(height: 0),
      ],
    );
  }
}
