import 'package:rydr_app/models/creator_stats.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class CreatorStatsResponse extends BaseResponse {
  CreatorStatsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => CreatorStats.fromJson(j),
        );
}
