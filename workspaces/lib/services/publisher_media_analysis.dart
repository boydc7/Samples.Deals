import 'dart:async';

import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/requests/publisher_media_analysis.dart';
import 'package:rydrworkspaces/models/responses/publisher_media_analysis.dart';
import 'package:rydrworkspaces/services/api.dart';

class PublisherMediaAnalysisService {
  static Future<PublisherAccountMediaAnalysisResponse>
      getPublisherAccountMediaAnalysis([
    int forUserId,
  ]) async {
    try {
      final Response res = await AppApi.instance
          .call('publisheracct/${forUserId ?? 'me'}/mediaanalysis');

      return PublisherAccountMediaAnalysisResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherAccountMediaAnalysisResponse.withError(error);
    }
  }

  static Future<PublisherMediaAnalysisResponse> getPublisherMediaAnalysis(
      int mediaId) async {
    try {
      final Response res =
          await AppApi.instance.call('publishermedia/$mediaId/analysis');

      return PublisherMediaAnalysisResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherMediaAnalysisResponse.withError(error);
    }
  }

  static Future<PublisherMediaAnalysisQueryResponse>
      queryPublisherMediaAnalysis(
          int profileId, PublisherMediaAnalysisQuery request) async {
    try {
      final Response res = await AppApi.instance.call(
          'publisheracct/$profileId/mediasearch',
          queryParams: request.toMap());

      return PublisherMediaAnalysisQueryResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherMediaAnalysisQueryResponse.withError(error);
    }
  }
}
