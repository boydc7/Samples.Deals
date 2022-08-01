import 'dart:async';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/services/api.dart';

import 'package:rydr_app/models/requests/deals.dart';

import '../models/enums/deal.dart';

class DealsService {
  static String pathBusinessDeals = 'query/publisherdeals';
  static String pathBusinessRequests = 'query/dealrequests';
  static String pathCreatorDeals = 'query/publisheddeals';
  static String pathCreatorRequests = 'query/requesteddeals';

  static Future<DealsResponse> getRecentDrafts(
          [bool forceRefresh = false]) async =>
      await queryDeals(
        true,
        request: DealsRequest(
          status: [DealStatus.draft],
        ),
        forceRefresh: forceRefresh,
      );

  static Future<DealsResponse> getRecentDeals(
          [bool forceRefresh = false]) async =>
      await queryDeals(
        true,
        request: DealsRequest(
          status: [DealStatus.published],
          sort: DealSort.newest,
        ),
        forceRefresh: forceRefresh,
      );

  static Future<DealsResponse> queryDeals(
    bool profileIsBusiness, {
    DealsRequest request,
    bool forceRefresh = false,
  }) async {
    /// get the right api path based on whether we're a business or an influencer
    /// and whether we want just deals and deal requests
    final String path = request.requestsQuery
        ? profileIsBusiness ? pathBusinessRequests : pathCreatorRequests
        : profileIsBusiness ? pathBusinessDeals : pathCreatorDeals;

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: request.toMap(),
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return DealsResponse.fromApiResponse(apiResponse);
  }

  static void clearDealsCache(ApiResponse apiResponse, bool isBusiness) {
    if (apiResponse.error == null) {
      AppApi.instance.clearCacheByPath(AppApi.instance.getCachePrimaryKey(
          isBusiness ? pathBusinessDeals : pathCreatorDeals));
    }
  }

  static void clearRequestsCache(ApiResponse apiResponse, bool isBusiness) {
    if (apiResponse.error == null) {
      AppApi.instance.clearCacheByPath(AppApi.instance.getCachePrimaryKey(
          isBusiness ? pathBusinessRequests : pathCreatorRequests));
    }
  }
}
