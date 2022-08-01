import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class InvitationsTile extends StatelessWidget {
  final String title = "Invitations";
  final String subtitle = "Personal invites from businesses";

  InvitationsTile();

  void _goToInvites(BuildContext context) => Navigator.of(context).pushNamed(
        AppRouting.getRequestsInvited,
        arguments: ListPageArguments(filterRequestStatus: [
          DealRequestStatus.invited,
        ], layoutType: ListPageLayout.StandAlone),
      );

  @override
  Widget build(BuildContext context) {
    if (appState.currentProfile == null) {
      return Container(height: 0);
    }

    return StreamBuilder<PublisherAccountStatsResponse>(
      stream: appState.currentProfileStats,
      builder: (context, snapshot) {
        return appState.currentProfile.isCreator &&
                snapshot.data != null &&
                snapshot.data.error == null &&
                snapshot.data.model
                        .tryGetDealStatValue(DealStatType.currentInvites) >
                    0
            ? ListTileTheme(
                textColor: Theme.of(context).textTheme.bodyText2.color,
                child: ListTile(
                  contentPadding:
                      EdgeInsets.symmetric(vertical: 8.0, horizontal: 16.0),
                  onTap: () => _goToInvites(context),
                  leading: Container(
                    height: 40.0,
                    width: 40.0,
                    decoration: BoxDecoration(
                      color: Theme.of(context).scaffoldBackgroundColor,
                      borderRadius: BorderRadius.circular(20.0),
                      border: Border.all(
                          width: 1.35,
                          color: Theme.of(context).textTheme.bodyText2.color),
                    ),
                    child: Center(
                      child: Padding(
                        padding: EdgeInsets.only(bottom: 1.0),
                        child: Icon(
                          AppIcons.envelope,
                          color: Theme.of(context).textTheme.bodyText2.color,
                          size: 22.0,
                        ),
                      ),
                    ),
                  ),
                  title:
                      Text(title, style: Theme.of(context).textTheme.bodyText1),
                  subtitle: Text(
                    subtitle,
                    style: TextStyle(color: Theme.of(context).hintColor),
                  ),
                  trailing: FadeInScaleUp(
                    10,
                    Badge(
                      elevation: 0.0,
                      large: true,
                      color: Theme.of(context).primaryColor,
                      valueColor: Theme.of(context).scaffoldBackgroundColor,
                      value: snapshot.data.model
                          .tryGetDealStatValue(DealStatType.currentInvites)
                          .toString(),
                    ),
                  ),
                ),
              )
            : Container();
      },
    );
  }
}
