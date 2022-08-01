import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';

import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/deal_request_completion_stats.dart';

import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/icons.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/profile/widgets/insights_media_analysis_detail_viewer.dart';
import 'package:rydr_app/ui/shared/widgets/image_error.dart';

import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';

class DealCompletionDetails extends StatefulWidget {
  final Deal deal;

  DealCompletionDetails(this.deal);

  @override
  State createState() => _DealCompletionDetailsState();
}

class _DealCompletionDetailsState extends State<DealCompletionDetails> {
  OverlayEntry _overlayEntry;

  final Map<String, String> _pageContent = {
    "no_completion_stats_yet_title": "We're gathering stats...",
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

  void _showDetails(PublisherAccount user) {
    _overlayEntry = OverlayEntry(
      builder: (BuildContext context) => Dismissible(
        key: UniqueKey(),
        direction: DismissDirection.down,
        onDismissed: (DismissDirection direction) => _overlayEntry.remove(),
        child: MediaViewer(
          user,
          widget.deal.request.completionMedia,
          null,
          _overlayEntry,
          null,
        ),
      ),
      opaque: false,
      maintainState: true,
    );

    Navigator.of(context).overlay.insert(_overlayEntry);
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
                    height: 200.0,
                    child: Align(
                      alignment: Alignment.center,
                      child: GestureDetector(
                        onTap: () =>
                            _showDetails(widget.deal.request.publisherAccount),
                        child: _buildCompletionMediaThumbnails(),
                      ),
                    ),
                  ),
                ),
                _buildRollUpStats(),
                _buildCostStats(),
                Divider(height: 0),
              ],
            );

  Widget _buildCompletionMediaThumbnails() {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final String pluralPosts =
        widget.deal.request.completionMedia.length == 1 ? "Post" : "Posts";
    if (widget.deal.request.completionMedia.length == 0) {
      return Container();
    }

    List<Widget> _media = [];

    widget.deal.request.completionMedia.asMap().forEach((int index, dynamic m) {
      final bool first = index == 0;
      final bool second = index == 1;
      final bool third = index == 2;
      final int length = widget.deal.request.completionMedia.length;
      final double angle = first
          ? length == 2 ? -0.05 : length == 1 ? 0.0 : -0.12
          : second ? length == 2 ? 0.05 : 0.0 : third ? 0.12 : 0.0;
      final double distance = 32.0;
      final double storyHeight = length == 1 ? 140.0 : 120.0;

      if (index < 3) {
        return _media.add(
          Transform.rotate(
            angle: angle,
            child: Transform.translate(
              offset: Offset(
                  length >= 3 && !second
                      ? first ? -distance * 2 : third ? distance * 2 : 0.0
                      : length == 2 ? first ? -distance : distance : 0.0,
                  length >= 3 && !second ? -4.0 : 0.0),
              child: CachedNetworkImage(
                imageUrl: m.previewUrl,
                imageBuilder: (context, imageProvider) => Container(
                  width: m.contentType == PublisherContentType.post
                      ? length == 1 ? 120.0 : 92.0
                      : storyHeight * 0.5625,
                  height: m.contentType == PublisherContentType.post
                      ? length == 1 ? 120.0 : 92.0
                      : storyHeight,
                  decoration: BoxDecoration(
                    color: Theme.of(context).appBarTheme.color,
                    border: Border.all(
                        color: Theme.of(context).scaffoldBackgroundColor,
                        width: 1.0),
                    borderRadius: BorderRadius.circular(2.0),
                    boxShadow: AppShadows.elevation[index == 0 ? 0 : 1],
                    image: DecorationImage(
                        image: imageProvider,
                        fit: BoxFit.cover,
                        alignment: Alignment.center),
                  ),
                ),
                errorWidget: (context, url, error) => ImageError(
                  logUrl: url,
                  logParentName:
                      'deal/widgets/request/completion_details.dart > _buildCompletionMediaThumbnails',
                  logPublisherAccountId:
                      widget.deal.request.publisherAccount.id,
                ),
              ),
            ),
          ),
        );
      }
    });

    return Stack(
      alignment: _media.length > 1 ? Alignment.bottomCenter : Alignment.center,
      children: <Widget>[
        Container(
          width: double.infinity,
          height: 180.0,
          child: Stack(
            alignment: Alignment.center,
            children: _media,
          ),
        ),
        Container(
          padding: EdgeInsets.symmetric(
              horizontal: 12.0, vertical: _media.length > 1 ? 6.0 : 12.0),
          margin: EdgeInsets.only(bottom: _media.length > 1 ? 13.0 : 0.0),
          decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(30.0),
              color: dark
                  ? Theme.of(context).appBarTheme.color.withOpacity(0.8)
                  : AppColors.white.withOpacity(_media.length > 1 ? 0.8 : 0.3)),
          child: _media.length > 1
              ? Text(
                  "View ${widget.deal.request.completionMedia.length} $pluralPosts",
                  style: Theme.of(context).textTheme.caption.merge(TextStyle(
                      color: dark ? Colors.white : AppColors.grey800,
                      fontWeight: FontWeight.w600)))
              : Icon(
                  AppIcons.search,
                  color: Colors.white,
                ),
        )
      ],
    );
  }

  Widget _buildRollUpStats() {
    /// if the publisher who completed this request is not a full account
    /// then we won't have any rollup stats for this
    if (!widget.deal.request.publisherAccount.isAccountFull) {
      return Container();
    }

    /// if we don't have any stats yet, then they're still loading so show a message for that
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
                    .bodyText1
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
                            child: Icon(AppIcons.solidHeart,
                                size: 16.0,
                                color: Theme.of(context).iconTheme.color),
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
                            child: Icon(AppIcons.solidComment,
                                size: 16.0,
                                color: Theme.of(context).iconTheme.color),
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
                            child: Icon(AppIcons.video,
                                size: 16.0,
                                color: Theme.of(context).iconTheme.color),
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
                            child: Icon(AppIcons.paperPlaneSolid,
                                size: 16.0,
                                color: Theme.of(context).iconTheme.color),
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
                            child: Icon(AppIcons.solidBookmark,
                                size: 16.0,
                                color: Theme.of(context).iconTheme.color),
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

    /// we'd only have stats available for full publishers who copmleted this request
    /// so of we don't have one of those then we'd never have stats for this completion
    final DealRequestCompletionStats stats =
        !widget.deal.request.publisherAccount.isAccountFull
            ? null
            : widget.deal.requestCompletionStats;

    return stats == null
        ? Container(height: 0)
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
                Visibility(
                  visible: appState.currentProfile.isBusiness,
                  child: Column(
                    children: <Widget>[
                      // insightsStatTile(
                      //   context: context,
                      //   title: _pageContent['cpm'],
                      //   value: stats.cost == 0 ? null : stats.costImpressions,
                      //   valueAsString: 'N/A',
                      //   formatAsCurrency: true,
                      // ),
                      // stats.hasStories && stats.hasPosts
                      //     ? Column(
                      //         children: <Widget>[
                      //           insightsStatTileDense(
                      //             indent: false,
                      //             context: context,
                      //             title: _pageContent['from_stories'],
                      //             value: stats.cost == 0
                      //                 ? null
                      //                 : stats.costStoryImpressions,
                      //             valueAsString: 'N/A',
                      //             formatAsCurrency: true,
                      //           ),
                      //           insightsStatTileDense(
                      //             indent: false,
                      //             context: context,
                      //             title: _pageContent['from_posts'],
                      //             value: stats.cost == 0
                      //                 ? null
                      //                 : stats.costPostImpressions,
                      //             valueAsString: 'N/A',
                      //             formatAsCurrency: true,
                      //           )
                      //         ],
                      //       )
                      //     : nullWidget,
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
                ),
              ],
            ),
          );
  }
}
