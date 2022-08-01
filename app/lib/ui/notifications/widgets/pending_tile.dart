import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/ui/shared/widgets/badge.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class PendingTile extends StatelessWidget {
  final String title = "Pending Requests";
  final String subtitle = "Creators waiting your response";
  final String assetPath = "assets/icons/requests.svg";

  PendingTile();

  void _goToPending(BuildContext context) => Navigator.of(context).pushNamed(
        AppRouting.getRequestsPending,
        arguments: ListPageArguments(filterRequestStatus: [
          DealRequestStatus.requested,
        ], layoutType: ListPageLayout.StandAlone),
      );

  @override
  Widget build(BuildContext context) => appState.currentProfile == null
      ? Container(height: 0)
      : StreamBuilder<PublisherAccountStatsResponse>(
          stream: appState.currentProfileStats,
          builder: (context, snapshot) {
            return appState.currentProfile.isBusiness &&
                    snapshot.data != null &&
                    snapshot.data.error == null &&
                    snapshot.data.model.tryGetDealStatValue(
                            DealStatType.currentRequested) >
                        0
                ? ListTileTheme(
                    textColor: Theme.of(context).textTheme.bodyText2.color,
                    child: ListTile(
                      contentPadding:
                          EdgeInsets.symmetric(vertical: 8.0, horizontal: 16.0),
                      onTap: () => _goToPending(context),
                      leading: Container(
                        height: 40.0,
                        width: 40.0,
                        decoration: BoxDecoration(
                          color: Theme.of(context).scaffoldBackgroundColor,
                          borderRadius: BorderRadius.circular(20.0),
                          border: Border.all(
                              width: 1.35,
                              color:
                                  Theme.of(context).textTheme.bodyText2.color),
                        ),
                        child: Center(
                          child: SizedBox(
                            height: 22.0,
                            width: 22.0,
                            child: SvgPicture.asset(
                              assetPath,
                              color:
                                  Theme.of(context).textTheme.bodyText2.color,
                            ),
                          ),
                        ),
                      ),
                      title: Text(title,
                          style: Theme.of(context).textTheme.bodyText1),
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
                              .tryGetDealStatValue(
                                  DealStatType.currentRequested)
                              .toString(),
                        ),
                      ),
                    ),
                  )
                : Container(height: 0);
          },
        );
}
