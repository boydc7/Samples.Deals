import 'package:flutter/material.dart';

import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

import 'package:rydr_app/ui/connect/widgets/helpers.dart';

/// Explains in short bullet points what the business can do with RYDR
/// has no interactivity other than the user moving to the next page
class OnboardBusinessScreenIntro extends StatelessWidget {
  final Function moveForward;

  OnboardBusinessScreenIntro({
    this.moveForward,
  });

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: true,
      child: Padding(
        padding: EdgeInsets.only(left: 32.0, right: 32.0),
        child: Column(
          children: <Widget>[
            onboardUserHeader(context),
            Expanded(
              child: Column(
                // mainAxisAlignment: MainAxisAlignment.spaceAround,
                mainAxisSize: MainAxisSize.max,
                children: <Widget>[
                  onboardFadeInTile(
                    context: context,
                    delay: 5,
                    leadingIndex: "1",
                    title: "Create a RYDR",
                    subTitle:
                        "Goods, services, or discounts you're giving in exchange for Instagram stories or posts from a Creator.",
                  ),
                  SizedBox(height: 32.0),
                  onboardFadeInTile(
                    context: context,
                    delay: 30,
                    leadingIndex: "2",
                    title: "Approve Requests",
                    subTitle:
                        "Only Creators that meet or exceed the thresholds you set will be able to view and request your RYDR.",
                  ),
                  SizedBox(height: 32.0),
                  onboardFadeInTile(
                    context: context,
                    delay: 55,
                    leadingIndex: "3",
                    title: "View Insights",
                    subTitle:
                        "Creators select the stories and posts used for the RYDR and you see all the insights.",
                  ),
                ],
              ),
            ),
            SizedBox(
              height: 32.0,
            ),
            FadeInOpacityOnly(
              75,
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
