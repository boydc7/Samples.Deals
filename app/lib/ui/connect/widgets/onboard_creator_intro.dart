import 'package:flutter/material.dart';

import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/ui/connect/widgets/helpers.dart';

class OnboardCreatorScreenIntro extends StatelessWidget {
  final Function moveForward;

  OnboardCreatorScreenIntro({
    @required this.moveForward,
  });

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: true,
      bottom: true,
      child: Container(
        height: MediaQuery.of(context).size.height,
        width: MediaQuery.of(context).size.width,
        padding: EdgeInsets.only(left: 32.0, right: 32.0),
        child: Column(
          children: <Widget>[
            onboardUserHeader(context),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.spaceAround,
                mainAxisSize: MainAxisSize.max,
                children: <Widget>[
                  onboardFadeInTile(
                    context: context,
                    delay: 5,
                    leadingIcon: AppIcons.mapMarkedAltSolid,
                    title: "What is a RYDR?",
                    subTitle:
                        "A deal from a local business for goods or services in exchange for your Instagram Stories or Posts.",
                  ),
                  onboardFadeInTile(
                    context: context,
                    delay: 30,
                    leadingIcon: AppIcons.megaphoneSolid,
                    title: "Explore and Request",
                    subTitle:
                        "Request a RYDR and businesses will see you're interested, view your insights, and approve or decline your request.",
                  ),
                  onboardFadeInTile(
                    context: context,
                    delay: 55,
                    leadingIcon: AppIcons.solidImages,
                    title: "Redeem. Shoot. Post.",
                    subTitle:
                        "Once approved, redeem your RYDR and post the required amount of Stories and Posts.",
                  ),
                  onboardFadeInTile(
                    context: context,
                    delay: 75,
                    leadingIcon: AppIcons.checkDoubleReg,
                    title: "Select Posts to Share Insights",
                    subTitle:
                        "Complete your RYDR by selecting the Stories and Posts, giving the business visibility to valueable insights.",
                  ),
                ],
              ),
            ),
            SizedBox(
              height: 32.0,
            ),
            FadeInOpacityOnly(
              100,
              PrimaryButton(
                label: 'Continue',
                hasIcon: true,
                icon: AppIcons.arrowRight,
                onTap: moveForward,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
