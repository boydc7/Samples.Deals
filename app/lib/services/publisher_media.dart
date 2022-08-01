import 'dart:async';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/models/responses/publisher_media.dart';
import 'package:rydr_app/services/api.dart';

class PublisherMediaService {
  static Future<PublisherMediasResponse> getPublisherMedias({
    int forUserId,
    List<PublisherContentType> contentTypes,
    DateTime createdAfter,
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

    /// can never cache a call for non-synced media as that needs to get very latest
    /// that may not even have been synced on our end
    if (syncedOnly == false) {
      forceRefresh = true;
    }

    /// if we don't specify a specif user we want to get media for
    /// then use the current users' id
    if (forUserId == null) {
      forUserId = appState.currentProfile.id;
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

    final String path = syncedOnly
        ? 'publishermedia/$forUserId/syncedmedia'
        : 'publishermedia/$forUserId/media';

    final ApiResponse apiResponse = await AppApi.instance.get(
      path,
      queryParams: params,
      options: AppApi.instance.cacheConfig(
        path,
        duration: const Duration(hours: 3),

        /// media / image query can be cached independently of
        /// workspace or profile, given mediaIds is unique across all and we already
        /// either include mediaId in path or profile / userId
        includeProfileInKey: false,
        includeWorkspaceInKey: false,
        forceRefresh: forceRefresh,
      ),
    );

    /// NOTE! on dev s3 will delete images after 30 days, so we'll get frequent 403's from aws

    return PublisherMediasResponse.fromApiResponse(apiResponse);
  }

  /// retrieve a single publisher media object
  /// NOTE: currently not used anywhere
  static Future<PublisherMediaResponse> getPublisherMedia(int publisherMediaId,
      [bool forceRefresh = false]) async {
    final String path = 'publishermedia/$publisherMediaId';

    final ApiResponse apiResponse = await AppApi.instance.get(
      path,
      options: AppApi.instance.cacheConfig(
        path,
        duration: const Duration(hours: 3),

        /// media / image query can be cached independently of
        /// workspace or profile, given mediaIds is unique across all and we already
        /// either include mediaId in path or profile / userId
        includeProfileInKey: false,
        includeWorkspaceInKey: false,
        forceRefresh: forceRefresh,
      ),
    );

    return PublisherMediaResponse.fromApiResponse(apiResponse);
  }

  static Future<IntIdResponse> addPublisherMedia(PublisherMedia media) async {
    final ApiResponse apiResponse =
        await AppApi.instance.post('publishermedia', body: {
      'model': {
        'publisherAccountId': media.publisherAccountId,
        'publisherType': publisherTypeToString(media.pubType),
        'contentType': publisherContentTypeToString(media.contentType),
        'caption': media.caption,
        'mediaId': media.mediaId,
        'mediaType': mediaTypeToString(media.type),
        'publisherUrl': media.publisherUrl,
        'mediaUrl': media.mediaUrl
      }
    });

    return IntIdResponse.fromApiResponse(apiResponse);
  }
}
