import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/publisher_media_vision.dart';

class PublisherAccountMediaVisionResponse {
  final PublisherAccountMediaVision vision;
  final DioError error;

  PublisherAccountMediaVisionResponse(
    this.vision,
    this.error,
  );

  PublisherAccountMediaVisionResponse.fromResponse(Map<String, dynamic> json)
      : vision = PublisherAccountMediaVision.fromJson(json['result']),
        error = null;

  PublisherAccountMediaVisionResponse.withError(DioError error)
      : vision = null,
        error = error;
}
