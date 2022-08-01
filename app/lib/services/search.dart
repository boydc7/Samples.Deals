import 'dart:async';
import 'package:rydr_app/models/requests/business_search.dart';
import 'package:rydr_app/models/requests/creator_search.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';

import 'api.dart';

class SearchService {
  static Future<PublisherAccountsResponse> queryCreators(
    CreatorSearchRequest request, [
    bool forceRefresh = false,
  ]) async {
    final String path = 'search/creators';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: request.toMap(),
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherAccountsResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherAccountsResponse> queryBusinesses(
    BusinessSearchRequest request, [
    bool forceRefresh = false,
  ]) async {
    final String path = 'search/businesses';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: request.toMap(),
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherAccountsResponse.fromApiResponse(apiResponse);
  }
}
