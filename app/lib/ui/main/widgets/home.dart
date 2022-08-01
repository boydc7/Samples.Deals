import 'dart:async';
import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/deal_metrics.dart';
import 'package:rydr_app/models/deal_metric.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/ui/shared/widgets/insights_discovery.dart';
import 'package:rydr_app/ui/shared/widgets/insights_engagement.dart';
import 'package:rydr_app/ui/shared/widgets/insights_media.dart';
import 'package:rydr_app/ui/shared/widgets/list_item.dart';

class Home extends StatefulWidget {
  final Function onDealFilter;
  final Function onRequestFilter;

  Home(this.onDealFilter, this.onRequestFilter);

  @override
  _HomeState createState() => _HomeState();
}

class _HomeState extends State<Home> with AutomaticKeepAliveClientMixin {
  final _scrollController = ScrollController();

  ThemeData _theme;
  bool _darkMode;

  @override
  bool get wantKeepAlive => true;

  void _handleFilter(int linkIndex) {
    if (linkIndex == 0) {
      widget.onDealFilter();
    } else if (linkIndex == 1) {
      widget.onRequestFilter(DealRequestStatus.requested);
    } else if (linkIndex == 2) {
      widget.onRequestFilter(DealRequestStatus.invited);
    } else if (linkIndex == 3) {
      widget.onRequestFilter(DealRequestStatus.inProgress);
    } else if (linkIndex == 4) {
      widget.onRequestFilter(DealRequestStatus.redeemed);
    } else if (linkIndex == 5) {
      widget.onRequestFilter(DealRequestStatus.completed);
    }
  }

  Future<void> refresh() async => appState.loadProfileStats();

  @override
  Widget build(BuildContext context) {
    super.build(context);

    _theme = Theme.of(context);
    _darkMode = _theme.brightness == Brightness.dark;

    return RefreshIndicator(
        displacement: 0.0,
        backgroundColor: _theme.appBarTheme.color,
        color: _theme.textTheme.bodyText2.color,
        onRefresh: refresh,
        child: ListView(
            padding: EdgeInsets.only(
                top: 0, bottom: 16 + MediaQuery.of(context).padding.bottom),
            controller: _scrollController,
            children: <Widget>[
              StreamBuilder<DealMetricsResponse>(
                stream: appState.currentProfileDealStats,
                builder: (context, snapshot) {
                  final DealMetricsResponse metricsResponse = snapshot.data;
                  final DealCompletionMediaMetrics metrics =
                      metricsResponse?.model;

                  return snapshot.data == null
                      ? Container()
                      : Column(
                          children: <Widget>[
                            InsightsDiscovery(metrics: metrics),
                            _buildInjections(),
                            InsightsMedia(metrics: metrics),
                            InsightsEngagement(metrics: metrics),
                            // InsightsCost(metrics: metrics),

                            /// Not implemented yet => UpgraderCheck(),
                          ],
                        );
                },
              ),
            ]));
  }

  Widget _buildInjections() {
    return StreamBuilder<PublisherAccountStatsResponse>(
      stream: appState.currentProfileStats,
      builder: (context, snapshot) {
        final PublisherAccountStatsResponse res = snapshot.data;

        if (res == null) {
          return sectionDivider(context);
        } else {
          final int requested =
              res.model.tryGetDealStatValue(DealStatType.currentRequested);
          final int completed =
              res.model.tryGetDealStatValue(DealStatType.completedThisWeek);

          return Container(
            width: double.infinity,
            padding: EdgeInsets.symmetric(vertical: 16),
            decoration: BoxDecoration(
              border: Border(
                bottom: BorderSide(
                    color: _darkMode ? Color(0xFF090909) : Colors.grey.shade300,
                    width: 0.5),
                top: BorderSide(
                    color: _darkMode ? Color(0xFF090909) : Colors.grey.shade300,
                    width: 0.5),
              ),
              color: _darkMode ? Color(0xFF0F0F0F) : Colors.grey.shade200,
            ),
            child: Padding(
              padding: EdgeInsets.symmetric(horizontal: 8),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Card(
                      elevation: 1,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                      color: _darkMode ? _theme.canvasColor : _theme.cardColor,
                      margin: EdgeInsets.all(0),
                      child: InkWell(
                        borderRadius: BorderRadius.circular(8),
                        splashColor: Utils.getRequestStatusColor(
                                DealRequestStatus.requested, _darkMode)
                            .withOpacity(0.2),
                        onTap: () => _handleFilter(1),
                        child: Container(
                          padding:
                              EdgeInsets.symmetric(horizontal: 8, vertical: 16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: <Widget>[
                              Text(
                                "$requested",
                                style: _theme.textTheme.headline6.merge(
                                  TextStyle(
                                      color: Utils.getRequestStatusColor(
                                          DealRequestStatus.requested,
                                          _darkMode)),
                                ),
                              ),
                              SizedBox(height: 8),
                              Text(
                                "Pending " +
                                    (requested == 1 ? "Request" : "Requests"),
                                style: _theme.textTheme.bodyText1,
                              ),
                              Text(
                                "Tap to respond",
                                style: _theme.textTheme.caption.merge(
                                  TextStyle(
                                    color: _theme.hintColor,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                  SizedBox(width: 8),
                  Expanded(
                    child: Card(
                      elevation: 1,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                      color: _darkMode ? _theme.canvasColor : _theme.cardColor,
                      margin: EdgeInsets.all(0),
                      child: InkWell(
                        borderRadius: BorderRadius.circular(8),
                        splashColor: Utils.getRequestStatusColor(
                                DealRequestStatus.completed, _darkMode)
                            .withOpacity(0.2),
                        onTap: () => _handleFilter(5),
                        child: Container(
                          padding:
                              EdgeInsets.symmetric(horizontal: 8, vertical: 16),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: <Widget>[
                              Text(
                                "$completed",
                                style: _theme.textTheme.headline6.merge(
                                  TextStyle(
                                      color: Utils.getRequestStatusColor(
                                          DealRequestStatus.completed,
                                          _darkMode)),
                                ),
                              ),
                              SizedBox(height: 8),
                              Text(
                                "Completed this Week",
                                style: _theme.textTheme.bodyText1,
                              ),
                              Text(
                                "Tap to view",
                                style: _theme.textTheme.caption.merge(
                                  TextStyle(
                                    color: _theme.hintColor,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          );
        }
      },
    );
  }
}
