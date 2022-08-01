import 'package:flutter/material.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/ui/shared/widgets/user_avatar.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/state.dart';

class DealBrand extends StatelessWidget {
  final Deal deal;

  DealBrand(this.deal);

  @override
  Widget build(BuildContext context) {
    /// showing the brand is only applicable to when its not the business
    /// looking at their own deal, but an influencer
    return appState.currentProfile.isBusiness
        ? Container()
        : Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              SizedBox(height: 3.0),
              ListTile(
                leading: UserAvatar(deal.publisherAccount),
                title: Text(
                  deal.publisherAccount.userName,
                  style: Theme.of(context).textTheme.bodyText1,
                ),
                subtitle: Text(deal.publisherAccount.nameDisplay,
                    style: Theme.of(context).textTheme.bodyText2),
                trailing: Icon(
                  AppIcons.angleRight,
                  color: Theme.of(context).iconTheme.color,
                ),
                onTap: () => Utils.goToProfile(
                  context,
                  deal.publisherAccount,
                  deal,
                ),
              ),
              SizedBox(height: 4.0),
              Divider(indent: 72, height: 1)
            ],
          );
  }
}
