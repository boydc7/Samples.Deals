import 'package:rydr_app/models/enums/publisher_media.dart';

class PublisherAccountMediaAnalysis {
  final PublisherContentType contentType;

  final int imageCount;
  final int imagesQueued;
  final int imageFacesCount;
  final List<ValueWithConfidence> imageLabels;
  final List<ValueWithConfidence> imageModerations;
  final Map<String, dynamic> imageFacesEmotions;
  final double imageFacesAvgAge;
  final int imageFacesMales;
  final int imageFacesFemales;
  final int imageFacesSmiles;
  final int imageFacesBeards;
  final int imageFacesMustaches;
  final int imageFacesEyeglasses;
  final int imageFacesSunglasses;

  final int totalSentimentOccurrences;
  final int textCount;
  final List<ValueWithConfidence> textEntities;
  final double textPositiveSentimentPercentage;
  final double textNegativeSentimentPercentage;
  final double textNeutralSentimentPercentage;
  final double textMixedSentimentPercentage;

  PublisherAccountMediaAnalysis.fromJson(Map<String, dynamic> json)
      : contentType = publisherContentTypeFromJson(json['contentType']),
        imageCount = json['imageCount'],
        imagesQueued = json['imagesQueued'],
        imageFacesCount = json['imageFacesCount'],
        imageLabels = jsonToValueWithConfidence(json['imageLabels']),
        imageModerations = jsonToValueWithConfidence(json['imageModerations']),
        imageFacesEmotions = json['imageFacesEmotions'],
        imageFacesAvgAge = jsonToDoubleOrNull(json['imageFacesAvgAge']),
        imageFacesMales = json['imageFacesMales'],
        imageFacesFemales = json['imageFacesFemales'],
        imageFacesSmiles = json['imageFacesSmiles'],
        imageFacesBeards = json['imageFacesBeards'],
        imageFacesMustaches = json['imageFacesMustaches'],
        imageFacesEyeglasses = json['imageFacesEyeglasses'],
        imageFacesSunglasses = json['imageFacesSunglasses'],
        textCount = json['textCount'],
        textEntities = jsonToValueWithConfidence(json['textEntities']),
        textPositiveSentimentPercentage =
            jsonToDoubleOrNull(json['textPositiveSentimentPercentage']),
        textNegativeSentimentPercentage =
            jsonToDoubleOrNull(json['textNegativeSentimentPercentage']),
        textNeutralSentimentPercentage =
            jsonToDoubleOrNull(json['textNeutralSentimentPercentage']),
        textMixedSentimentPercentage =
            jsonToDoubleOrNull(json['textMixedSentimentPercentage']),
        totalSentimentOccurrences = json['totalSentimentOccurrences'];

  bool get hasValidTextSentimentData =>
      textMixedSentimentPercentage > 0 ||
      textNegativeSentimentPercentage > 0 ||
      textPositiveSentimentPercentage > 0 ||
      textNeutralSentimentPercentage > 0;
}

class PublisherMediaAnalysis {
  final int imageFacesCount;
  final List<ValueWithConfidence> imageLabels;
  final List<ValueWithConfidence> imageModerations;
  final Map<String, int> imageFacesEmotions;
  final double imageFacesAvgAge;
  final int imageFacesMales;
  final int imageFacesFemales;
  final int imageFacesSmiles;
  final int imageFacesBeards;
  final int imageFacesMustaches;
  final int imageFacesEyeglasses;
  final int imageFacesSunglasses;

  final List<ValueWithConfidence> textEntities;
  final bool isPositiveSentiment;
  final bool isNegativeSentiment;
  final bool isNeutralSentiment;
  final bool isMixedSentiment;

  PublisherMediaAnalysis.fromJson(Map<String, dynamic> json)
      : imageFacesCount = json['imageFacesCount'],
        imageLabels = jsonToValueWithConfidence(json['imageLabels']),
        imageModerations = jsonToValueWithConfidence(json['imageModerations']),
        imageFacesEmotions = json['imageFacesEmotions'] != null
            ? Map.from(json['imageFacesEmotions'])
            : {},
        imageFacesAvgAge = jsonToDoubleOrNull(json['imageFacesAvgAge']),
        imageFacesMales = json['imageFacesMales'],
        imageFacesFemales = json['imageFacesFemales'],
        imageFacesSmiles = json['imageFacesSmiles'],
        imageFacesBeards = json['imageFacesBeards'],
        imageFacesMustaches = json['imageFacesMustaches'],
        imageFacesEyeglasses = json['imageFacesEyeglasses'],
        imageFacesSunglasses = json['imageFacesSunglasses'],
        textEntities = jsonToValueWithConfidence(json['textEntities']),
        isPositiveSentiment = json['isPositiveSentiment'],
        isNegativeSentiment = json['isNegativeSentiment'],
        isNeutralSentiment = json['isNeutralSentiment'],
        isMixedSentiment = json['isMixedSentiment'];
}

class ValueWithConfidence {
  final String value;
  final double confidence;
  final int occurrences;

  ValueWithConfidence.fromJson(Map<String, dynamic> json)
      : value = json['value'],
        confidence = json['confidence'].toDouble(),
        occurrences = json['occurrences'];
}

List<ValueWithConfidence> jsonToValueWithConfidence(List<dynamic> json) {
  return json == null
      ? null
      : List<ValueWithConfidence>.from(
          json.map((media) => ValueWithConfidence.fromJson(media)).toList());
}

double jsonToDoubleOrNull(dynamic json) =>
    json == null ? null : json.toDouble();
