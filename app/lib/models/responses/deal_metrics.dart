import 'package:rydr_app/models/deal_metric.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class DealMetricsResponse extends BaseResponse<DealCompletionMediaMetrics> {
  DealMetricsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => DealCompletionMediaMetrics.fromJson(j),
        );
}

class DealMetrcisDelinquentResponse extends BaseIntResponse {
  DealMetrcisDelinquentResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          'delinquentCount',
        );
}
