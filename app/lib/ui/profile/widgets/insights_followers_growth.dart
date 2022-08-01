import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/ui/profile/blocs/insights_followers.dart';
import 'package:shimmer/shimmer.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';
import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';

class ProfileInsightsFollowersGrowth extends StatelessWidget {
  final InsightsFollowerBloc bloc;

  final NumberFormat f = NumberFormat.decimalPattern();

  final Map<String, String> _pageContent = {
    'mass_follower': 'Very Below Average',
    'popular': 'Above Average',
    'low_popularity': 'Below Average',
    'average': 'Average',
    'influencer_status': 'Very good!',
  };

  ProfileInsightsFollowersGrowth(this.bloc);

  @override
  Widget build(BuildContext context) {
    final bool isMe = appState.currentProfile.id == bloc.profile.id;
    final String profileUsername = bloc.profile.userName;

    return Column(
      children: <Widget>[
        /// header of the data / graph widget will show always,
        /// e.g. while loading, after load and regardles of success or error
        _buildHeader(context, isMe, profileUsername),

        StreamBuilder<InsightsFollowersData>(
          stream: bloc.dataGrowth,
          builder: (context, snapshot) {
            return snapshot.connectionState == ConnectionState.waiting
                ? insightsLoadingBody()
                : snapshot.data.growthResponse.error != null
                    ? insightsErrorBody(snapshot.data.growthResponse.error, () {
                        bloc.loadGrowth(true);
                      })
                    : !snapshot.data.growthResponseWithData.hasResults
                        ? insightsNoResults(
                            context, bloc.strings.followerGrowthNoResults, null)
                        : _buildResultsBody(context, snapshot.data);
          },
        ),
      ],
    );
  }

  /// header of widget which includes title and subtitle
  /// we'll re-use this to render the lo
  Widget _buildHeader(
          BuildContext context, bool isMe, String profileUsername) =>
      insightsSectionHeader(
          context: context,
          icon: AppIcons.chartLine,
          title: bloc.strings.followGrowthTitle,
          subtitle: bloc.strings.followGrowthSubtitle,
          bottomSheetTitle: bloc.strings.followGrowthSheetTitle,
          bottomSheetSubtitle: bloc.strings.followGrowthSheetSubtitle,
          bottomSheetWidget: insightsBottomSheet(context, [
            InsightsBottomSheetTile(
              bloc.strings.followGrowthRatio,
              bloc.strings.followGrowthRatioDescription,
            ),
            InsightsBottomSheetTile(
              bloc.strings.followGrowthRate,
              bloc.strings.followGrowthRateDescription,
            ),
          ]),
          initialRatio: 0.43);

  Widget _buildResultsBody(BuildContext context, InsightsFollowersData res) {
    final int index = res.index;
    final double followerRatio = res.growthResponseWithData.followerRatio;
    final PublisherInsightsGrowthSummary summary = res.summary;
    final List<ChartData> data = res.data;

    final Color color = followerRatio <= 0.5
        ? AppColors.grey400
        : followerRatio > 0.5 && followerRatio <= 1
            ? Colors.yellow.shade700
            : followerRatio > 1.0 && followerRatio <= 2.1
                ? AppColors.teal
                : followerRatio > 2.1 && followerRatio <= 10.0
                    ? AppColors.successGreen
                    : followerRatio > 10.0
                        ? Theme.of(context).appBarTheme.color
                        : AppColors.white;

    final String popularity = followerRatio <= 0.5
        ? _pageContent['mass_follower']
        : followerRatio > 0.5 && followerRatio <= 1
            ? _pageContent['low_popularity']
            : followerRatio > 1.0 && followerRatio <= 2.1
                ? _pageContent['average']
                : followerRatio > 2.1 && followerRatio <= 10.0
                    ? _pageContent['popular']
                    : followerRatio > 10.0
                        ? _pageContent['influencer_status']
                        : '';

    return summary == null
        ? Container()
        : ListTileTheme(
            textColor: Theme.of(context).textTheme.bodyText2.color,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: <Widget>[
                Container(
                  padding: EdgeInsets.only(left: 16.0, top: 48.0, right: 16.0),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: <Widget>[
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: followerRatio,
                          formatAsPercentage: true,
                          label: bloc.strings.followGrowthRatio,
                          subtitleWidget: Visibility(
                            visible: res.growthResponseWithData.followedBy !=
                                    0 &&
                                res.growthResponseWithData.followedBy <
                                    15000, //We only show good/bad/ugly ratings for users with less than 15k followers, once it gets more than that, it's too volitile to pin down (not enough data behind it)
                            child: Container(
                              margin: EdgeInsets.only(top: 4.0),
                              decoration: BoxDecoration(
                                borderRadius: BorderRadius.circular(8.0),
                                color: color,
                              ),
                              padding: EdgeInsets.symmetric(
                                  horizontal: 8.0, vertical: 4.0),
                              child: followerRatio > 10.0
                                  ? Shimmer.fromColors(
                                      baseColor: Colors.red,
                                      highlightColor: Colors.yellow,
                                      child: Text(
                                          _pageContent['influencer_status'],
                                          style: TextStyle(
                                              fontSize: 12.0,
                                              color: Theme.of(context)
                                                  .textTheme
                                                  .bodyText2
                                                  .color,
                                              height: 1.0,
                                              fontWeight: FontWeight.w500)),
                                    )
                                  : Text(
                                      popularity,
                                      style: TextStyle(
                                          fontSize: 12.0,
                                          color: Theme.of(context)
                                              .appBarTheme
                                              .color,
                                          height: 1.0,
                                          fontWeight: FontWeight.w500),
                                    ),
                            ),
                          ),
                        ),
                      ),
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          countColor: AppColors.grey800,
                          value: summary.growthRate,
                          formatAsPercentage: true,
                          label: bloc.strings.followGrowthRate,
                          subtitleWidget: Padding(
                            padding: EdgeInsets.only(top: 6.0),
                            child: Row(
                              mainAxisAlignment: MainAxisAlignment.start,
                              children: <Widget>[
                                Padding(
                                  padding:
                                      EdgeInsets.only(bottom: 2.0, right: 4.0),
                                  child: summary.diff == 0
                                      ? Container(width: 0, height: 16)
                                      : Container(
                                          height: 16,
                                          child: Center(
                                            child: Icon(
                                              summary.diff.isNegative
                                                  ? AppIcons.levelUpReg
                                                  : AppIcons.levelDownReg,
                                              size: 12.0,
                                              color: summary.diff.isNegative
                                                  ? AppColors.successGreen
                                                  : Colors.deepOrange,
                                            ),
                                          ),
                                        ),
                                ),
                                Text(
                                  summary.diff == 0
                                      ? "0 Followers"
                                      : summary.diff.abs() == 1
                                          ? "${summary.diff.abs().toString()} Follower"
                                          : "${summary.diff.abs().toString()} Followers",
                                  style:
                                      Theme.of(context).textTheme.caption.merge(
                                            TextStyle(
                                                color: summary.diff.isNegative
                                                    ? AppColors.successGreen
                                                    : Colors.deepOrange),
                                          ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                ProfileInsightsChart(
                  dates: summary.dates,
                  data: data,
                ),
                Visibility(
                  visible: res.growthResponseWithData.growth.length > 5,
                  child: insightsToggleButtons(
                    context,
                    [
                      bloc.strings.sevenDays,
                      bloc.strings.thirtyDays,
                    ],
                    index,
                    bloc.setShowIndexGrowth,
                  ),
                ),
                SizedBox(height: 12),
                Divider(height: 1, indent: 16.0, endIndent: 16.0),
                SizedBox(
                  height: 4,
                ),
                ListTile(
                  contentPadding: EdgeInsets.symmetric(horizontal: 16.0),
                  dense: true,
                  title: Text(
                    bloc.strings.followGrowthTotalFollowers,
                    style: TextStyle(fontSize: 14.0),
                  ),
                  trailing:
                      Text(f.format(res.growthResponseWithData.followedBy)),
                ),
                Padding(
                  padding: EdgeInsets.only(right: 16.0),
                  child: insightsStatTileDense(
                    context: context,
                    title: "High",
                    value: summary.max.total.toDouble(),
                    formatAsInt: true,
                  ),
                ),
                Padding(
                  padding: EdgeInsets.only(right: 16.0),
                  child: insightsStatTileDense(
                    context: context,
                    title: "Low",
                    value: summary.min.total.toDouble(),
                    formatAsInt: true,
                  ),
                ),
                SizedBox(height: 4),
                Divider(height: 1),
                SizedBox(height: 4),
                ListTile(
                  contentPadding: EdgeInsets.symmetric(horizontal: 16.0),
                  dense: true,
                  title: Text(
                    bloc.strings.followGrowthTotalFollowing,
                    style: TextStyle(fontSize: 14.0),
                  ),
                  trailing: Text(f.format(res.growthResponseWithData.follows)),
                ),
                SizedBox(
                  height: 8.0,
                ),
              ],
            ),
          );
  }
}
