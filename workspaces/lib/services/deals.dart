import 'dart:async';

import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/requests/deals.dart';
import 'package:rydrworkspaces/models/responses/deals.dart';
import 'package:rydrworkspaces/services/api.dart';

class DealsService {
  static Future<DealsResponse> getRecentDrafts(
      [bool forceRefresh = false]) async {
    return await queryDeals(
      request: DealsRequest(
        status: [DealStatus.draft],
      ),
      forceRefresh: forceRefresh,
    );
  }

  static Future<DealsResponse> queryDeals({
    DealsRequest request,
    bool forceRefresh = false,
  }) async {
    /// get the right api path based on whether we're a business or an influencer
    /// and whether we want just deals and deal requests
    final String apiPath =
        request.requestsQuery ? 'query/dealrequests' : 'query/publisherdeals';

    try {
      final Response res =
          await AppApi.instance.call(apiPath, queryParams: request.toMap());

      return DealsResponse.fromResponse(res.data);
    } catch (error) {
      return DealsResponse.withError(error);
    }
  }
}
