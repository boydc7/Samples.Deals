import 'dart:async';
import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/app/strings.dart';
import 'package:rydr_app/ui/profile/blocs/insights_stories.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';

import 'package:rydr_app/models/publisher_account.dart';

import 'package:rydr_app/ui/profile/widgets/engagement_calculator.dart';
import 'package:rydr_app/ui/profile/widgets/insights_chart.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class ProfileInsightsStory extends StatefulWidget {
  final PublisherAccount profile;
  ProfileInsightsStory(this.profile);

  @override
  _ProfileInsightsStoryState createState() => _ProfileInsightsStoryState();
}

class _ProfileInsightsStoryState extends State<ProfileInsightsStory> {
  final _bloc = InsightsStoriesBloc();
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
        title: Text("Story Insights"),
      ),
      body: StreamBuilder<InsightsStoriesBlocResponse>(
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
                              ? _strings.storyInsightStillSyncing
                              : _strings.storyInsightNoResults,
                          widget.profile.lastSyncedOnDisplay == null
                              ? AppIcons.sync
                              : AppIcons.portrait)
                      : _buildResultsBody(snapshot.data, isMe, profileUsername);
        },
      ),
    );
  }

  Widget _buildResultsBody(
    InsightsStoriesBlocResponse response,
    bool isMe,
    String profileUsername,
  ) {
    final bool isOwner = appState.currentProfile.id == widget.profile.id;
    final mediaSummary = response.mediaSummary;
    final double storyEngagmentRate =
        widget.profile.publisherMetrics.storyEngagementRating;
    final double storyEngagmentRateDbl =
        widget.profile.publisherMetrics.storyEngagementRating;

    return RefreshIndicator(
        displacement: 0.0,
        backgroundColor: Theme.of(context).appBarTheme.color,
        color: Theme.of(context).textTheme.bodyText2.color,
        onRefresh: () => _load(true),
        child: ListView(
          children: <Widget>[
            Column(
              mainAxisAlignment: MainAxisAlignment.start,
              children: <Widget>[
                _buildImpReachHeader(isMe, profileUsername),
                Padding(
                  padding: EdgeInsets.only(top: 40.0, left: 16, right: 16),
                  child: Row(
                    children: <Widget>[
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: mediaSummary.avgImpressions.toDouble(),
                          formatAsInt: true,
                          countColor: Theme.of(context).primaryColor,
                          label: _strings.averageImpressionsStory,
                        ),
                      ),
                      Expanded(
                        child: insightsBigStat(
                          context: context,
                          value: mediaSummary.avgReach.toDouble(),
                          formatAsInt: true,
                          countColor: AppColors.teal,
                          label: _strings.averageReachStory,
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
                  widget.profile.publisherMetrics
                      .avgStoriesPerDay(response.mediaCurrent),
                  style: Theme.of(context).textTheme.caption.merge(
                        TextStyle(color: Theme.of(context).hintColor),
                      ),
                ),
                SizedBox(height: 12.0),
                Divider(height: 1, indent: 16.0, endIndent: 16.0),
              ],
            ),
            Container(
              padding: EdgeInsets.symmetric(horizontal: 16.0),
              child: Column(
                children: <Widget>[
                  SizedBox(
                    height: 8.0,
                  ),
                  ListTileTheme(
                    textColor: Theme.of(context).textTheme.bodyText2.color,
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
                            _strings.totalImpressions,
                            style: TextStyle(fontSize: 14.0),
                          ))
                        ],
                      ),
                      trailing: Text(mediaSummary.totalImpressionsDisplay),
                    ),
                  ),
                  insightsStatTileDense(
                    showArrow: isOwner,
                    onTap: () => _launchMedia(mediaSummary.maxImpressions.url),
                    context: context,
                    title:
                        'High · ${mediaSummary.maxImpressions.dayTimeDisplay}',
                    value: mediaSummary.maxImpressions.total.toDouble(),
                    formatAsInt: true,
                  ),
                  insightsStatTileDense(
                    showArrow: isOwner,
                    onTap: () => _launchMedia(mediaSummary.minImpressions.url),
                    context: context,
                    title:
                        'Low · ${mediaSummary.minImpressions.dayTimeDisplay}',
                    value: mediaSummary.minImpressions.total.toDouble(),
                    formatAsInt: true,
                  ),
                  SizedBox(height: 4),
                  Divider(height: 1),
                  SizedBox(height: 4),
                  ListTileTheme(
                    textColor: Theme.of(context).textTheme.bodyText2.color,
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
                            _strings.averageReach,
                            style: TextStyle(fontSize: 14.0),
                          ))
                        ],
                      ),
                      trailing: Text(
                        mediaSummary.avgReach.toStringAsFixed(1),
                      ),
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
                  SizedBox(height: 12),
                ],
              ),
            ),
            sectionDivider(context),
            Container(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _buildEngagementHeader(isMe, profileUsername),
                  Padding(
                    padding: EdgeInsets.only(top: 40.0, left: 16, right: 16),
                    child: insightsBigStat(
                      context: context,
                      value: storyEngagmentRate,
                      formatAsPercentage: true,
                      label: _strings.storyEngagementFormula,
                      subtitleWidget: EngagementCalculator(
                        followerCount: response.followedBy,
                        engagementRate: storyEngagmentRateDbl,
                      ),
                    ),
                  ),
                  ProfileInsightsChart(
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
                  ),
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
            ),
            Container(
              padding: EdgeInsets.symmetric(horizontal: 16.0),
              child: Column(
                children: <Widget>[
                  SizedBox(
                    height: 8.0,
                  ),
                  ListTileTheme(
                    textColor: Theme.of(context).textTheme.bodyText2.color,
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
                            _strings.storyEngagementPerStory,
                            style: TextStyle(fontSize: 14.0),
                          ))
                        ],
                      ),
                    ),
                  ),
                  insightsStatTileDense(
                    showArrow: isOwner,
                    onTap: () => _launchMedia(mediaSummary.maxEngagement.url),
                    context: context,
                    title:
                        'High · ${mediaSummary.maxEngagement.dayTimeDisplay}',
                    value: mediaSummary.maxEngagement.total.toDouble(),
                    formatAsPercentage: true,
                  ),
                  insightsStatTileDense(
                    showArrow: isOwner,
                    onTap: () => _launchMedia(mediaSummary.minEngagement.url),
                    context: context,
                    title: 'Low · ${mediaSummary.minEngagement.dayTimeDisplay}',
                    value: mediaSummary.minEngagement.total.toDouble(),
                    formatAsPercentage: true,
                  ),
                  SizedBox(height: 4),
                  Divider(height: 1),
                  SizedBox(height: 4),
                  ListTileTheme(
                    textColor: Theme.of(context).textTheme.bodyText2.color,
                    child: ListTile(
                      contentPadding: EdgeInsets.symmetric(horizontal: 0),
                      dense: true,
                      title: Text(
                        _strings.storyReplies,
                        style: TextStyle(fontSize: 14.0),
                      ),
                      trailing: mediaSummary.avgReplies.toString().endsWith('0')
                          ? Text(mediaSummary.avgReplies.toInt().toString())
                          : Text(mediaSummary.avgReplies.toStringAsFixed(1)),
                    ),
                  ),
                  SizedBox(
                    height: 16.0,
                  )
                ],
              ),
            ),
          ],
        ));
  }

  Widget _buildImpReachHeader(bool isMe, String profileUsername) =>
      insightsSectionHeader(
          context: context,
          icon: AppIcons.chartLine,
          title: _strings.discovery,
          subtitle: _strings.storyImpReachSubtitle,
          bottomSheetTitle: _strings.storyImpReachSheetTitle,
          bottomSheetSubtitle: _strings.storyImpReachSubtitle,
          bottomSheetWidget: insightsBottomSheet(
            context,
            [
              InsightsBottomSheetTile(
                _strings.impressions,
                _strings.storyImpressionsDescription,
              ),
              InsightsBottomSheetTile(
                _strings.reach,
                _strings.storyReachDescription,
              ),
              InsightsBottomSheetTile(
                _strings.totalReach,
                _strings.storyTotalReachDescription,
              ),
              InsightsBottomSheetTile(
                _strings.averageReach,
                _strings.storyAvgReachDescription,
              ),
            ],
          ),
          initialRatio: 0.54);

  Widget _buildEngagementHeader(bool isMe, String profileUsername) =>
      insightsSectionHeader(
          context: context,
          icon: AppIcons.chartLine,
          title: _strings.engagement,
          subtitle: _strings.storyEngagementSubtitle,
          bottomSheetTitle: _strings.storyEngagementSheetTitle,
          bottomSheetSubtitle: _strings.storyEngagementSubtitle,
          bottomSheetWidget: insightsBottomSheet(
            context,
            [
              InsightsBottomSheetTile(
                _strings.impressions,
                _strings.storyImpressionsDescription,
              ),
              InsightsBottomSheetTile(
                _strings.replies,
                _strings.storyRepliesDescription,
              ),
              InsightsBottomSheetTile(
                _strings.storyEngagement,
                _strings.storyEngagementRateDescription,
              ),
            ],
          ),
          initialRatio: 0.46);

  Widget _buildGroupingLinks(int index) => _bloc.lastPostsIndex2 != ''
      ? insightsToggleButtons(
          context,
          [
            _bloc.lastPostsIndex0,
            _bloc.lastPostsIndex1,
            _bloc.lastPostsIndex2,
          ],
          index,
          _bloc.toggleShowIndex,
        )
      : _bloc.lastPostsIndex1 != ''
          ? insightsToggleButtons(
              context,
              [
                _bloc.lastPostsIndex0,
                _bloc.lastPostsIndex1,
              ],
              index,
              _bloc.toggleShowIndex,
            )
          : insightsToggleButtons(
              context,
              [
                _bloc.lastPostsIndex0,
              ],
              index,
              _bloc.toggleShowIndex,
            );
}
