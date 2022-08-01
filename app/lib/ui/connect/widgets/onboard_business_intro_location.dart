import 'package:flutter/material.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/ui/shared/widgets/buttons.dart';

import 'package:rydr_app/ui/connect/widgets/helpers.dart';

/// Explains local focus of RYDR, then primes them to go to the next page
/// where they'll choose their business location. Has no iteractivity other than going to next page
class OnboardBusinessScreenIntroLocation extends StatelessWidget {
  final Function moveForward;

  OnboardBusinessScreenIntroLocation({
    @required this.moveForward,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.symmetric(horizontal: 32.0),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Padding(
              padding: EdgeInsets.symmetric(horizontal: 16.0),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  onboardFadeInTile(
                    context: context,
                    delay: 5,
                    leadingIcon: AppIcons.streetViewSolid,
                    title: "Get discovered",
                    subTitle:
                        "Creators find you based on your RYDR's location.",
                  ),
                  onboardFadeInTile(
                    context: context,
                    delay: 10,
                    leadingIcon: AppIcons.mapMarkerAltSolid,
                    title: "Local focused",
                    subTitle:
                        "Every RYDR requires a physical location where the Creator will post.",
                  ),
                  onboardFadeInTile(
                    context: context,
                    delay: 15,
                    leadingIcon: AppIcons.locationArrowSolid,
                    title: "Unlimited locations",
                    subTitle:
                        "One location or multiple venues, each RYDR is unique.",
                  ),
                ],
              ),
            ),
          ),
          PrimaryButton(
            label: 'Continue',
            onTap: moveForward,
          ),
          SizedBox(
            height: 8.0,
          ),
          Text(
            "Additional locations can be added later.",
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.caption.merge(
                  TextStyle(color: AppColors.grey400),
                ),
          ),
        ],
      ),
    );
  }
}
