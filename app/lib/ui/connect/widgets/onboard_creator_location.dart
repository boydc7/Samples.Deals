import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:location_permissions/location_permissions.dart';
import 'package:rydr_app/app/links.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

import 'package:rydr_app/services/location.dart';

import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class OnboardCreatorScreenLocationPrimer extends StatefulWidget {
  final Function moveNext;

  OnboardCreatorScreenLocationPrimer({
    @required this.moveNext,
  });

  @override
  State<StatefulWidget> createState() {
    return _OnboardCreatorScreenLocationPrimerState();
  }
}

class _OnboardCreatorScreenLocationPrimerState
    extends State<OnboardCreatorScreenLocationPrimer> {
  @override
  void initState() {
    super.initState();
  }

  void _locationPrompt() async {
    final PermissionStatus status =
        await LocationService.getInstance().requestPermission();

    /// NOTE: problem here is that the status will be 'denied' even when the user chooses
    /// 'only once' - so both that choice and denying completly will result in this alert...
    if (status == PermissionStatus.denied) {
      /// if the user chose not to allow permission for location, then show them an alert
      /// explanining why they should have it on, and then send them to next if they hit "OK"
      showSharedModalAlert(
        context,
        Text("Location Services"),
        content: Text(
            "We recommend that location services are turned on while using this app. We can then give you accurate distances, detailed directions, and better suggested offers based on your location. Go to Settings > ${AppName.rydr} > Location and change to 'While Using'."),
        actions: <ModalAlertAction>[
          ModalAlertAction(
            label: "OK",
            onPressed: () {
              Navigator.of(context).pop();
              widget.moveNext();
            },
          )
        ],
      );
    } else {
      /// move them to next page
      widget.moveNext();
    }
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.symmetric(horizontal: 32.0),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Expanded(
              child: FadeInOpacityOnly(
                15,
                SizedBox(
                  height: 140.0,
                  width: 140.0,
                  child: SvgPicture.asset(
                    'assets/icons/access-location-marker.svg',
                    color: Theme.of(context).textTheme.bodyText2.color,
                  ),
                ),
              ),
            ),
            Container(
              height: 140.0,
              child: Column(
                children: <Widget>[
                  FadeInTopBottom(
                    15,
                    Text(
                      "Let's Get Started",
                      style: Theme.of(context).textTheme.bodyText1.merge(
                            TextStyle(fontSize: 18.0),
                          ),
                    ),
                    300,
                    begin: -40,
                  ),
                  SizedBox(
                    height: 8.0,
                  ),
                  FadeInTopBottom(
                    15,
                    Text(
                      "We would like to access your location so you can quickly request RYDRs near you.",
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodyText2.merge(
                            TextStyle(color: Theme.of(context).hintColor),
                          ),
                    ),
                    300,
                    begin: -40,
                  ),
                ],
              ),
            ),
            FadeInOpacityOnly(
              35,
              Row(
                children: <Widget>[
                  Expanded(
                    child: PrimaryButton(
                      labelColor: Theme.of(context).hintColor,
                      buttonColor: Theme.of(context).canvasColor,
                      label: 'Not Now',
                      onTap: widget.moveNext,
                    ),
                  ),
                  SizedBox(
                    width: 8,
                  ),
                  Expanded(
                    child: PrimaryButton(
                      label: 'Allow',
                      onTap: _locationPrompt,
                    ),
                  ),
                ],
              ),
            ),
            SizedBox(
              height: 8.0,
            ),
            FadeInOpacityOnly(
              35,
              Text(
                "We don't do anything creepy. Just directions. 📍",
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.caption.merge(
                      TextStyle(color: Theme.of(context).hintColor),
                    ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
