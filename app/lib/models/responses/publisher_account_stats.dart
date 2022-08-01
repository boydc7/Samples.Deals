import 'package:rydr_app/models/publisher_account_stats.dart';
import 'package:rydr_app/models/publisher_account_stats_with.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherAccountStatsResponse
    extends BaseResponse<PublisherAccountStats> {
  PublisherAccountStatsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherAccountStats.fromJson(j),
        );
}

class PublisherAccountStatsWithResponse
    extends BaseResponse<PublisherAccountStatsWith> {
  PublisherAccountStatsWithResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherAccountStatsWith.fromJson(j),
        );
}
