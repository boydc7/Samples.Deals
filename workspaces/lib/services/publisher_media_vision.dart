import 'dart:async';

import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/responses/base.dart';
import 'package:rydrworkspaces/models/responses/publisher_media_vision.dart';
import 'package:rydrworkspaces/services/api.dart';

class PublisherAccountMediaVisionService {
  static Future<PublisherAccountMediaVisionResponse> getPublisherMediaVision(
      int profileId) async {
    try {
      final Response res = await AppApi.instance.call(
        'publisheracct/$profileId/mediavision',
      );

      return PublisherAccountMediaVisionResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherAccountMediaVisionResponse.withError(error);
    }
  }

  static Future<BaseResponse> setAiToAnalyzePriority(
      int mediaId, int priority) async {
    try {
      await AppApi.instance.call(
        'publishermedia/$mediaId/prioritize',
        method: 'PUT',
      );

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }
}
