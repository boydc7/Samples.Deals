import 'package:rydr_app/models/enums/publisher_media.dart';
import 'package:rydr_app/models/publisher_media_vision.dart';
import 'package:rydr_app/models/range.dart';

class PublisherMediaAnalysisQuery {
  int skip;
  int take;
  String query;
  PublisherContentType contentType;
  List<String> sentiments;
  LongRange facesRange;
  LongRange facesAvgAgeRange;
  LongRange facesMalesRange;
  LongRange facesFemalesRange;
  LongRange facesSmilesRange;
  LongRange facesBeardsRange;
  LongRange facesMustachesRange;
  LongRange facesEyeglassesRange;
  LongRange facesSunglassesRange;
  bool forceRefresh;

  PublisherMediaAnalysisQuery({
    this.skip = 0,
    this.take = 10,
    this.query,
    this.contentType,
    this.sentiments,
    this.facesRange,
    this.facesAvgAgeRange,
    this.facesMalesRange,
    this.facesFemalesRange,
    this.facesSmilesRange,
    this.facesBeardsRange,
    this.facesMustachesRange,
    this.facesEyeglassesRange,
    this.facesSunglassesRange,
    this.forceRefresh = false,
  });

  PublisherMediaAnalysisQuery.fromSearchDescriptor(
      PublisherAccountMediaVisionSectionSearchDescriptor desc,
      [PublisherContentType contentType]) {
    this.query = desc.query;
    this.contentType = desc.contentType != PublisherContentType.unkonwn
        ? desc.contentType
        : contentType;
    this.sentiments = desc.sentiments.isNotEmpty ? desc.sentiments : null;
    this.facesRange = desc.facesRange;
    this.facesAvgAgeRange = desc.facesAvgAgeRange;
    this.facesMalesRange = desc.facesMalesRange;
    this.facesFemalesRange = desc.facesFemalesRange;
    this.facesSmilesRange = desc.facesSmilesRange;
    this.facesBeardsRange = desc.facesBeardsRange;
    this.facesMustachesRange = desc.facesMustachesRange;
    this.facesEyeglassesRange = desc.facesEyeglassesRange;
    this.facesSunglassesRange = desc.facesSunglassesRange;
  }

  Map<String, dynamic> toMap() {
    final Map<String, dynamic> paramsMap = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        paramsMap[fieldName] = value;
      }
    }

    addIfNonNull('skip', skip != null ? skip.toString() : null);
    addIfNonNull('take', take != null ? take.toString() : null);
    addIfNonNull('query', query);
    addIfNonNull('contentType',
        contentType != null ? publisherContentTypeToString(contentType) : null);
    addIfNonNull(
        'sentiments',
        sentiments != null
            ? '[' + sentiments.map((String s) => '$s').toList().join(',') + ']'
            : null);
    addIfNonNull('facesRange', facesRange);
    addIfNonNull('facesAvgAgeRange', facesAvgAgeRange);
    addIfNonNull('facesMalesRange', facesMalesRange);
    addIfNonNull('facesFemalesRange', facesFemalesRange);
    addIfNonNull('facesSmilesRange', facesSmilesRange);
    addIfNonNull('facesBeardsRange', facesBeardsRange);
    addIfNonNull('facesMustachesRange', facesMustachesRange);
    addIfNonNull('facesEyeglassesRange', facesEyeglassesRange);
    addIfNonNull('facesSunglassesRange', facesSunglassesRange);
    addIfNonNull('forceRefresh', forceRefresh);
    addIfNonNull('sortRecent', true);

    return paramsMap;
  }
}
