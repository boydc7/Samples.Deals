import 'dart:async';

import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/publisher_insights_age_gender.dart';
import 'package:rydr_app/models/responses/publisher_insights_growth.dart';
import 'package:rydr_app/models/responses/publisher_insights_locations.dart';
import 'package:rydr_app/models/responses/publisher_insights_media.dart';
import 'package:rydr_app/services/api.dart';

class PublisherInsightsService {
  static Future<PublisherInsightsLocationsResponse> getLocations(
    int profileId, {
    bool forceRefresh = false,
  }) async {
    final String path = 'publisheracct/$profileId/audience/locations';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherInsightsLocationsResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherInsightsGrowthResponse> getGrowth(
    int profileId, {
    bool forceRefresh = false,
  }) async {
    final String path = 'publisheracct/$profileId/audience/growth';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherInsightsGrowthResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherInsightsAgeAndGenderResponse> getAgeAndGender(
    int profileId, {
    bool forceRefresh = false,
  }) async {
    final String path = 'publisheracct/$profileId/audience/agegenders';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherInsightsAgeAndGenderResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherInsightsMediaResponse> getMediaInsights(
    int profileId, {
    PublisherContentType contentType,
    bool forceRefresh = false,
    int take = 25,
  }) async {
    final String path = 'publisheracct/$profileId/contentstats';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: {
          "contentType": publisherContentTypeToString(contentType),
          "limit": take,
        },
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherInsightsMediaResponse.fromApiResponse(apiResponse);
  }
}
