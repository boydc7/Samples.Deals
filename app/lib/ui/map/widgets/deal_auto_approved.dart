import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/services/device_settings.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/sprite_animation.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';

class DealAutoApproved extends StatefulWidget {
  final Deal deal;

  DealAutoApproved(this.deal);

  @override
  _DealAutoApprovedState createState() => _DealAutoApprovedState();
}

class _DealAutoApprovedState extends State<DealAutoApproved>
    with SingleTickerProviderStateMixin {
  ThemeData _theme;
  Deal _deal;
  PageController _pageController;

  @override
  void initState() {
    super.initState();

    _deal = widget.deal;

    _pageController = PageController(initialPage: 0);
  }

  @override
  void dispose() {
    _pageController.dispose();

    super.dispose();
  }

  void _onRedeemLater() {
    /// check if we've shown the user the redeem later onboarding information
    /// if not, navigate to the next page, otherwise close and reload the map
    if (!appState.onboardSettings.creatorSawAutoApprove) {
      /// save a setting for the user that we've shown them this before
      DeviceSettings.saveOnboardSettings(
          appState.onboardSettings..creatorSawAutoApprove = true);

      /// take them to the next page where we show them how to get to
      /// the redeemed request later on their profile page
      _pageController.animateToPage(
        1,
        duration: Duration(milliseconds: 350),
        curve: Curves.easeInOut,
      );
    } else {
      Navigator.of(context).pop();
      Navigator.of(context).pushReplacementNamed(AppRouting.getDealsMap);
    }
  }

  @override
  Widget build(BuildContext context) {
    _theme = Theme.of(context);

    return Container(
      margin: EdgeInsets.symmetric(vertical: 16.0),
      padding: EdgeInsets.symmetric(horizontal: 32, vertical: 16),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          stops: [
            0.1,
            0.9,
          ],
          colors: [
            Colors.black,
            AppColors.grey800,
          ],
        ),
        color: _theme.scaffoldBackgroundColor,
        borderRadius: BorderRadius.circular(16.0),
        boxShadow: AppShadows.elevation[3],
      ),
      constraints: BoxConstraints(
          maxHeight: 400, maxWidth: MediaQuery.of(context).size.width),
      child: PageView(
        controller: _pageController,
        physics: NeverScrollableScrollPhysics(),
        children: <Widget>[_buildApproved(), _buildRedeemLater()],
      ),
    );
  }

  Widget _buildApproved() => Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Opacity(
                  opacity: 0.25,
                  child: SpriteAnimation(Theme.of(context).primaryColor),
                ),
                Container(
                  width: 90,
                  height: 90,
                  child: UserAvatar(
                    _deal.publisherAccount,
                    width: 90,
                  ),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(100),
                  ),
                ),
              ],
            ),
          ),
          Text(
            'You are approved!',
            textAlign: TextAlign.center,
            style: _theme.textTheme.headline6.merge(
              TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          Padding(
            padding: EdgeInsets.only(left: 16.0, right: 16, bottom: 16, top: 8),
            child: Text(
              "Redeem your RYDR now or use it later.",
              textAlign: TextAlign.center,
              style: _theme.textTheme.caption.merge(
                TextStyle(color: Colors.white),
              ),
            ),
          ),
          SizedBox(height: 8.0),
          PrimaryButton(
            label: "Redeem Now",
            hasIcon: true,
            hasShadow: true,
            icon: AppIcons.ticketAltSolid,
            rotateIcon: true,
            onTap: () {
              Navigator.of(context).pop();

              /// send the user directly to the 'redeem' page
              Navigator.of(context).pushNamed(
                AppRouting.getRequestRedeemRoute(
                  _deal.id,
                  appState.currentProfile.id,
                ),
              );
            },
          ),
          SizedBox(height: 8),
          PrimaryButton(
            label: "Redeem Later",
            labelColor: AppColors.grey400,
            buttonColor: AppColors.grey800,
            onTap: _onRedeemLater,
          ),
        ],
      );

  Widget _buildRedeemLater() => Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Center(
              child: Container(
                width: 90,
                height: 90,
                child: Icon(AppIcons.megaphone, size: 40, color: Colors.white),
              ),
            ),
          ),
          Text(
            'Saved for Later',
            textAlign: TextAlign.center,
            style: _theme.textTheme.headline6.merge(
              TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          Padding(
            padding: EdgeInsets.only(left: 16.0, right: 16, bottom: 16, top: 8),
            child: Text(
              "Go to your profile and tap the megaphone icon to see all active, pending, and completed RYDRs.",
              textAlign: TextAlign.center,
              style: _theme.textTheme.caption.merge(
                TextStyle(color: Colors.white),
              ),
            ),
          ),
          SizedBox(height: 8.0),
          PrimaryButton(
            label: "Got It",
            hasIcon: true,
            hasShadow: true,
            icon: AppIcons.arrowRight,
            onTap: () {
              Navigator.of(context).pop();
              Navigator.of(context)
                  .pushReplacementNamed(AppRouting.getDealsMap);
            },
          ),
          SizedBox(height: 16.0),

          /// TODO: Brian change button style/text as needed
          SecondaryButton(
            fullWidth: true,
            label: "Take me there",
            onTap: () {
              Navigator.of(context).pop();
              Navigator.of(context)
                  .pushReplacementNamed(AppRouting.getProfileRequestsRoute);
            },
          ),
        ],
      );
}
