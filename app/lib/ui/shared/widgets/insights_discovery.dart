import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/deal_metric.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/ui/shared/widgets/loading_animations.dart';

class InsightsDiscovery extends StatefulWidget {
  final bool isLoading;
  final Deal deal;
  final Widget overlay;
  final DealCompletionMediaMetrics metrics;

  InsightsDiscovery({
    @required this.metrics,
    this.deal,
    this.isLoading = false,
    this.overlay,
  });

  @override
  _InsightsDiscoveryState createState() => _InsightsDiscoveryState();
}

class _InsightsDiscoveryState extends State<InsightsDiscovery> {
  final GlobalKey key = GlobalKey();

  Map<String, String> _pageContent = {
    "title": "Business Discovery",
    "subtitle": "People seeing your business in posts",
    "bottom_sheet_title": "Lifetime Discovery Insights",
    "impressions": "Impressions",
    "impressions_description":
        "The total number of times a post or story has been seen.",
    "reach": "Reach",
    "reach_description":
        "The number of unique accounts that have seen a post or story. This metric is an estimate and may not be exact.",
    "total_impressions": "Total Impressions",
    "total_reach": "Total Reach",
    "from_stories": "Total Stories",
    "from_posts": "Total Feed Posts",
  };

  @override
  void initState() {
    super.initState();

    _pageContent['bottom_sheet_subtitle'] = widget.deal != null
        ? 'This set of insights measures results for this RYDR since its been active in the marketplace.'
        : 'This set of insights measures your results from Instagram since your RYDR account was created.';
  }

  @override
  Widget build(BuildContext context) {
    return widget.isLoading ? _buildLoadingBody() : _buildResultsBody();
  }

  Widget _buildHeader() {
    return insightsSectionHeader(
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
            _pageContent['impressions'],
            _pageContent['impressions_description'],
          ),
          InsightsBottomSheetTile(
            _pageContent['reach'],
            _pageContent['reach_description'],
          ),
        ],
      ),
    );
  }

  Widget _buildLoadingBody() {
    return Container(
      child: Column(
        key: key,
        children: <Widget>[
          _buildHeader(),
          LoadingStatsShimmer(),
        ],
      ),
    );
  }

  Widget _buildListTile(String title, String value, {bool hasDense = false}) {
    return Container(
      padding: EdgeInsets.only(
          left: 16, right: 16, top: 8, bottom: hasDense ? 4 : 8),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(title),
          ),
          Text(value == "NaN%" ? "0%" : value),
        ],
      ),
    );
  }

  Widget _buildListTileDense(String title, int value) {
    NumberFormat numberFormat = NumberFormat.decimalPattern();
    return Container(
      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
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
    );
  }

  Widget _buildStats(int totalImpressions, int totalReach, double aspectRatio) {
    final NumberFormat f = NumberFormat.decimalPattern();
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Padding(
          padding: EdgeInsets.only(top: 40, bottom: 32, left: 16, right: 16),
          child: Row(
            children: <Widget>[
              Expanded(
                child: insightsBigStat(
                  context: context,
                  value: totalImpressions.toDouble(),
                  formatAsInt: true,
                  label: _pageContent['total_impressions'],
                ),
              ),
              Expanded(
                child: insightsBigStat(
                  context: context,
                  value: totalReach.toDouble(),
                  formatAsInt: true,
                  label: _pageContent['total_reach'],
                ),
              ),
            ],
          ),
        ),
        Divider(height: 1, indent: 16, endIndent: 16),
        SizedBox(height: 8),
        _buildListTile(_pageContent['from_stories'],
            f.format(widget.metrics?.completedStoryMedias),
            hasDense: true),
        _buildListTileDense(
            _pageContent['impressions'], widget.metrics?.storyImpressions ?? 0),
        _buildListTileDense(
            _pageContent['reach'], widget.metrics?.storyReach ?? 0),
        SizedBox(height: 12),
        Divider(height: 1, indent: 16, endIndent: 16),
        SizedBox(height: 8),
        _buildListTile(_pageContent['from_posts'],
            f.format(widget.metrics?.completedPostMedias),
            hasDense: true),
        _buildListTileDense(
            _pageContent['impressions'], widget.metrics?.postImpressions ?? 0),
        _buildListTileDense(
            _pageContent['reach'], widget.metrics?.postReach ?? 0),
        SizedBox(height: 16),
      ],
    );
  }

  Widget _buildResultsBody() {
    final double aspectRatio = 1.75;
    final int totalImpressions = (widget.metrics.postImpressions ?? 0) +
        (widget.metrics.storyImpressions ?? 0);
    final int totalReach =
        (widget.metrics.postReach ?? 0) + (widget.metrics.storyReach ?? 0);

    return Column(
      mainAxisSize: MainAxisSize.min,
      key: key,
      children: <Widget>[
        _buildHeader(),
        _buildStats(totalImpressions, totalReach, aspectRatio),
      ],
    );
  }
}
