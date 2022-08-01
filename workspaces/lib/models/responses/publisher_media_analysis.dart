import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_media.dart';
import 'package:rydrworkspaces/models/publisher_media_analysis.dart';

class PublisherAccountMediaAnalysisResponse {
  final PublisherAccountMediaAnalysis accountAnalysis;
  final PublisherAccountMediaAnalysis storyAnalysis;
  final PublisherAccountMediaAnalysis postAnalysis;
  final DioError error;

  PublisherAccountMediaAnalysisResponse(
    this.accountAnalysis,
    this.storyAnalysis,
    this.postAnalysis,
    this.error,
  );

  PublisherAccountMediaAnalysisResponse.fromResponse(Map<String, dynamic> json)
      : accountAnalysis = PublisherAccountMediaAnalysis.fromJson(
            json['result']['accountAnalysis']),
        storyAnalysis = PublisherAccountMediaAnalysis.fromJson(
            json['result']['storyAnalysis']),
        postAnalysis = PublisherAccountMediaAnalysis.fromJson(
            json['result']['postAnalysis']),
        error = null;

  PublisherAccountMediaAnalysisResponse.withError(DioError error)
      : accountAnalysis = null,
        storyAnalysis = null,
        postAnalysis = null,
        error = error;
}

class PublisherMediaAnalysisResponse {
  final PublisherMediaAnalysis analysis;
  final DioError error;

  PublisherMediaAnalysisResponse(this.analysis, this.error);

  PublisherMediaAnalysisResponse.fromResponse(Map<String, dynamic> json)
      : analysis = PublisherMediaAnalysis.fromJson(json['result']),
        error = null;

  PublisherMediaAnalysisResponse.withError(DioError error)
      : analysis = null,
        error = error;
}

class PublisherMediaAnalysisQueryResponse {
  final List<PublisherMedia> medias;
  final DioError error;

  PublisherMediaAnalysisQueryResponse(this.medias, this.error);

  PublisherMediaAnalysisQueryResponse.fromResponse(Map<String, dynamic> json)
      : medias = json['results'] != null
            ? json['results']
                .map((dynamic d) => PublisherMedia.fromJson(d))
                .cast<PublisherMedia>()
                .toList()
            : [],
        error = null;

  PublisherMediaAnalysisQueryResponse.withError(DioError error)
      : medias = null,
        error = error;
}
