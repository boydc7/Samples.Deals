import 'dart:async';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/publisher_media_vision.dart';
import 'package:rydr_app/services/api.dart';

class PublisherAccountMediaVisionService {
  static Future<PublisherAccountMediaVisionResponse> getPublisherMediaVision(
    int profileId, {
    bool forceRefresh = false,
  }) async {
    final String path = 'publisheracct/$profileId/mediavision';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          duration: const Duration(hours: 3),

          /// media / image analysis can be cached independently of
          /// workspace or profile, given mediaIds is unique across all
          includeProfileInKey: false,
          includeWorkspaceInKey: false,
          forceRefresh: forceRefresh,
        ));

    return PublisherAccountMediaVisionResponse.fromApiResponse(apiResponse);
  }

  /// NOTE: this is not yet implemented
  /// this allows
  static Future<BasicVoidResponse> setAiToAnalyzePriority(
      int mediaId, int priority) async {
    final ApiResponse apiResponse = await AppApi.instance.put(
      'publishermedia/$mediaId/prioritize',
    );

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }
}
