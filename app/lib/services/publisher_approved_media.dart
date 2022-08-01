import 'dart:async';
import 'dart:io';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/models/responses/publisher_approved_media.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/services/file.dart';

class PublisherApprovedMediaService {
  static Future<IntIdResponse> uploadPublisherApprovedMedia(File file) async {
    /// upload the file which will either give us back the media file id we use
    /// to then create the publisher approved media, or NULL in case there were errors
    final int mediaFileId = await FileService.upload(file);

    if (mediaFileId == null) {
      return null;
    }

    final ApiResponse apiResponse = await AppApi.instance.post(
      'approvedmedia',
      body: {
        "model": {
          "mediaFileId": mediaFileId,
          "contentType":
              publisherContentTypeToString(PublisherContentType.post),
        },
      },
    );

    return IntIdResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherApprovedMediaResponse> getPublisherApprovedMedia(
      int id,
      [bool refresh = false]) async {
    final String path = 'approvedmedia/$id';

    final ApiResponse apiResponse = await AppApi.instance.get(
      path,
      options: AppApi.instance.cacheConfig(
        path,
        forceRefresh: refresh,
      ),
    );

    return PublisherApprovedMediaResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> updatePublisherApprovedMedia(
    PublisherApprovedMedia media,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance.put(
      'approvedmedia/${media.id}',
      body: {
        "model": media.toJson(),
      },
    );

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<PublisherApprovedMediasResponse> getPublisherApprovedMedias({
    int dealId,
    int skip,
    int take,
    bool forceRefresh = false,
  }) async {
    final String path = 'approvedmedia';

    final ApiResponse apiResponse = await AppApi.instance.get(
      path,
      queryParams: {
        "skip": skip ?? 0,
        "take": take ?? 50,
        "dealId": dealId,
      },
      options: AppApi.instance.cacheConfig(
        path,
        forceRefresh: forceRefresh,
      ),
    );

    return PublisherApprovedMediasResponse.fromApiResponse(apiResponse);
  }
}
