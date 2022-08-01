import 'dart:async';

import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/deal_metrics.dart';
import 'package:rydr_app/ui/deal/blocs/deal_insights.dart';
import 'package:rydr_app/ui/shared/widgets/buttons.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

import 'package:rydr_app/ui/shared/widgets/insights_discovery.dart';
import 'package:rydr_app/ui/shared/widgets/insights_engagement.dart';
import 'package:rydr_app/ui/shared/widgets/insights_media.dart';

import 'package:rydr_app/app/theme.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/ui/shared/widgets/retry_error.dart';

class DealInsightsPage extends StatefulWidget {
  final Deal deal;

  DealInsightsPage({
    this.deal,
  });

  @override
  State<StatefulWidget> createState() {
    return _DealInsightsPageState();
  }
}

class _DealInsightsPageState extends State<DealInsightsPage> {
  final ScrollController _scrollController = ScrollController();
  final DealInsightsBloc _bloc = DealInsightsBloc();

  @override
  void initState() {
    super.initState();

    _bloc.loadInsights(widget.deal.id);

    _scrollController.addListener(_scrollListener);
  }

  @override
  void dispose() {
    _bloc.dispose();
    _scrollController.dispose();

    super.dispose();
  }

  Future<void> _refresh() async => _bloc.loadInsights(widget.deal.id, true);

  void _scrollListener() {
    if (_scrollController.offset > 50.0 &&
        (_bloc.scrolled.value == null || _bloc.scrolled.value == false)) {
      _bloc.setScrolled(true);
    } else if (_scrollController.offset <= 50.0 &&
        _bloc.scrolled.value == true) {
      _bloc.setScrolled(false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    return StreamBuilder<DealMetricsResponse>(
      stream: _bloc.metricsResponse,
      builder: (context, snapshot) {
        return snapshot.connectionState == ConnectionState.waiting
            ? _buildLoadingBody(dark)
            : snapshot.data.error != null || snapshot.data.model == null
                ? _buildErrorBody(dark, snapshot.data)
                : _buildSuccessBody(dark, snapshot.data);
      },
    );
  }

  Widget _buildLoadingBody(bool dark) => Scaffold(
        appBar: AppBar(
          leading: AppBarBackButton(context),
          title: Text("Loading insights..."),
        ),
        body: ListView(
          children: <Widget>[
            InsightsDiscovery(
              isLoading: true,
              deal: widget.deal,
              metrics: null,
            ),
            sectionDivider(context),
            InsightsMedia(
              isLoading: true,
              deal: widget.deal,
              metrics: null,
            ),
            sectionDivider(context),
            InsightsEngagement(
              isLoading: true,
              deal: widget.deal,
              metrics: null,
            ),
            sectionDivider(context),
            // InsightsCost(
            //   isLoading: true,
            //   deal: widget.deal,
            //   metrics: null,
            // ),
          ],
        ),
      );

  Widget _buildErrorBody(bool dark, DealMetricsResponse dealResponse) =>
      Scaffold(
        appBar: AppBar(leading: AppBarBackButton(context)),
        backgroundColor: dark
            ? Theme.of(context).scaffoldBackgroundColor
            : AppColors.white50,
        body: RetryError(
          onRetry: () => _bloc.loadInsights(widget.deal.id),
          error: dealResponse.error,
        ),
      );

  Widget _buildSuccessBody(bool dark, DealMetricsResponse metricsResponse) =>
      Scaffold(
        appBar: AppBar(
          title: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              Text('Insights: ${widget.deal.title}'),
              Text(
                  widget.deal.getStat(DealStatType.currentCompleted) == 1
                      ? 'Totals for ${widget.deal.getStat(DealStatType.currentCompleted)} completed request'
                      : 'Totals for ${widget.deal.getStat(DealStatType.currentCompleted)} completed requests',
                  style: Theme.of(context).textTheme.caption),
            ],
          ),
          leading: AppBarBackButton(context),
          actions: <Widget>[
            IconButton(
              icon: Container(),
              onPressed: null,
            )
          ],
        ),
        body: RefreshIndicator(
          displacement: 0.0,
          backgroundColor: Theme.of(context).appBarTheme.color,
          color: Theme.of(context).textTheme.bodyText2.color,
          onRefresh: _refresh,
          child: ListView(
            children: <Widget>[
              InsightsDiscovery(
                  deal: widget.deal, metrics: metricsResponse.model),
              InsightsMedia(
                metrics: metricsResponse.model,
                deal: widget.deal,
              ),
              InsightsEngagement(
                  deal: widget.deal, metrics: metricsResponse.model),
              // InsightsCost(deal: widget.deal, metrics: metricsResponse.model),
            ],
          ),
        ),
      );
}
