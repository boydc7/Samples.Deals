import 'package:rydr_app/models/tag.dart';

class BusinessSearchRequest {
  int skip;
  int take;
  double longitude;
  double latitude;
  double miles;
  List<Tag> tags;
  String search;

  BusinessSearchRequest({
    this.skip = 0,
    this.take = 25,
    this.latitude,
    this.longitude,
    this.miles,
    this.tags,
    this.search,
  });

  Map<String, dynamic> toMap() {
    final Map<String, dynamic> paramsMap = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        paramsMap[fieldName] = value;
      }
    }

    addIfNonNull('skip', skip);
    addIfNonNull('take', take);
    addIfNonNull('latitude', latitude);
    addIfNonNull('longitude', longitude);
    addIfNonNull('miles', miles);
    addIfNonNull(
        'tags',
        tags != null && tags.isNotEmpty
            ? tags.map((t) => t.toJson()).toList().toString()
            : null);
    addIfNonNull('query',
        search != null && search.trim().length > 0 ? search.trim() : null);

    return paramsMap;
  }
}
