import 'package:mime/mime.dart';
import 'package:rydr_app/models/enums/file.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_stat.dart';

class PublisherMedia {
  /// update-able
  int id;

  final int publisherAccountId;
  final String mediaId;
  final PublisherType pubType;
  final PublisherContentType contentType;
  final String mediaUrl;
  final MediaType type;
  final String publisherUrl;
  final DateTime createdAt;
  final String thumbnailUrl;
  final int actionCount;
  final int commentCount;
  final int analyzePriority;
  final PublisherMediaStat lifetimeStats;
  final bool isPreBizAccountConversionMedia;
  final bool isCompletionMedia;
  final bool isAnalyzed;
  final bool isMediaRydrHosted;

  /// caption can be set
  String caption;

  /// hack: rather than extending...
  /// using this only to keep track of selected media on completion page
  bool selected = false;

  PublisherMedia.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        publisherAccountId = json['publisherAccountId'],
        mediaId = json['mediaId'],
        pubType = publisherTypeFromString(json['publisherType']),
        contentType = publisherContentTypeFromJson(json['contentType']),
        caption = json['caption'],
        mediaUrl = json['mediaUrl'],
        type = mediaTypeFromString(json['mediaType']),
        publisherUrl = json['publisherUrl'],
        createdAt = json['createdAt'] != null
            ? DateTime.parse(json['createdAt'])
            : null,
        thumbnailUrl = json['thumbnailUrl'],
        actionCount = json['actionCount'],
        commentCount = json['commentCount'],
        analyzePriority = json['analyzePriority'],
        isPreBizAccountConversionMedia =
            json['isPreBizAccountConversionMedia'] ?? true,
        isAnalyzed = json['isAnalyzed'] ?? false,
        isCompletionMedia = json['isCompletionMedia'] ?? false,
        isMediaRydrHosted = json['isMediaRydrHosted'] ?? false,
        lifetimeStats = json['lifetimeStats'] != null
            ? PublisherMediaStat.fromJson(json['lifetimeStats'])
            : null;

  PublisherMediaStatValues get lifetimeStatValues =>
      PublisherMediaStatValues(lifetimeStats?.stats);

  String get previewUrl =>
      this.thumbnailUrl != null ? this.thumbnailUrl : this.mediaUrl;

  /// is this file something we can play in the video player?
  /// must be type video, hosted on our servers, have a mediaUrl and a mime type of any video type
  /// NOTE: extension parsing in the mime package does not support query strings so we split on ?
  bool get isMediaPlayable =>
      this.type == MediaType.video &&
      this.isMediaRydrHosted &&
      this.mediaUrl != null &&
      lookupMimeType(this.mediaUrl.split('?')[0]).split('/')[0] == 'video';
}

class PublisherApprovedMedia {
  int id;
  int publisherAccountId;
  String caption;
  PublisherContentType contentType;
  int mediaFileId;
  String mediaUrl;
  String thumbnailUrl;
  FileConvertStatus convertStatus;

  PublisherApprovedMedia.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        publisherAccountId = json['publisherAccountId'],
        contentType = publisherContentTypeFromJson(json['contentType']),
        mediaFileId = json['mediaFileId'],
        caption = json['caption'],
        mediaUrl = json['mediaUrl'],
        thumbnailUrl = json['thumbnailUrl'],
        convertStatus = fileConvertStatusFromString(json['convertStatus']);

  Map<String, dynamic> toJson() {
    final Map<String, dynamic> map = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        map[fieldName] = value.toString();
      }
    }

    addIfNonNull('id', this.id);
    addIfNonNull('mediaFileId', this.mediaFileId);
    addIfNonNull('caption', this.caption);
    addIfNonNull('contentType', publisherContentTypeToString(this.contentType));
    addIfNonNull(
        'convertStatus', fileConvertStatusToString(this.convertStatus));

    return map;
  }

  String get previewUrl => this.thumbnailUrl ?? this.mediaUrl;
}
