import 'dart:async';
import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/responses/publisher_account_stats.dart';
import 'package:rydrworkspaces/services/api.dart';

class PublisherAccountStatsService {
  static Future<PublisherAccountStatsResponse> getAccountStats() async {
    try {
      final Response res = await AppApi.instance.call('publisheracct/me/stats');

      return PublisherAccountStatsResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherAccountStatsResponse.withError(error);
    }
  }

  static Future<PublisherAccountStatsWithResponse> getAccountStatsWith(
      int dealtWithPublisherAccountId) async {
    try {
      /// TODO: see if 'me' works and if it does then replace in app also
      final Response res = await AppApi.instance
          .call('publisheracct/me/stats/$dealtWithPublisherAccountId');

      return PublisherAccountStatsWithResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherAccountStatsWithResponse.withError(error);
    }
  }
}
