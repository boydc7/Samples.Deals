import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class RedeemedTile extends StatelessWidget {
  final String title = "Action Required";
  final String subtitle = "Select posts and complete";

  RedeemedTile();

  void _goToInProgress(BuildContext context) => Navigator.of(context).pushNamed(
        AppRouting.getRequestsRedeemed,
        arguments: ListPageArguments(filterRequestStatus: [
          DealRequestStatus.redeemed,
        ], layoutType: ListPageLayout.StandAlone),
      );

  @override
  Widget build(BuildContext context) => appState.currentProfile == null
      ? Container(height: 0)
      : StreamBuilder<PublisherAccountStatsResponse>(
          stream: appState.currentProfileStats,
          builder: (context, snapshot) {
            return appState.currentProfile != null &&
                    appState.currentProfile.isCreator &&
                    snapshot.data != null &&
                    snapshot.data.error == null &&
                    snapshot.data.model
                            .tryGetDealStatValue(DealStatType.currentRedeemed) >
                        0
                ? Container(
                    color: Theme.of(context).primaryColor,
                    child: ListTileTheme(
                      textColor: Theme.of(context).textTheme.bodyText2.color,
                      child: ListTile(
                        contentPadding: EdgeInsets.symmetric(
                            vertical: 8.0, horizontal: 16.0),
                        onTap: () => _goToInProgress(context),
                        leading: Container(
                          height: 40.0,
                          width: 40.0,
                          child: Center(
                            child: Padding(
                              padding: EdgeInsets.only(right: 3.0, bottom: 3.0),
                              child: Icon(
                                AppIcons.exclamationTriangle,
                                color: Colors.white,
                                size: 28.0,
                              ),
                            ),
                          ),
                        ),
                        title: Text(
                          title,
                          style: Theme.of(context).textTheme.bodyText1.merge(
                                TextStyle(
                                  color: Colors.white,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                        ),
                        subtitle: Text(
                          subtitle,
                          style: TextStyle(color: Colors.white70),
                        ),
                        trailing: FadeInScaleUp(
                          10,
                          Badge(
                            elevation: 0.0,
                            large: true,
                            color: Colors.white,
                            valueColor: Theme.of(context).primaryColor,
                            value: snapshot.data.model
                                .tryGetDealStatValue(
                                    DealStatType.currentRedeemed)
                                .toString(),
                          ),
                        ),
                      ),
                    ),
                  )
                : Container(height: 0);
          },
        );
}
