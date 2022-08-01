import 'dart:async';

import 'package:rydr_app/models/requests/publisher_media_analysis.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/publisher_media_analysis.dart';
import 'package:rydr_app/services/api.dart';

class PublisherMediaAnalysisService {
  static Future<PublisherMediaAnalysisResponse> getPublisherMediaAnalysis(
      int mediaId,
      {bool forceRefresh = false}) async {
    final String path = 'publishermedia/$mediaId/analysis';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,

          /// individual media / image analysis can be cached independently of
          /// workspace or profile, given mediaId is unique across all
          includeProfileInKey: false,
          includeWorkspaceInKey: false,
          forceRefresh: forceRefresh,
        ));

    return PublisherMediaAnalysisResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherMediaAnalysisQueryResponse>
      queryPublisherMediaAnalysis(
    int profileId,
    PublisherMediaAnalysisQuery request, {
    bool forceRefresh = false,
  }) async {
    final String path = 'publisheracct/$profileId/mediasearch';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: request.toMap(),
        options: AppApi.instance.cacheConfig(
          path,

          /// media / image search can be cached independently of
          /// workspace or profile, given mediaIds is unique across all
          includeProfileInKey: false,
          includeWorkspaceInKey: false,
          forceRefresh: forceRefresh,
        ));

    return PublisherMediaAnalysisQueryResponse.fromApiResponse(apiResponse);
  }
}
