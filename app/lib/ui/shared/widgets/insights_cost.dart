import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/insights_helpers.dart';
import 'package:rydr_app/models/deal_metric.dart';

class InsightsCost extends StatefulWidget {
  final bool isLoading;
  final Widget overlay;
  final Deal deal;
  final DealCompletionMediaMetrics metrics;

  InsightsCost({
    @required this.metrics,
    this.isLoading = false,
    this.overlay,
    this.deal,
  });

  @override
  _InsightsCostState createState() => _InsightsCostState();
}

class _InsightsCostState extends State<InsightsCost> {
  final GlobalKey key = GlobalKey();

  Map<String, String> _pageContent = {
    "title": "Audience Cost",
    "subtitle": "Value of attention on Instagram",
    "bottom_sheet_title": "Lifetime Audience Cost Insights",
    "cost_per_engagement": "Cost Per Engagement",
    "cost_per_engagement_description":
        "The cost of goods divided by total engagement.",
    "cost_per_cpm": "Cost Per Thousand Impressions (CPM)",
    "cost_per_cpm_description":
        "The cost of goods divided by impressions, multipled by 1,000.",
    "avg_cpm": "Average CPM",
    "stories_avg_cpm": "Average CPM per Story",
    "posts_avg_cpm": "Average CPM per Post",
    "avg_cost_per_engagement": "Average Cost per Engagement",
    "avg_cost_of_goods": "Average Cost of Goods",
  };

  @override
  void initState() {
    super.initState();

    _pageContent['rydr_avg_cpm'] = widget.deal != null
        ? 'Average CPM across all Requests'
        : 'Average CPM across all RYDRs';
    _pageContent['bottom_sheet_subtitle'] = widget.deal != null
        ? 'This set of insights measures your return on investment from this RYDR since it has been active in the marketplace.'
        : 'This set of insights measures your return on investment since your RYDR account was created.';
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
      initialRatio: 0.425,
      bottomSheetTitle: _pageContent['bottom_sheet_title'],
      bottomSheetSubtitle: _pageContent['bottom_sheet_subtitle'],
      bottomSheetWidget: insightsBottomSheet(
        context,
        [
          InsightsBottomSheetTile(
            _pageContent['cost_per_engagement'],
            _pageContent['cost_per_engagement_description'],
          ),
          InsightsBottomSheetTile(
            _pageContent['cost_per_cpm'],
            _pageContent['cost_per_cpm_description'],
          ),
        ],
      ),
    );
  }

  Widget _buildLoadingBody() {
    return Column(
      key: key,
      children: <Widget>[
        _buildHeader(),
        insightsLoadingBody(),
      ],
    );
  }

  Widget _buildListTile(String title, Color color, double value,
      {bool cpe = false}) {
    NumberFormat numberFormat =
        NumberFormat.simpleCurrency(decimalDigits: cpe ? 4 : 2);
    return Container(
      padding: EdgeInsets.only(left: 16, right: 16, top: 8, bottom: 8),
      child: Row(
        children: <Widget>[
          Visibility(
            visible: color != null,
            child: Container(
              height: 8.0,
              width: 8.0,
              margin: EdgeInsets.only(right: 16),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16.0),
                color: color,
              ),
            ),
          ),
          Expanded(
            child: Text(title),
          ),
          Text(numberFormat.format(value)),
        ],
      ),
    );
  }

  Widget _buildResultsBody() {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      key: key,
      children: <Widget>[
        _buildHeader(),
        Padding(
          padding: EdgeInsets.only(top: 40, bottom: 32, left: 16, right: 16),
          child: insightsBigStat(
            context: context,
            value: widget.metrics.avgCpmPerStory == 0.0 ||
                    widget.metrics.avgCpmPerPost == 0.0
                ? (widget.metrics.avgCpmPerStory +
                        widget.metrics.avgCpmPerPost) /
                    1
                : (widget.metrics.avgCpmPerStory +
                        widget.metrics.avgCpmPerPost) /
                    2,
            formatAsCurrency: true,
            label: _pageContent['rydr_avg_cpm'],
          ),
        ),
        Divider(height: 1, indent: 16, endIndent: 16),
        SizedBox(height: 8),
        _buildListTile(
          _pageContent['stories_avg_cpm'],
          null,
          widget.metrics.avgCpmPerStory,
        ),
        _buildListTile(
          _pageContent['posts_avg_cpm'],
          null,
          widget.metrics.avgCpmPerPost,
        ),
        SizedBox(height: 12),
        Divider(height: 1, indent: 16, endIndent: 16),
        SizedBox(height: 8),
        _buildListTile(_pageContent['avg_cost_per_engagement'], null,
            widget.metrics.avgCpePerCompletion,
            cpe: true),
        _buildListTile(
          _pageContent['avg_cost_of_goods'],
          null,
          widget.metrics.avgCogPerCompletedDeal,
        ),
        SizedBox(height: 8),
      ],
    );
  }
}
