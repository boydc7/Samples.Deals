import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/ui/main/list_requests.dart';
import 'package:rydr_app/ui/profile/blocs/creator.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

class CreatorWorkHistorySection extends StatefulWidget {
  final CreatorBloc bloc;
  final PublisherAccount profile;

  CreatorWorkHistorySection(this.bloc, this.profile);

  @override
  _CreatorWorkHistorySectionState createState() =>
      _CreatorWorkHistorySectionState();
}

class _CreatorWorkHistorySectionState extends State<CreatorWorkHistorySection> {
  @override
  void initState() {
    super.initState();

    /// creator history is only available to paid profiles
    if (appState.isBusinessPro) {
      widget.bloc.loadWorkHistory(widget.profile.id);
    }
  }

  void _goToRequests(BuildContext context) => Navigator.of(context).push(
        MaterialPageRoute(
          builder: (context) {
            return ListRequests(
              arguments: ListPageArguments(
                filterDealRequestPublisherAccountId: widget.profile.id,
                filterDealPublisherAccountName: widget.profile.userName,
                isRequests: true,
                layoutType: ListPageLayout.StandAlone,
                isCreatorHistory: true,
                filterRequestStatus: [
                  DealRequestStatus.completed,
                  DealRequestStatus.inProgress,
                  DealRequestStatus.invited,
                  DealRequestStatus.requested,
                  DealRequestStatus.denied,
                  DealRequestStatus.cancelled,
                  DealRequestStatus.delinquent,
                ],
              ),
            );
          },
          settings: AppAnalytics.instance
              .getRouteSettings('requests/completed/bypublisher'),
        ),
      );

  @override
  Widget build(BuildContext context) {
    /// creator work history is only available to 'team' workspaces
    return !appState.isBusinessPro
        ? Container(height: 0)
        : StreamBuilder<PublisherAccountStatsWithResponse>(
            stream: widget.bloc.accountStatsWithResponse,
            builder: (context, snapshot) {
              PublisherAccountStatsWithResponse res = snapshot.data;

              return snapshot.connectionState == ConnectionState.waiting
                  ? rydrListItem(
                      context: context,
                      icon: AppIcons.history,
                      title: 'Loading history...',
                    )
                  : res.error != null
                      ? _buildError(context)
                      : res.model.completedDealCount == 0
                          ? _buildNoHistory(context)
                          : StreamBuilder<bool>(
                              stream: widget.bloc.showWorkHistory,
                              builder: (context, snapshot) {
                                return snapshot.data != null &&
                                        snapshot.data == true
                                    ? _buildSummary(context, res)
                                    : _buildSummary(context, res);
                              });
            },
          );
  }

  Widget _buildError(BuildContext context) => Column(
        children: <Widget>[
          rydrListItem(
            context: context,
            icon: AppIcons.history,
            title: 'Creator history',
            subtitle: "Unable to retrieve working history...",
            subtitleIsHint: true,
            lastInList: true,
          ),
          sectionDivider(context),
        ],
      );

  Widget _buildNoHistory(BuildContext context) => Column(
        children: <Widget>[
          Divider(height: 1),
          Padding(
            padding: EdgeInsets.symmetric(
              horizontal: 16,
              vertical: 12,
            ),
            child: Text(
              "You have never worked with ${widget.profile.userName}",
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(
                      color: Theme.of(context).hintColor,
                    ),
                  ),
            ),
          ),
        ],
      );

  Widget _buildStat(BuildContext context, String title, String value) =>
      Padding(
        padding: EdgeInsets.only(right: 16.0, bottom: 8.0, top: 4.0),
        child: Column(
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.bodyText2,
                  ),
                ),
                Text(value, style: Theme.of(context).textTheme.bodyText2)
              ],
            ),
          ],
        ),
      );

  Widget _buildSummary(
    BuildContext context,
    PublisherAccountStatsWithResponse res,
  ) {
    final NumberFormat f = NumberFormat.decimalPattern();

    return GestureDetector(
      onTap: () => _goToRequests(context),
      child: Container(
        color: Colors.transparent,
        child: Column(
          children: <Widget>[
            sectionDivider(context),
            SizedBox(
              height: 20.0,
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.start,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Stack(
                  alignment: Alignment.center,
                  children: <Widget>[
                    Container(
                      width: 72,
                      height: 40,
                    ),
                    Icon(
                      AppIcons.history,
                      color: Theme.of(context).brightness == Brightness.dark
                          ? Theme.of(context).appBarTheme.iconTheme.color
                          : Theme.of(context).iconTheme.color,
                    )
                  ],
                ),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      SizedBox(
                        height: 4.0,
                      ),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text('History with ${widget.profile.userName}',
                              style: Theme.of(context).textTheme.bodyText1),
                          Padding(
                            padding: EdgeInsets.only(top: 3.5, right: 16.0),
                            child: Text(
                              "View all",
                              style: Theme.of(context).textTheme.caption.merge(
                                    TextStyle(
                                      fontWeight: FontWeight.w500,
                                      height: 1.0,
                                      color: Theme.of(context).primaryColor,
                                    ),
                                  ),
                            ),
                          ),
                        ],
                      ),
                      SizedBox(
                        height: 6.0,
                      ),
                      _buildStat(
                        context,
                        "Completed RYDRs",
                        res.model.completedDealCount.toString(),
                      ),
                      _buildStat(
                        context,
                        "Total Media Posted",
                        res.model.completionMediaCount.toString(),
                      ),
                      _buildStat(
                        context,
                        "Total Impressions",
                        f.format(res.model.stats?.impressions ?? 0),
                      ),
                    ],
                  ),
                )
              ],
            ),
            SizedBox(height: 16.0),
          ],
        ),
      ),
    );
  }
}
