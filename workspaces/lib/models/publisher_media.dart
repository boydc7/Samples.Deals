import 'package:rydrworkspaces/models/enums/publisher_account.dart';
import 'package:rydrworkspaces/models/enums/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_media_stat.dart';

class PublisherMedia {
  final int id;
  final String mediaId;
  final PublisherType pubType;
  final PublisherContentType contentType;
  final String caption;
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

  PublisherMedia.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        mediaId = json['mediaId'],
        pubType = publisherTypeFromString(json['publisherType']),
        contentType = publisherContentTypeFromJson(json['contentType']),
        caption = json['caption'],
        mediaUrl = json['mediaUrl'],
        type = mediaTypeFromString(json['mediaType']),
        publisherUrl = json['publisherUrl'],
        createdAt = DateTime.parse(json['createdAt']),
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
}
