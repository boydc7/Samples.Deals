import 'package:flutter/material.dart';
import 'package:rydr_app/app/strings.dart';
import 'package:rydr_app/models/enums/publisher_insights.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/ui/profile/blocs/insights_profile.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/icons.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/publisher_insights_growth.dart';
import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';

class ProfileInsightsProfileViews extends StatefulWidget {
  final bool loading;
  final PublisherAccount profile;
  final PublisherInsightsGrowthResponse growthResponse;
  final InsightsProfileBloc bloc;

  ProfileInsightsProfileViews({
    @required this.loading,
    @required this.profile,
    @required this.growthResponse,
    @required this.bloc,
  });

  @override
  _ProfileInsightsProfileViewsState createState() =>
      _ProfileInsightsProfileViewsState();
}

class _ProfileInsightsProfileViewsState
    extends State<ProfileInsightsProfileViews> {
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
                    context,
                    widget.profile.lastSyncedOnDisplay == null
                        ? _strings.profileProfileViewsNoResults
                        : _strings.profileProfileVStillSyncing,
                    widget.profile.lastSyncedOnDisplay == null
                        ? AppIcons.sync
                        : AppIcons.chartLine)
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
        title: _strings.profileInteractionsViewsTitle,
        subtitle: _strings.profileInteractionsSubtitle,
        bottomSheetTitle: _strings.profileInteractionsViewsSheetTitle,
        bottomSheetSubtitle: _strings.profileInteractionsSubtitle,
        bottomSheetWidget: insightsBottomSheet(
          context,
          [
            InsightsBottomSheetTile(
              _strings.profileViews,
              _strings.profileInteractionsViewsDescription,
            ),
          ],
        ),
        initialRatio: 0.3);
  }

  Widget _buildResultsBody(int showIndex) {
    final PublisherInsightsGrowthSummary summary =
        PublisherInsightsGrowthSummary(
      widget.growthResponse.models,
      ProfileGrowthType.ProfileViews,
      showIndex == 0 ? 7 : widget.growthResponse.models.length,
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        summary.max.total > 0
            ? ProfileInsightsBarChart(summary)
            : insightsNoGraph(context),
        insightsToggleButtons(
          context,
          [
            _strings.sevenDays,
            _strings.thirtyDays,
          ],
          showIndex,
          widget.bloc.setShowIndex,
        ),
        SizedBox(height: 12),
        Divider(
          height: 1,
          indent: 16.0,
          endIndent: 16.0,
        ),
        SizedBox(height: 4),
        insightsGrowthMinMax(
          context,
          summary: summary,
          title: _strings.totalProfileViews,
          color: Theme.of(context).primaryColor,
        ),
      ],
    );
  }
}
