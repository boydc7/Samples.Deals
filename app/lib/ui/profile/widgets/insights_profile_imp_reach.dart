import 'package:flutter/material.dart';
import 'package:rydr_app/app/strings.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/enums/publisher_insights.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/ui/profile/blocs/insights_profile.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';

import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';

class ProfileInsightsProfileImpReach extends StatefulWidget {
  final bool loading;
  final PublisherAccount profile;
  final PublisherInsightsGrowthResponse growthResponse;
  final InsightsProfileBloc bloc;

  ProfileInsightsProfileImpReach(
      {@required this.loading,
      @required this.profile,
      @required this.growthResponse,
      @required this.bloc});

  @override
  _ProfileInsightsProfileImpReachState createState() =>
      _ProfileInsightsProfileImpReachState();
}

class _ProfileInsightsProfileImpReachState
    extends State<ProfileInsightsProfileImpReach> {
  AppStrings _strings;
  @override
  initState() {
    super.initState();

    _strings = AppStrings(widget.profile);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        /// header of the data / graph widget will show always,
        /// e.g. while loading, after load and regardles of success or error
        _buildHeader(),

        /// body of the widget will show different states depending on loading (shimmer)
        /// completed with error (retry widget), or success (actual data/graph)
        widget.loading
            ? insightsLoadingBody()
            : widget.growthResponse.models == null
                ? insightsNoResults(
                    context, _strings.profileImpReachNoResults, null)
                : StreamBuilder<int>(
                    stream: widget.bloc.showIndex,
                    builder: (context, snapshot) {
                      return _buildResultsBody(snapshot.data ?? 0);
                    }),
      ],
    );
  }

  /// header of widget which includes title, subtitle, and bottom sheet
  /// this will be rendered while and after loading is completed
  Widget _buildHeader() {
    return insightsSectionHeader(
      context: context,
      icon: AppIcons.chartLine,
      title: _strings.profileImpReachTitle,
      subtitle: _strings.profileImpReachSubtitle,
      bottomSheetTitle: _strings.profileImpReachSheetTitle,
      bottomSheetSubtitle: _strings.profileImpReachSubtitle,
      bottomSheetWidget: insightsBottomSheet(
        context,
        [
          InsightsBottomSheetTile(
            _strings.totalImpressions,
            _strings.profileImpressionsDescription,
          ),
          InsightsBottomSheetTile(
            _strings.totalReach,
            _strings.profileReachDescription,
          ),
        ],
      ),
      initialRatio: 0.38,
    );
  }

  Widget _buildResultsBody(int showIndex) {
    final PublisherInsightsGrowthSummary summaryImp =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.Impressions,
      showIndex == 0 ? 7 : 30,
    );

    final PublisherInsightsGrowthSummary summaryReach =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.Reach,
      showIndex == 0 ? 7 : 30,
    );

    final List<ChartData> data = [
      ChartData(
        dataColor: chartDataColor.blue,
        data: summaryImp.flSpots,
        maxY: summaryImp.max.total,
        minY: summaryImp.min.total,
      ),
      ChartData(
        dataColor: chartDataColor.teal,
        data: summaryReach.flSpots,
        maxY: summaryReach.max.total,
        minY: summaryReach.min.total,
      ),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        summaryImp.max.total > 0 || summaryReach.max.total > 0
            ? Container(
                padding: EdgeInsets.only(
                    left: 16.0, top: 48.0, right: 16.0, bottom: 20.0),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Expanded(
                      child: insightsBigStat(
                        context: context,
                        formatAsInt: true,
                        value: summaryImp.avg,
                        label: _strings.averageImpressions,
                        countColor: Theme.of(context).primaryColor,
                      ),
                    ),
                    Expanded(
                      child: insightsBigStat(
                        context: context,
                        formatAsInt: true,
                        value: summaryReach.avg,
                        label: _strings.averageReach,
                        countColor: AppColors.teal,
                      ),
                    ),
                  ],
                ),
              )
            : Container(),
        summaryImp.max.total > 0 || summaryReach.max.total > 0
            ? ProfileInsightsChart(
                dates: summaryImp.dates,
                data: data,
              )
            : insightsNoGraph(context),
        insightsToggleButtons(
          context,
          [_strings.sevenDays, _strings.thirtyDays],
          showIndex,
          widget.bloc.setShowIndex,
        ),
        SizedBox(height: 12),
        Divider(height: 1, indent: 16.0, endIndent: 16.0),
        SizedBox(height: 4),
        insightsGrowthMinMax(
          context,
          summary: summaryImp,
          title: _strings.totalImpressions,
          color: Theme.of(context).primaryColor,
        ),
        Divider(height: 1),
        SizedBox(height: 4),
        insightsGrowthMinMax(
          context,
          summary: summaryReach,
          title: _strings.totalReach,
          color: AppColors.teal,
        ),
        SizedBox(height: 4),
      ],
    );
  }
}
