import 'dart:async';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/deal_metrics.dart';
import 'package:rydr_app/services/api.dart';

class DealMetricsService {
  /// NOTE: this is currently not implemented yet, its meant to return
  /// count of rydrs that are 'delinquent' based on time/date but have not yet
  /// been marked as delinquent by the business
  static Future<DealMetrcisDelinquentResponse> getDelinquentCount() async {
    final ApiResponse apiResponse =
        await AppApi.instance.get('dealmetrics/me/delinquent');

    return DealMetrcisDelinquentResponse.fromApiResponse(apiResponse);
  }

  /// Gets aggregated stats either for a given deal
  /// or for the currently authenticated pub account as a whole
  static Future<DealMetricsResponse> getDealMetrics({
    int dealId,
    bool forceRefresh = false,
  }) async {
    final String path = 'dealmetrics/completion';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: dealId != null ? {"dealId": dealId} : null,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return DealMetricsResponse.fromApiResponse(apiResponse);
  }
}
