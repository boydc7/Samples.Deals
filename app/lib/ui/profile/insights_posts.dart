import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/app/strings.dart';
import 'package:rydr_app/ui/profile/blocs/insights_posts.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/ui/profile/widgets/engagement_calculator.dart';
import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class ProfileInsightsPosts extends StatefulWidget {
  final PublisherAccount profile;
  ProfileInsightsPosts(this.profile);

  @override
  _ProfileInsightsPostsState createState() => _ProfileInsightsPostsState();
}

class _ProfileInsightsPostsState extends State<ProfileInsightsPosts> {
  final InsightsPostsBloc _bloc = InsightsPostsBloc();
  AppStrings _strings;

  @override
  void initState() {
    super.initState();

    _strings = AppStrings(widget.profile);

    _load();
  }

  @override
  void dispose() {
    _bloc.dispose();

    super.dispose();
  }

  Future<void> _load([bool forceRefresh = false]) =>
      _bloc.loadData(widget.profile, forceRefresh);

  void _launchMedia(String url) {
    if (appState.currentProfile.id == widget.profile.id) {
      Utils.launchUrl(context, url, trackingName: 'media');
    }
  }

  @override
  Widget build(BuildContext context) {
    final bool isMe = appState.currentProfile.id == widget.profile.id;
    final String profileUsername = widget.profile.userName;

    return Scaffold(
      appBar: AppBar(
        leading: AppBarBackButton(context),
        title: Text("Post Insights"),
      ),
      body: StreamBuilder<InsightsPostsBlocResponse>(
        stream: _bloc.data,
        builder: (context, snapshot) {
          return snapshot.connectionState == ConnectionState.waiting
              ? insightsLoadingBody()
              : snapshot.data.error != null
                  ? RetryError(
                      error: snapshot.data.error,
                      fullSize: true,
                      onRetry: _load,
                    )
                  : !snapshot.data.hasResults
                      ? insightsNoResults(
                          context,
                          widget.profile.lastSyncedOnDisplay == null
                              ? _strings.postInsightStillSyncing
                              : _strings.postInsightNoResults,
                          widget.profile.lastSyncedOnDisplay == null
                              ? AppIcons.sync
                              : AppIcons.imagesAlt)
                      : _buildResultsBody(snapshot.data, isMe, profileUsername);
        },
      ),
    );
  }

  Widget _buildResultsBody(
      InsightsPostsBlocResponse response, bool isMe, String profileUsername) {
    final bool isOwner = appState.currentProfile.id == widget.profile.id;
    final mediaSummary = response.mediaSummary;
    final int mediaLength = response.mediaCurrent.length == 6
        ? 5
        : response.mediaCurrent.length == 11
            ? 10
            : response.mediaCurrent.length;
    final firstPoint = response.mediaCurrent.first.mediaCreatedOn;
    final lastPoint = response.mediaCurrent.last.mediaCreatedOn;
    final difference = lastPoint.difference(firstPoint).inDays;
    final ending = difference == 0
        ? "within 24 hours"
        : difference == 1 ? "over 1 day" : "over $difference days";

    final double average = difference > 0 ? mediaLength / difference : 0;
    final double weeklyAverage = average * 7;

    return RefreshIndicator(
        displacement: 0.0,
        backgroundColor: Theme.of(context).appBarTheme.color,
        color: Theme.of(context).textTheme.bodyText2.color,
        onRefresh: () => _load(true),
        child: ListView(
          children: <Widget>[
            Column(
              children: <Widget>[
                _buildImpReachHeader(),
                Padding(
                  padding: EdgeInsets.only(left: 16.0, top: 48.0, right: 16.0),
                  child: Row(
                    children: <Widget>[
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: mediaSummary.avgImpressions.toDouble(),
                          formatAsInt: true,
                          countColor: Theme.of(context).primaryColor,
                          label: _strings.averageImpressionsPost,
                        ),
                      ),
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: mediaSummary.avgReach.toDouble(),
                          formatAsInt: true,
                          countColor: AppColors.teal,
                          label: _strings.averageReachPost,
                        ),
                      ),
                    ],
                  ),
                ),
                ProfileInsightsChart(
                  dates: response.dates,
                  data: response.chartData,
                ),
                _buildGroupingLinks(response.showIndex),
                Text(
                  "${mediaLength.toString()} posts $ending" +
                      ((difference == 1 || difference == 0)
                          ? ""
                          : " - ${weeklyAverage.toStringAsFixed(1)} posts per week"),
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(color: Theme.of(context).hintColor),
                      ),
                ),
                SizedBox(height: 16.0),
                Divider(height: 1, indent: 16.0, endIndent: 16.0),
              ],
            ),
            ListTileTheme(
              textColor: Theme.of(context).textTheme.bodyText2.color,
              child: Container(
                padding: EdgeInsets.symmetric(horizontal: 16.0),
                child: Column(
                  children: <Widget>[
                    SizedBox(height: 4),
                    ListTile(
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
                              _strings.totalImpressions,
                              style: TextStyle(fontSize: 14.0),
                            ),
                          )
                        ],
                      ),
                      trailing: Text(mediaSummary.totalImpressionsDisplay),
                    ),
                    insightsStatTileDense(
                      showArrow: isOwner,
                      onTap: () =>
                          _launchMedia(mediaSummary.maxImpressions.url),
                      context: context,
                      title:
                          'High · ${mediaSummary.maxImpressions.dayTimeDisplay}',
                      value: mediaSummary.maxImpressions.total.toDouble(),
                      formatAsInt: true,
                    ),
                    insightsStatTileDense(
                      showArrow: isOwner,
                      onTap: () =>
                          _launchMedia(mediaSummary.minImpressions.url),
                      context: context,
                      title:
                          'Low · ${mediaSummary.minImpressions.dayTimeDisplay}',
                      value: mediaSummary.minImpressions.total.toDouble() ?? 0,
                      formatAsInt: true,
                    ),
                    SizedBox(height: 4),
                    Divider(height: 1),
                    SizedBox(height: 4),
                    ListTile(
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
                            _strings.totalReach,
                            style: TextStyle(fontSize: 14.0),
                          ))
                        ],
                      ),
                      trailing: Text(
                        mediaSummary.totalReachDisplay,
                      ),
                    ),
                    insightsStatTileDense(
                      showArrow: isOwner,
                      onTap: () => _launchMedia(mediaSummary.maxReach.url),
                      context: context,
                      title: 'High · ${mediaSummary.maxReach.dayTimeDisplay}',
                      value: mediaSummary.maxReach.total.toDouble(),
                      formatAsInt: true,
                    ),
                    insightsStatTileDense(
                      showArrow: isOwner,
                      onTap: () => _launchMedia(mediaSummary.minReach.url),
                      context: context,
                      title: 'Low · ${mediaSummary.minReach.dayTimeDisplay}',
                      value: mediaSummary.minReach.total.toDouble(),
                      formatAsInt: true,
                    ),
                    SizedBox(
                      height: 12,
                    ),
                  ],
                ),
              ),
            ),
            sectionDivider(context),
            Column(
              children: <Widget>[
                _buildEngagementHeader(),
                Padding(
                  padding: EdgeInsets.only(top: 40.0, left: 16, right: 16),
                  child: Row(
                    children: <Widget>[
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: mediaSummary.engagementRateTrue,
                          formatAsPercentage: true,
                          label: _strings.postTrueEngagement,
                          subtitleWidget: EngagementCalculator(
                            followerCount: response.followedBy,
                            engagementRate: mediaSummary.engagementRateTrue,
                          ),
                        ),
                      ),
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: mediaSummary.engagementRate,
                          formatAsPercentage: true,
                          label: _strings.postEngagement,
                          subtitleWidget: EngagementCalculator(
                            followerCount: response.followedBy,
                            engagementRate: mediaSummary.engagementRate,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                mediaSummary.flSpotsEngagements.length > 0
                    ? ProfileInsightsChart(
                        dates: response.dates,
                        data: [
                          ChartData(
                            isPercent: true,
                            data: mediaSummary.flSpotsEngagements,
                            dataColor: chartDataColor.blue,
                            maxY: mediaSummary.maxEngagement.total,
                            minY: mediaSummary.minEngagement.total,
                          ),
                        ],
                      )
                    : Container(),
                _buildGroupingLinks(response.showIndex),
                SizedBox(
                  height: 16.0,
                ),
                Divider(
                  height: 1,
                  indent: 16.0,
                  endIndent: 16.0,
                ),
              ],
            ),
            ListTileTheme(
              textColor: Theme.of(context).textTheme.bodyText2.color,
              child: Container(
                padding: EdgeInsets.symmetric(horizontal: 16.0),
                child: Column(
                  children: <Widget>[
                    SizedBox(
                      height: 8.0,
                    ),
                    ListTile(
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
                            _strings.postEngagementPerPost,
                            style: TextStyle(fontSize: 14.0),
                          ))
                        ],
                      ),
                    ),
                    insightsStatTileDense(
                      showArrow: isOwner,
                      onTap: () => _launchMedia(mediaSummary.maxEngagement.url),
                      context: context,
                      title:
                          'High · ${mediaSummary.maxEngagement.dayTimeDisplay}',
                      value: mediaSummary.maxEngagement.total?.toDouble(),
                      formatAsPercentage: true,
                    ),
                    insightsStatTileDense(
                      showArrow: isOwner,
                      onTap: () => _launchMedia(mediaSummary.minEngagement.url),
                      context: context,
                      title:
                          'Low · ${mediaSummary.minEngagement.dayTimeDisplay}',
                      value: mediaSummary.minEngagement.total?.toDouble(),
                      formatAsPercentage: true,
                    ),
                    SizedBox(height: 4),
                    Divider(height: 1),
                    SizedBox(height: 4),
                    ListTile(
                      contentPadding: EdgeInsets.symmetric(horizontal: 0),
                      dense: true,
                      title: Text(
                        _strings.postAvgLikes,
                        style: TextStyle(fontSize: 14.0),
                      ),
                      trailing: mediaSummary.avgLikes.toString().endsWith('0')
                          ? Text(mediaSummary.avgLikes.toInt().toString())
                          : Text(mediaSummary.avgLikes.toStringAsFixed(1)),
                    ),
                    Divider(height: 1),
                    ListTile(
                      contentPadding: EdgeInsets.symmetric(horizontal: 0),
                      dense: true,
                      title: Text(
                        _strings.postAvgComments,
                        style: TextStyle(fontSize: 14.0),
                      ),
                      trailing: mediaSummary.avgComments
                              .toString()
                              .endsWith('0')
                          ? Text(mediaSummary.avgComments.toInt().toString())
                          : Text(mediaSummary.avgComments.toStringAsFixed(1)),
                    ),
                    Divider(height: 1),
                    ListTile(
                      contentPadding: EdgeInsets.symmetric(horizontal: 0),
                      dense: true,
                      title: Text(
                        _strings.postAvgSaves,
                        style: TextStyle(fontSize: 14.0),
                      ),
                      trailing: mediaSummary.avgSaves.toString().endsWith('0')
                          ? Text(mediaSummary.avgSaves.toInt().toString())
                          : Text(mediaSummary.avgSaves.toStringAsFixed(1)),
                    ),
                    SizedBox(
                      height: 16.0,
                    )
                  ],
                ),
              ),
            ),
          ],
        ));
  }

  Widget _buildImpReachHeader() {
    return insightsSectionHeader(
      context: context,
      icon: AppIcons.chartLine,
      title: _strings.discovery,
      subtitle: _strings.postDiscoverySubtitle,
      bottomSheetTitle: _strings.postImpReachSheetTitle,
      bottomSheetSubtitle: _strings.postDiscoverySheetSubtitle,
      bottomSheetWidget: insightsBottomSheet(context, [
        InsightsBottomSheetTile(
          _strings.impressions,
          _strings.postImpressionsDescription,
        ),
        InsightsBottomSheetTile(
          _strings.reach,
          _strings.postReachDescription,
        ),
        InsightsBottomSheetTile(
          _strings.totalReach,
          _strings.postTotalReachDescription,
        ),
        InsightsBottomSheetTile(
          _strings.averageReach,
          _strings.postAvgReachDescription,
        ),
      ]),
      initialRatio: 0.54,
    );
  }

  Widget _buildEngagementHeader() {
    return insightsSectionHeader(
        context: context,
        icon: AppIcons.chartLine,
        title: _strings.engagement,
        subtitle: _strings.postEngagementSubtitle,
        bottomSheetTitle: _strings.postEngagementSheetTitle,
        bottomSheetSubtitle: _strings.postEngagementSubtitle,
        bottomSheetWidget: insightsBottomSheet(context, [
          InsightsBottomSheetTile(
              _strings.engagement, _strings.postEngagementShortDescription),
          InsightsBottomSheetTile(
            _strings.postEngagement,
            _strings.postEngagementDescription,
          ),
          InsightsBottomSheetTile(
            _strings.postTrueEngagement,
            _strings.postTrueEngagementDescription,
          ),
        ]),
        initialRatio: 0.55);
  }

  Widget _buildGroupingLinks(int index) {
    if (_bloc.lastPostsIndex2 != '') {
      return insightsToggleButtons(
        context,
        [
          _bloc.lastPostsIndex0,
          _bloc.lastPostsIndex1,
          _bloc.lastPostsIndex2,
        ],
        index,
        _bloc.toggleShowIndex,
      );
    } else if (_bloc.lastPostsIndex1 != '') {
      return insightsToggleButtons(
        context,
        [
          _bloc.lastPostsIndex0,
          _bloc.lastPostsIndex1,
        ],
        index,
        _bloc.toggleShowIndex,
      );
    } else {
      return insightsToggleButtons(
        context,
        [
          _bloc.lastPostsIndex0,
        ],
        index,
        _bloc.toggleShowIndex,
      );
    }
  }
}
