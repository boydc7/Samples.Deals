import 'package:dio/dio.dart';

import 'package:rydrworkspaces/models/publisher_insights_media.dart';

class PublisherInsightsMediaResponse {
  final List<PublisherInsightsMedia> media;
  final DioError error;

  PublisherInsightsMediaResponse(this.media, this.error);

  PublisherInsightsMediaResponse.fromResponse(Map<String, dynamic> json)
      : media = json['results'] != null
            ? json['results']
                .map((dynamic d) => PublisherInsightsMedia.fromJson(d))
                .cast<PublisherInsightsMedia>()
                .toList()
            : [],
        error = null;

  PublisherInsightsMediaResponse.withError(DioError error)
      : media = null,
        error = error;
}
