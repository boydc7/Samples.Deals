import 'dart:async';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/creator_stats.dart';

import 'api.dart';

class CreatorStatsService {
  /// NOTE: this is not yet implemented
  static Future<CreatorStatsResponse> queryStats({
    int publisherAccountId,
  }) async {
    final ApiResponse apiResponse =
        await AppApi.instance.get('query/creatorstats');

    return CreatorStatsResponse.fromApiResponse(apiResponse);
  }
}
