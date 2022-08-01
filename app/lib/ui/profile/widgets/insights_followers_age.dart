import 'package:flutter/material.dart';
import 'package:fl_chart/fl_chart.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/ui/profile/blocs/insights_followers.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';

class ProfileInsightsAgeAndGender extends StatelessWidget {
  final InsightsFollowerBloc bloc;

  ProfileInsightsAgeAndGender(this.bloc);

  @override
  Widget build(BuildContext context) {
    final bool isMe = appState.currentProfile.id == bloc.profile.id;
    final String profileUsername = bloc.profile.userName;

    return Column(
      children: <Widget>[
        /// header of the data / graph widget will show always,
        /// e.g. while loading, after load and regardles of success or error
        _buildHeader(context, isMe, profileUsername),

        StreamBuilder<InsightsFollowersAgeData>(
          stream: bloc.dataAgeGender,
          builder: (context, snapshot) {
            return snapshot.connectionState == ConnectionState.waiting
                ? insightsLoadingBody()
                : snapshot.data.ageAndGenderResponse.error != null
                    ? insightsErrorBody(
                        snapshot.data.ageAndGenderResponse.error, () {
                        bloc.loadAgeGender(true);
                      })
                    : !snapshot.data.ageAndGenderResponseWithData.hasResults
                        ? insightsNoResults(context,
                            bloc.strings.followerAgeGenderNoResults, null)
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
        icon: AppIcons.chartBar,
        title: bloc.strings.followAgeGenderTitle,
        subtitle: bloc.strings.followAgeGenerSubtitle,
      );

  Widget _buildResultsBody(
    BuildContext context,
    InsightsFollowersAgeData data,
  ) {
    final NumberFormat f = NumberFormat.decimalPattern();
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: EdgeInsets.only(left: 16.0, top: 48.0, right: 16.0),
          child: Row(
            children: <Widget>[
              Expanded(
                child: insightsBigStat(
                  context: context,
                  formatAsInt: true,
                  countColor: Theme.of(context).primaryColor,
                  value: data.ageAndGenderResponseWithData.totalFollowersMale
                      .toDouble(),
                  label: "Total Males",
                ),
              ),
              Expanded(
                child: insightsBigStat(
                  context: context,
                  formatAsInt: true,
                  countColor: AppColors.teal,
                  value: data.ageAndGenderResponseWithData.totalFollowersFemale
                      .toDouble(),
                  label: "Total Females",
                ),
              ),
            ],
          ),
        ),
        Padding(
          padding:
              EdgeInsets.only(top: 40.0, bottom: 16.0, left: 8.0, right: 8.0),
          child: Container(
            alignment: Alignment.bottomCenter,
            child: AspectRatio(
              aspectRatio: 1.8,
              child: BarChart(BarChartData(
                barTouchData: BarTouchData(
                  touchTooltipData: BarTouchTooltipData(
                    getTooltipItem: (barGroupData, x1, barRodData, x2) {
                      return BarTooltipItem(
                        f.format(barRodData.y),
                        TextStyle(
                          color: Theme.of(context).primaryColor,
                          fontWeight: FontWeight.w500,
                        ),
                      );
                    },
                    tooltipRoundedRadius: 4.0,
                    tooltipBottomMargin: 8.0,
                    tooltipPadding: EdgeInsets.only(
                        top: 8.0, bottom: 4.0, left: 8.0, right: 8.0),
                    tooltipBgColor: dark
                        ? AppColors.grey800.withOpacity(0.8)
                        : Colors.white.withOpacity(0.85),
                  ),
                ),
                alignment: BarChartAlignment.spaceAround,
                maxY: data.ageAndGenderResponseWithData.topAmount,
                titlesData: FlTitlesData(
                  show: true,
                  bottomTitles: SideTitles(
                    showTitles: true,
                    textStyle:
                        TextStyle(color: AppColors.grey300, fontSize: 11.0),
                    margin: 16,
                    getTitles: (double value) {
                      switch (value.toInt()) {
                        case 0:
                          return '13-17';
                        case 1:
                          return '18-24';
                        case 2:
                          return '25-34';
                        case 3:
                          return '35-44';
                        case 4:
                          return '45-54';
                        case 5:
                          return '55-64';
                        case 6:
                          return '65+';
                      }
                      return '';
                    },
                  ),
                  leftTitles: SideTitles(showTitles: false),
                  rightTitles: SideTitles(showTitles: false),
                ),
                borderData: FlBorderData(show: false),
                barGroups: data.ageGroups,
              )),
            ),
          ),
        ),
        ListTileTheme(
          textColor: Theme.of(context).textTheme.bodyText2.color,
          child: Padding(
            padding: EdgeInsets.symmetric(horizontal: 16.0),
            child: Column(
              children: <Widget>[
                Visibility(
                  visible:
                      data.ageAndGenderResponseWithData.totalFollowersMale > 0,
                  child: ListTile(
                    contentPadding: EdgeInsets.symmetric(horizontal: 0),
                    dense: true,
                    title: Row(
                      children: <Widget>[
                        Container(
                          height: 8.0,
                          width: 8.0,
                          margin: EdgeInsets.only(right: 8.0),
                          decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(16.0),
                              color: Theme.of(context).primaryColor),
                        ),
                        Expanded(
                          child: Text(
                            bloc.strings.totalMale,
                            style: TextStyle(fontSize: 14.0),
                          ),
                        ),
                      ],
                    ),
                    trailing: Container(
                      width: 120.0,
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: <Widget>[
                          Text(
                              '${data.ageAndGenderResponseWithData.totalFollowersMalePercent}%'),
                        ],
                      ),
                    ),
                  ),
                ),
                Visibility(
                  visible:
                      data.ageAndGenderResponseWithData.totalFollowersFemale >
                          0,
                  child: ListTile(
                    contentPadding: EdgeInsets.symmetric(horizontal: 0),
                    dense: true,
                    title: Row(
                      children: <Widget>[
                        Container(
                          height: 8.0,
                          width: 8.0,
                          margin: EdgeInsets.only(right: 8.0),
                          decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(16.0),
                              color: AppColors.teal),
                        ),
                        Expanded(
                          child: Text(
                            bloc.strings.totalFemale,
                            style: TextStyle(fontSize: 14.0),
                          ),
                        ),
                      ],
                    ),
                    trailing: Container(
                      width: 120.0,
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: <Widget>[
                          Text(
                              '${data.ageAndGenderResponseWithData.totalFollowersFemalePercent}%'),
                        ],
                      ),
                    ),
                  ),
                ),
                Visibility(
                  visible:
                      data.ageAndGenderResponseWithData.totalFollowersUnknown >
                          0,
                  child: ListTile(
                    contentPadding: EdgeInsets.symmetric(horizontal: 0),
                    dense: true,
                    title: Row(
                      children: <Widget>[
                        Container(
                          height: 8.0,
                          width: 8.0,
                          margin: EdgeInsets.only(right: 8.0),
                          decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(16.0),
                              color: AppColors.grey300),
                        ),
                        Expanded(
                          child: Text(
                            bloc.strings.totalUnknown,
                            style: TextStyle(fontSize: 14.0),
                          ),
                        ),
                      ],
                    ),
                    trailing: Container(
                      width: 120.0,
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: <Widget>[
                          Text(
                              '${data.ageAndGenderResponseWithData.totalFollowersUnknownPercent}%'),
                        ],
                      ),
                    ),
                  ),
                ),
                SizedBox(height: 16.0)
              ],
            ),
          ),
        )
      ],
    );
  }
}
