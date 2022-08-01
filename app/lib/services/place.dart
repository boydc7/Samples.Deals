import 'dart:async';
import 'package:rydr_app/models/requests/facebook_places.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/facebook_places.dart';
import 'package:rydr_app/models/responses/places.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/models/place.dart';

class PlaceService {
  static Future<FacebookPlacesSearchResponse> searchFacebookPlaces(
      FacebookPlacesSearchRequest request) async {
    final ApiResponse apiResponse = await AppApi.instance.get(
      'facebook/search/places',
      queryParams: request.toMap(),
    );

    return FacebookPlacesSearchResponse.fromApiResponse(apiResponse);
  }

  /// get list of 'places' (locations) associated with a particular publisher account
  /// will return an indicator at the place model for which one is set as primary
  static Future<PlacesResponse> getPublisherPlaces(int profileId,
      [bool forceRefresh = false]) async {
    final String path = 'publisheracct/$profileId/places';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PlacesResponse.fromApiResponse(apiResponse);
  }

  /// add/update a place for a publisher, here we can update the isPrimary option as well
  /// which will manage a single primary place on the server for a given publisher
  static Future<PlaceUpsertResponse> savePublisherPlace(
    int profileId,
    Place place,
    bool isPrimary,
  ) async {
    /// if we're linking an existing place then only send the id to link
    /// otherwise, we'll attempt to create a new place and link it in which case
    /// send the entire place model down
    final Map<String, dynamic> payload = place.id != null && place.id > 0
        ? {"id": place.id.toString()}
        : place.toJson();

    final ApiResponse apiResponse = await AppApi.instance.post(
      'publisheracct/$profileId/places',
      body: {
        "isPrimary": isPrimary,
        "place": payload,
      },
    );

    /// force a refresh of places for this publisher
    await getPublisherPlaces(profileId, true);

    return PlaceUpsertResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> deletePublisherPlace(
    int profileId,
    Place place,
  ) async {
    final ApiResponse apiResponse = await AppApi.instance.delete(
      'publisheracct/$profileId/places/${place.id}',
    );

    /// force a refresh of places for this publisher
    getPublisherPlaces(profileId, true);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }
}
