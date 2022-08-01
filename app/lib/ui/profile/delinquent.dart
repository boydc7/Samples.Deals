import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';

class ProfileDelinquent extends StatelessWidget {
  void _backToPages(context) => Navigator.of(context).pushNamedAndRemoveUntil(
      AppRouting.getConnectPages, (Route<dynamic> route) => false);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        elevation: 0,
        leading:
            AppBarBackButton(context, onPressed: () => _backToPages(context)),
      ),
      body: Container(
        width: double.infinity,
        padding: EdgeInsets.symmetric(horizontal: 32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(
              AppIcons.ban,
              size: 48,
              color: AppColors.errorRed,
            ),
            SizedBox(height: 8),
            Text("Account Suspended",
                style: Theme.of(context).textTheme.headline6),
            SizedBox(height: 4),
            Text(
              "RYDR prides itself on being able to bring a reliable and scalable marketing channel to local small businesses. To do so, we protect businesses from Creators who do not fulfill their obligation to provide value.\n\nYou have reached your limit of delinquent RYDRs.",
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                      color: Theme.of(context).hintColor,
                    ),
                  ),
            ),
            SizedBox(height: 24),
            SecondaryButton(
              label: "Contact RYDR Support",
              onTap: () {
                Utils.launchUrl(
                    context,
                    Uri.encodeFull(
                        "mailto:contest@getrydr.com?subject=Contesting Delinquent RYDR: ${appState.currentProfile.userName} (${appState.currentProfile.id})&body=Please give a reason for your delinquent RYDR..."));
              },
            ),
            SizedBox(height: 8),
            Text(
              "or email contest@getrydr.com",
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                      fontSize: 10,
                    ),
                  ),
            ),
            SizedBox(height: kToolbarHeight),
          ],
        ),
      ),
    );
  }
}
