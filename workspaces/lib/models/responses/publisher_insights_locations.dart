import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/enums/publisher_insights.dart';
import 'package:rydrworkspaces/models/publisher_account.dart';
import 'package:rydrworkspaces/models/publisher_insights_location.dart';

class PublisherInsightsLocationsResponse {
  List<PublisherInsightsLocation> locations;
  PublisherAccount profile;
  DioError error;

  List<PublisherInsightsLocation> cities;
  List<PublisherInsightsLocation> countries;
  bool hasResults = false;

  int followedBy;
  double topCity;
  double topCountry;

  PublisherInsightsLocationsResponse(this.locations, this.profile, this.error);

  PublisherInsightsLocationsResponse.fromResponse(
    PublisherAccount p,
    Map<String, dynamic> json,
  ) {
    profile = p;

    locations = json['results'] != null
        ? json['results']
            .map((dynamic d) => PublisherInsightsLocation.fromJson(d))
            .cast<PublisherInsightsLocation>()
            .toList()
        : [];

    followedBy = profile.publisherMetrics.followedBy.toInt();

    /// sort cities & countries by the most followers to the least
    cities = locations
        .where((PublisherInsightsLocation loc) => loc.type == LocationType.City)
        .toList()
          ..sort((a, b) => b.value.compareTo(a.value));

    countries = locations
        .where(
            (PublisherInsightsLocation loc) => loc.type == LocationType.Country)
        .toList()
          ..sort((a, b) => b.value.compareTo(a.value));

    /// get only top 10
    if (cities.length > 10) {
      cities = cities..removeRange(10, cities.length);
    }

    if (countries.length > 10) {
      countries = countries..removeRange(10, countries.length);
    }

    /// get the top city/country in terms of followers percentage
    topCity = cities.length > 0 && followedBy > 0
        ? cities[0].value / followedBy
        : 0.0;

    topCountry = countries.length > 0 && followedBy > 0
        ? countries[0].value / followedBy
        : 0.0;

    /// we define having results as having at least one city and one country
    hasResults = cities.length > 1 && countries.length > 1;
  }

  PublisherInsightsLocationsResponse.withError(DioError error)
      : profile = null,
        locations = null,
        error = error;
}
