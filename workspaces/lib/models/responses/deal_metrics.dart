import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/deal_metric.dart';

class DealMetricsResponse {
  final DealCompletionMediaMetrics metrics;
  final DioError error;

  DealMetricsResponse(this.metrics, this.error);

  DealMetricsResponse.fromResponse(Map<String, dynamic> json)
      : metrics = DealCompletionMediaMetrics.fromJson(json['result']),
        error = null;

  DealMetricsResponse.withError(DioError error)
      : metrics = null,
        error = error;
}

class DealMetrcisDelinquentResponse {
  final int delinquentCount;
  final DioError error;

  DealMetrcisDelinquentResponse(this.delinquentCount, this.error);

  DealMetrcisDelinquentResponse.fromResponse(Map<String, dynamic> json)
      : delinquentCount = json['delinquentCount'],
        error = null;

  DealMetrcisDelinquentResponse.withError(DioError error)
      : delinquentCount = null,
        error = error;
}
