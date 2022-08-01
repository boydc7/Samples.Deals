import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:intl/intl.dart';
import 'package:rydrworkspaces/app/theme.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/deal_request_completion_stats.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/ui/profile/widgets/insights_media_analysis_detail_viewer.dart';
import 'package:rydrworkspaces/ui/shared/widgets/insights_helpers.dart';

class DealCompletionDetails extends StatefulWidget {
  final Deal deal;

  DealCompletionDetails(this.deal);

  @override
  State createState() => _DealCompletionDetailsState();
}

class _DealCompletionDetailsState extends State<DealCompletionDetails> {
  final Map<String, String> _pageContent = {
    "no_completion_stats_yet_title": "We're still gathering stats.",
    "no_completion_stats_yet":
        "Check back soon for updated analytics & insights.",
    "view_details": "",
    "completed_media_insights": "Completed Media Insights",
    "total_completed_media_insights": "Total Completed Media Insights",
    "impressions": "Impressions",
    "total_impressions": "Total Impressions",
    "from_stories": "From Stories",
    "from_stories_total": "From Stories (total)",
    "from_stories_avg": "From Stories (average)",
    "from_posts": "From Posts",
    "avg_reach": "Average Reach",
    "total_reach": "Total Reach",
    "estimated_total_reach": "Estimated Total Reach",
    "unique_reach": "Unique Reach",
    "total_replies": "Total Replies",
    "video_views": "Video Views",
    "total_video_views": "Total Video Views",
    "saves": "Saves",
    "total_saves": "Total Saves",
    "cpm": "Cost per 1,000 Impressions (CPM)",
    "cpe": "Cost per Engagement",
    "cogs": "Cost of Goods",
    "updated_on": "updated",
  };

  @override
  void initState() {
    super.initState();
  }

  @override
  void dispose() {
    super.dispose();
  }

  @override
  Widget build(BuildContext context) =>
      widget.deal.request.status != DealRequestStatus.completed
          ? Container()
          : Column(
              children: <Widget>[
                Visibility(
                  visible: widget.deal.request.completionMedia != null &&
                      widget.deal.request.completionMediaStatValues != null,
                  child: Container(
                      alignment: Alignment.center,
                      height: 600.0,
                      child: MediaViewer(widget.deal.request.publisherAccount,
                          widget.deal.request.completionMedia, null, null)),
                ),
                _buildRollUpStats(),
                _buildCostStats(),
                Divider(height: 0),
              ],
            );

  /// if we don't have any rollup stats then show a message telling the user
  /// that we're still gathering stats on the completion media
  Widget _buildRollUpStats() {
    if (widget.deal.request.completionMediaStatValues == null) {
      return Container(
        width: MediaQuery.of(context).size.width * 0.66,
        padding: EdgeInsets.symmetric(horizontal: 32.0, vertical: 24.0),
        margin: EdgeInsets.all(32),
        decoration: BoxDecoration(
          color: Theme.of(context).canvasColor,
          borderRadius: BorderRadius.circular(8.0),
        ),
        child: Column(
          children: <Widget>[
            Text(
              _pageContent['no_completion_stats_yet_title'],
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 4.0),
            Text(
              _pageContent['no_completion_stats_yet'],
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.caption.merge(
                    TextStyle(color: Theme.of(context).hintColor),
                  ),
            ),
          ],
        ),
      );
    }

    final DealRequestCompletionStats stats = widget.deal.requestCompletionStats;
    final formatter = NumberFormat("#,###");
    final formatterSmall = NumberFormat.compact();
    final TextStyle statStyle = TextStyle(fontSize: 16.0, height: 1.1);

    return Column(
      children: <Widget>[
        Padding(
          padding: EdgeInsets.only(top: 16.0, bottom: 24.0),
          child: Column(
            children: <Widget>[
              Text(
                stats.completedMedia == 1
                    ? _pageContent['completed_media_insights']
                    : _pageContent['total_completed_media_insights'],
                style: Theme.of(context)
                    .textTheme
                    .bodyText2
                    .merge(TextStyle(fontWeight: FontWeight.w600)),
              ),
              Text(
                '${_pageContent['updated_on']} ${widget.deal.request.completionMediaStatsLastSyncedOnDisplayAgo}',
                style: Theme.of(context)
                    .textTheme
                    .caption
                    .merge(TextStyle(color: AppColors.grey400)),
              ),
            ],
          ),
        ),
        Padding(
          padding: EdgeInsets.only(bottom: 16.0),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              stats.completedPosts > 0
                  ? Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          Container(
                            width: 32,
                            child: Icon(
                              Icons.thumb_up,
                              size: 16.0,
                            ),
                            margin: EdgeInsets.only(bottom: 8.0),
                          ),
                          Text(
                            stats.actions == 0
                                ? ""
                                : stats.actions > 1000
                                    ? formatterSmall.format(stats.actions)
                                    : formatter.format(stats.actions),
                            style: statStyle,
                          ),
                        ],
                      ),
                    )
                  : Container(
                      width: 0,
                    ),
              stats.completedPosts > 0
                  ? Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          Container(
                            width: 32,
                            child: Icon(Icons.comment, size: 16.0),
                            margin: EdgeInsets.only(bottom: 8.0),
                          ),
                          Text(
                            stats.comments == 0
                                ? ""
                                : stats.comments > 1000
                                    ? formatterSmall.format(stats.comments)
                                    : formatter.format(stats.comments),
                            style: statStyle,
                          ),
                        ],
                      ),
                    )
                  : Container(
                      width: 0,
                    ),
              stats.completedPosts > 0 && stats.videoViews > 0
                  ? Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          Container(
                            width: 32,
                            child: Icon(
                              Icons.videocam,
                              size: 16.0,
                            ),
                            margin: EdgeInsets.only(bottom: 8.0),
                          ),
                          Text(
                            stats.videoViews == 0
                                ? ""
                                : stats.videoViews > 1000
                                    ? formatterSmall.format(stats.videoViews)
                                    : formatter.format(stats.videoViews),
                            style: statStyle,
                          ),
                        ],
                      ),
                    )
                  : Container(
                      width: 0,
                    ),
              stats.completedStories > 0 && stats.replies != 0
                  ? Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          Container(
                            width: 32,
                            child: Icon(
                              Icons.reply,
                              size: 16.0,
                            ),
                            margin: EdgeInsets.only(bottom: 8.0),
                          ),
                          Text(
                            stats.replies == 0
                                ? ""
                                : stats.replies > 1000
                                    ? formatterSmall.format(stats.replies)
                                    : formatter.format(stats.replies),
                            style: statStyle,
                          ),
                        ],
                      ),
                    )
                  : Container(
                      width: 0,
                    ),
              stats.completedPosts > 0 && stats.saves > 0
                  ? Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.center,
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: <Widget>[
                          Container(
                            width: 32,
                            child: Icon(
                              Icons.bookmark_border,
                              size: 16.0,
                            ),
                            margin: EdgeInsets.only(bottom: 8.0),
                          ),
                          Text(
                            stats.saves == 0
                                ? ""
                                : stats.saves > 1000
                                    ? formatterSmall.format(stats.saves)
                                    : formatter.format(stats.saves),
                            style: statStyle,
                          ),
                        ],
                      ),
                    )
                  : Container(
                      width: 0,
                    ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildCostStats() {
    final Widget nullWidget = Container(height: 0, width: 0);
    final DealRequestCompletionStats stats = widget.deal.requestCompletionStats;

    return stats == null
        ? Container(
            height: 0,
          )
        : Padding(
            padding: EdgeInsets.only(left: 16.0, right: 16.0, bottom: 16.0),
            child: Column(
              children: <Widget>[
                insightsStatTile(
                  context: context,
                  title: stats.hasStories && stats.hasPosts
                      ? _pageContent['total_impressions']
                      : stats.hasPosts && stats.completedPosts == 1
                          ? _pageContent['impressions']
                          : stats.hasPosts && stats.completedPosts > 1
                              ? _pageContent['total_impressions']
                              : stats.hasStories && stats.completedStories == 1
                                  ? _pageContent['impressions']
                                  : stats.hasStories &&
                                          stats.completedStories > 1
                                      ? _pageContent['total_impressions']
                                      : _pageContent['impressions'],
                  value: stats.impressions.toDouble(),
                  formatAsInt: true,
                ),
                stats.hasStories && stats.hasPosts
                    ? insightsStatTileDense(
                        indent: false,
                        context: context,
                        title: _pageContent['from_stories'],
                        value: stats.storyImpressions.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories && stats.hasPosts
                    ? insightsStatTileDense(
                        indent: false,
                        context: context,
                        title: _pageContent['from_posts'],
                        value: stats.postImpressions.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories &&
                        !stats.hasPosts &&
                        stats.completedStories > 1
                    ? Column(
                        children: <Widget>[
                          insightsStatTile(
                            context: context,
                            title: _pageContent['avg_reach'],
                            value: stats.avgStoryReach,
                            formatAsInt: true,
                          ),
                          insightsStatTileDense(
                            indent: false,
                            context: context,
                            title: _pageContent['total_reach'],
                            value: stats.reach.toDouble(),
                            formatAsInt: true,
                          )
                        ],
                      )
                    : insightsStatTile(
                        context: context,
                        title: stats.completedMedia == 1
                            ? _pageContent['unique_reach']
                            : stats.hasStories &&
                                    stats.hasPosts &&
                                    stats.completedStories > 1
                                ? _pageContent['estimated_total_reach']
                                : _pageContent['total_reach'],
                        value: stats.hasStories &&
                                stats.hasPosts &&
                                stats.completedStories > 1
                            ? stats.avgStoryReach + stats.postReach
                            : stats.reach.toDouble(),
                        formatAsInt: true,
                      ),
                !stats.hasStories && stats.hasPosts && stats.completedPosts > 1
                    ? insightsStatTile(
                        context: context,
                        title: _pageContent['avg_reach'],
                        value: stats.reach / stats.completedMedia,
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories &&
                        stats.hasPosts &&
                        stats.completedStories == 1
                    ? insightsStatTileDense(
                        indent: false,
                        context: context,
                        title: _pageContent['from_stories'],
                        value: stats.storyReach.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories && stats.hasPosts && stats.completedStories > 1
                    ? insightsStatTileDense(
                        indent: false,
                        context: context,
                        title: _pageContent['from_stories_total'],
                        value: stats.storyReach.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories && stats.hasPosts && stats.completedStories > 1
                    ? insightsStatTileDense(
                        indent: false,
                        context: context,
                        title: _pageContent['from_stories_avg'],
                        value: stats.avgStoryReach.floorToDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories && stats.hasPosts
                    ? insightsStatTileDense(
                        indent: false,
                        context: context,
                        title: _pageContent['from_posts'],
                        value: stats.postReach.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasStories && stats.replies != 0
                    ? insightsStatTile(
                        context: context,
                        title: _pageContent['total_replies'],
                        value: stats.replies.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasPosts && stats.videoViews != 0
                    ? insightsStatTile(
                        context: context,
                        title: stats.completedMedia == 1
                            ? _pageContent['video_views']
                            : _pageContent['total_video_views'],
                        value: stats.videoViews.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                stats.hasPosts && stats.saves != 0
                    ? insightsStatTile(
                        context: context,
                        title: stats.completedMedia == 1
                            ? _pageContent['saves']
                            : _pageContent['total_saves'],
                        value: stats.saves.toDouble(),
                        formatAsInt: true,
                      )
                    : nullWidget,
                Column(
                  children: <Widget>[
                    insightsStatTile(
                      context: context,
                      title: _pageContent['cpm'],
                      value: stats.cost == 0 ? null : stats.costImpressions,
                      valueAsString: 'N/A',
                      formatAsCurrency: true,
                    ),
                    stats.hasStories && stats.hasPosts
                        ? Column(
                            children: <Widget>[
                              insightsStatTileDense(
                                indent: false,
                                context: context,
                                title: _pageContent['from_stories'],
                                value: stats.cost == 0
                                    ? null
                                    : stats.costStoryImpressions,
                                valueAsString: 'N/A',
                                formatAsCurrency: true,
                              ),
                              insightsStatTileDense(
                                indent: false,
                                context: context,
                                title: _pageContent['from_posts'],
                                value: stats.cost == 0
                                    ? null
                                    : stats.costPostImpressions,
                                valueAsString: 'N/A',
                                formatAsCurrency: true,
                              )
                            ],
                          )
                        : nullWidget,
                    insightsStatTile(
                      context: context,
                      title: _pageContent['cpe'],
                      value: stats.cost == 0 ? null : stats.costEngagement,
                      valueAsString: 'N/A',
                      formatAsCurrency: true,
                    ),
                    stats.hasStories && stats.hasPosts
                        ? Column(
                            children: <Widget>[
                              insightsStatTileDense(
                                indent: false,
                                context: context,
                                title: _pageContent['from_stories'],
                                value: stats.cost == 0
                                    ? null
                                    : stats.costEngagementStory,
                                valueAsString: 'N/A',
                                formatAsCurrency: true,
                              ),
                              insightsStatTileDense(
                                indent: false,
                                context: context,
                                title: _pageContent['from_posts'],
                                value: stats.cost == 0
                                    ? null
                                    : stats.costEngagementPost,
                                valueAsString: 'N/A',
                                formatAsCurrency: true,
                              )
                            ],
                          )
                        : nullWidget,
                    insightsStatTile(
                      context: context,
                      title: _pageContent['cogs'],
                      value: stats.cost,
                      formatAsCurrency: true,
                    ),
                  ],
                ),
              ],
            ),
          );
  }
}
