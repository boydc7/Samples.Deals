import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_media.dart';

class PublisherMediaResponse {
  final List<PublisherMedia> media;
  final DioError error;

  PublisherMediaResponse(this.media, this.error);

  PublisherMediaResponse.fromResponse(Map<String, dynamic> json)
      : media = json['results'] != null
            ? json['results']
                .map((dynamic d) => PublisherMedia.fromJson(d))
                .cast<PublisherMedia>()
                .toList()
            : [],
        error = null;

  PublisherMediaResponse.withError(DioError error)
      : media = null,
        error = error;
}
