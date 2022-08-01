import 'package:rydr_app/models/enums/publisher_insights.dart';

import 'package:rydr_app/models/publisher_insights_location.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherInsightsLocationsResponse
    extends BaseResponses<PublisherInsightsLocation> {
  PublisherInsightsLocationsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherInsightsLocation.fromJson(d))
                    .cast<PublisherInsightsLocation>()
                    .toList()
                : []);
}

class PublisherInsightsLocationsResponseWithData {
  final List<PublisherInsightsLocation> locations;
  final PublisherAccount profile;

  List<PublisherInsightsLocation> cities;
  List<PublisherInsightsLocation> countries;
  bool hasResults = false;

  int followedBy;
  double topCity;
  double topCountry;

  PublisherInsightsLocationsResponseWithData(this.locations, this.profile) {
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
}
