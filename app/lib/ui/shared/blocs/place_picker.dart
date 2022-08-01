import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/publisher_account.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/requests/facebook_places.dart';
import 'package:rydr_app/models/responses/facebook_places.dart';
import 'package:rydr_app/models/responses/places.dart';
import 'package:rydr_app/services/place.dart';

class PlacePickerBloc {
  final _searchResponse = BehaviorSubject<PlacePickerSearchResponse>();

  List<Place> _existingPlaces;

  PlacePickerBloc(List<Place> existingPlaces) {
    _existingPlaces = existingPlaces;

    _searchResponse.sink.add(null);
  }

  dispose() {
    _searchResponse.close();
  }

  Stream<PlacePickerSearchResponse> get searchResponse =>
      _searchResponse.stream;

  void search(String query) async {
    if (query.trim().length > 2) {
      _searchResponse.sink.add(PlacePickerSearchResponse(null, true));

      final FacebookPlacesSearchResponse res =
          await PlaceService.searchFacebookPlaces(
        FacebookPlacesSearchRequest(query),
      );

      /// if we have a successful response with results, and we have existing places
      /// then remove the existing places from the search results
      if (res.error == null &&
          res.models != null &&
          res.models.isEmpty == false &&
          _existingPlaces != null) {
        _existingPlaces.forEach((Place place) {
          res.models.removeWhere((FacebookPlaceInfo fbPlace) =>
              fbPlace.id == place.publisherId &&
              place.pubType == PublisherType.facebook);
        });
      }

      _searchResponse.sink.add(PlacePickerSearchResponse(res, false));
    }
  }

  void clearSearch() {
    _searchResponse.sink.add(PlacePickerSearchResponse(null, false));
  }

  Future<Place> addPlace(FacebookPlaceInfo fbPlace) async {
    final Place place = Place(
      id: fbPlace.rydrPlaceId,
      name: fbPlace.name,
      publisherId: fbPlace.id,
      pubType: PublisherType.facebook,
      address: PlaceAddress(
        name: fbPlace.singleLineAddress,
        address1: fbPlace.location.address1,
        city: fbPlace.location.city,
        stateProvince: fbPlace.location.stateProvince,
        postalCode: fbPlace.location.postalCode,
        latitude: fbPlace.location.latitude,
        longitude: fbPlace.location.longitude,
      ),
    );

    final PlaceUpsertResponse res = await PlaceService.savePublisherPlace(
        appState.currentProfile.id, place, true);

    return res.error == null ? place : null;
  }
}

class PlacePickerSearchResponse {
  final FacebookPlacesSearchResponse response;
  final bool isSearching;

  PlacePickerSearchResponse(this.response, this.isSearching);
}
