import 'package:rydrworkspaces/models/enums/publisher_insights.dart';

class PublisherInsightsLocation {
  LocationType type;
  String name;
  String nameCleaned;
  int value;

  PublisherInsightsLocation.fromJson(Map<String, dynamic> json) {
    type = locationTypeFromString(json['locationType']);
    name = json['name'];
    value = json['value'];

    /// split name to get "Miami" from "Miami, Florida"
    nameCleaned = name.split(',')[0];
  }

  double percentage(int totalFollowers) => (value / totalFollowers) * 100;
}
