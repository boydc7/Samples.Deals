import 'package:flutter/material.dart';
import 'package:fl_chart/fl_chart.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_metric.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class InsightsEngagement extends StatefulWidget {
  final bool isLoading;
  final Widget overlay;
  final Deal deal;
  final DealCompletionMediaMetrics metrics;

  InsightsEngagement({
    @required this.metrics,
    this.isLoading = false,
    this.overlay,
    this.deal,
  });

  @override
  _InsightsEngagementState createState() => _InsightsEngagementState();
}

class _InsightsEngagementState extends State<InsightsEngagement> {
  final GlobalKey key = GlobalKey();

  final NumberFormat numberFormat = NumberFormat.decimalPattern();

  Map<String, String> _pageContent = {
    "title": "Post Engagement",
    "subtitle": "Post Interactions about your business",
    "bottom_sheet_title": "Lifetime Engagement Insights",
    "total_engagement": "Total Engagement",
    "stories": "From Stories",
    "impressions": "Impressions",
    "replies": "Replies",
    "posts": "From Posts",
    "comments": "Comments",
    "likes": "Likes",
    "video_views": "Video Views",
    "post_saves": "Saves",
    "story_engagement": "Story Engagement",
    "story_engagement_description":
        "The combined number of impressions and replies.",
    "post_engagement": "Post Engagement",
    "post_engagement_description":
        "The combined number of video views, likes, comments, and saves.",
  };

  @override
  void initState() {
    super.initState();

    _pageContent['bottom_sheet_subtitle'] = widget.deal != null
        ? 'This set of insights measures the engagement of posts submitted since this RYDR has been active in the marketplace.'
        : 'This set of insights measures the engagement of RYDR posts, since your account was created.';
  }

  @override
  Widget build(BuildContext context) =>
      widget.isLoading ? _buildLoadingBody() : _buildResultsBody();

  Widget _buildHeader() => insightsSectionHeader(
        context: context,
        title: _pageContent['title'],
        subtitle: _pageContent['subtitle'],
        initialRatio: 0.4,
        bottomSheetTitle: _pageContent['bottom_sheet_title'],
        bottomSheetSubtitle: _pageContent['bottom_sheet_subtitle'],
        bottomSheetWidget: insightsBottomSheet(
          context,
          [
            InsightsBottomSheetTile(
              _pageContent['story_engagement'],
              _pageContent['story_engagement_description'],
            ),
            InsightsBottomSheetTile(
              _pageContent['post_engagement'],
              _pageContent['post_engagement_description'],
            )
          ],
        ),
      );

  Widget _buildListTile(
    String title,
    Color color,
    int value, {
    bool hasDense = false,
  }) =>
      Container(
        padding: EdgeInsets.only(
            left: 16, right: 16, top: 8, bottom: hasDense ? 4 : 8),
        child: Row(
          children: <Widget>[
            Container(
              height: 8.0,
              width: 8.0,
              margin: EdgeInsets.only(right: 16),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16.0),
                color: color,
              ),
            ),
            Expanded(
              child: Text(title),
            ),
            Text(numberFormat.format(value)),
          ],
        ),
      );

  Widget _buildListTileDense(String title, int value) => Visibility(
        visible: value != 0,
        child: Container(
          padding: EdgeInsets.only(right: 16, top: 4, bottom: 4, left: 40),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(title,
                    style: TextStyle(color: Theme.of(context).hintColor)),
              ),
              Text(numberFormat.format(value),
                  style: TextStyle(color: Theme.of(context).hintColor)),
            ],
          ),
        ),
      );

  Widget _buildLoadingBody() => Column(
        key: key,
        children: <Widget>[
          _buildHeader(),
          LoadingStatsShimmer(),
        ],
      );

  Widget _buildResultsBody() => Column(
        mainAxisSize: MainAxisSize.min,
        key: key,
        children: <Widget>[
          _buildHeader(),
          Padding(
            padding: EdgeInsets.only(top: 40, bottom: 32, left: 16, right: 16),
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                InsightsEngagementPie(widget.metrics),
                insightsBigStat(
                  context: context,
                  centered: true,
                  value: widget.metrics.totalEngagements?.toDouble() ?? 0,
                  formatAsInt: true,
                  label: _pageContent['total_engagement'],
                ),
              ],
            ),
          ),
          Divider(height: 1, indent: 16, endIndent: 16),
          SizedBox(height: 8),
          _buildListTile(
              _pageContent['stories'],
              Theme.of(context).primaryColor,
              widget.metrics.storyEngagements ?? 0,
              hasDense: true),
          _buildListTileDense(
            _pageContent['impressions'],
            widget.metrics.storyImpressions ?? 0,
          ),
          _buildListTileDense(
            _pageContent['replies'],
            widget.metrics.storyReplies ?? 0,
          ),
          SizedBox(height: 12),
          Divider(height: 1, indent: 16, endIndent: 16),
          SizedBox(height: 8),
          _buildListTile(_pageContent['posts'], AppColors.teal,
              widget.metrics.postEngagements ?? 0,
              hasDense: true),
          _buildListTileDense(
            _pageContent['likes'],
            widget.metrics.postActions ?? 0,
          ),
          _buildListTileDense(
            _pageContent['comments'],
            widget.metrics.postComments ?? 0,
          ),
          _buildListTileDense(
            _pageContent['video_views'],
            widget.metrics.postViews ?? 0,
          ),
          _buildListTileDense(
            _pageContent['post_saves'],
            widget.metrics.postSaves ?? 0,
          ),
          SizedBox(height: 12),
          sectionDivider(context),
        ],
      );
}

class InsightsEngagementPie extends StatelessWidget {
  final DealCompletionMediaMetrics metrics;

  InsightsEngagementPie(this.metrics);

  @override
  Widget build(BuildContext context) {
    /// we'll draw a pie based on two numbers, story and post engagement
    /// ensure we have them, otherwise set them to 0
    final double storyEngagements = metrics.storyEngagements?.toDouble() ?? 0;
    final double postEngagements = metrics.postEngagements?.toDouble() ?? 0;

    /// add pie data element for story and post engagement
    /// only if we have any greather than zero
    final items = [
      storyEngagements > 0
          ? PieChartSectionData(
              showTitle: false,
              color: Theme.of(context).primaryColor,
              value: metrics.storyEngagements?.toDouble() ?? 0,
              radius: 3.0,
            )
          : null,
      postEngagements > 0
          ? PieChartSectionData(
              showTitle: false,
              color: AppColors.teal,
              value: metrics.postEngagements?.toDouble() ?? 0,
              radius: 3.0,
            )
          : null,
    ].where((element) => element != null).toList();

    /// return a container if we don't have any piechart data
    /// otherwise draw the piechart
    return items.isEmpty
        ? Container()
        : AspectRatio(
            aspectRatio: 1.75,
            child: PieChart(
              PieChartData(
                startDegreeOffset: 270,
                pieTouchData: PieTouchData(
                  enabled: false,
                ),
                borderData: FlBorderData(
                  show: false,
                ),
                sectionsSpace: 2.0,
                centerSpaceRadius: 88.0,
                sections: items,
              ),
            ),
          );
  }
}
