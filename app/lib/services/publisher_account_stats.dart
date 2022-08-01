import 'dart:async';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/publisher_account_stats.dart';
import 'package:rydr_app/services/api.dart';

class PublisherAccountStatsService {
  static Future<PublisherAccountStatsResponse> getAccountStats() async {
    final ApiResponse apiResponse =
        await AppApi.instance.get('publisheracct/me/stats');

    return PublisherAccountStatsResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherAccountStatsWithResponse> getAccountStatsWith(
      int profileId, int dealtWithPublisherAccountId) async {
    final ApiResponse apiResponse = await AppApi.instance
        .get('publisheracct/$profileId/stats/$dealtWithPublisherAccountId');

    return PublisherAccountStatsWithResponse.fromApiResponse(apiResponse);
  }
}
