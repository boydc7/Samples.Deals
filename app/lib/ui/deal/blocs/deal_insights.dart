import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/models/responses/deal_metrics.dart';
import 'package:rydr_app/services/deal_metrics.dart';

class DealInsightsBloc {
  final _metricsResponse = BehaviorSubject<DealMetricsResponse>();
  final _scrolled = BehaviorSubject<bool>();

  dispose() {
    _metricsResponse.close();
    _scrolled.close();
  }

  BehaviorSubject<DealMetricsResponse> get metricsResponse =>
      _metricsResponse.stream;

  BehaviorSubject<bool> get scrolled => _scrolled.stream;

  void setScrolled(bool value) => _scrolled.sink.add(value);

  Future<void> loadInsights(int dealId, [bool forceRefresh = false]) async =>
      _metricsResponse.sink.add(await DealMetricsService.getDealMetrics(
        dealId: dealId,
        forceRefresh: forceRefresh,
      ));
}
