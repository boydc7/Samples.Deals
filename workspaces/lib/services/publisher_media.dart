import 'dart:async';
import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/enums/publisher_media.dart';
import 'package:rydrworkspaces/models/responses/publisher_media.dart';
import 'package:rydrworkspaces/services/api.dart';

class PublisherMediaService {
  static Future<PublisherMediaResponse> getPublisherMedia({
    int forUserId,
    List<PublisherContentType> contentTypes,
    DateTime createdAfter,
    int publisherMediaId,
    int limit = 30,
    bool forceRefresh = false,

    /// only media we have synced on our end
    bool syncedOnly = true,

    /// only active stories if true
    bool liveMediaOnly = false,
  }) async {
    var params = {
      'limit': limit.toString(),
      'forceRefresh': forceRefresh.toString(),
      'liveMediaOnly': liveMediaOnly.toString()
    };

    /// if we don't specify a specif user we want to get media for
    /// then use the current users' id
    if (forUserId == null) {
      //forUserId = appState.currentProfile.id;
    }

    if (contentTypes != null) {
      params['contentTypes'] = '[' +
          contentTypes
              .map((s) => '"${publisherContentTypeToString(s)}"')
              .toList()
              .join(',') +
          ']';
    }

    if (createdAfter != null) {
      params['createdAfter'] = createdAfter.toIso8601String();
    }

    try {
      final Response res = await AppApi.instance.call(
        publisherMediaId != null
            ? 'publishermedia/$publisherMediaId'
            : syncedOnly
                ? 'publishermedia/$forUserId/syncedmedia'
                : 'publishermedia/$forUserId/media',
        queryParams: params,
      );

      return PublisherMediaResponse.fromResponse(res.data);
    } catch (error) {
      return PublisherMediaResponse.withError(error);
    }
  }
}
