import 'dart:async';

import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/state.dart';

import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/responses/places.dart';

import 'package:rydr_app/services/place.dart';

class PlacesBloc {
  final loading = BehaviorSubject<bool>.seeded(true);
  final places = BehaviorSubject<List<Place>>.seeded([]);

  dispose() {
    loading.close();
    places.close();
  }

  Future<bool> deletePlace(Place place) async {
    final res = await PlaceService.deletePublisherPlace(
        appState.currentProfile.id, place);

    if (res.error == null) {
      List<Place> newPlaces = places.value;
      newPlaces.remove(place);

      places.add(newPlaces);

      AppAnalytics.instance.logScreen('profile/settings/places/deleted');
    }

    return res.error == null;
  }

  Future<bool> markAsPrimary(Place place) async {
    final PlaceUpsertResponse res = await PlaceService.savePublisherPlace(
        appState.currentProfile.id, Place(id: place.id), true);

    if (res.error == null) {
      List<Place> newPlaces = places.value;
      newPlaces.forEach((Place p) {
        if (p.id == place.id) {
          p.isPrimary = true;
        } else {
          p.isPrimary = false;
        }
      });

      /// sort places result by primary location first
      places.add(
        newPlaces
          ..sort(
            (a, b) => _boolToInt(b.isPrimary).compareTo(
              _boolToInt(a.isPrimary),
            ),
          ),
      );

      AppAnalytics.instance.logScreen('profile/settings/places/added');
    }

    return res.error == null;
  }

  void loadPlaces() async {
    loading.add(true);

    final PlacesResponse placesResponse =
        await PlaceService.getPublisherPlaces(appState.currentProfile.id);

    if (placesResponse.error == null &&
        placesResponse.models != null &&
        placesResponse.models.isEmpty == false) {
      /// sort places result by primary location first
      places.add(placesResponse.models
        ..where((Place p) => p.name != null && p.address != null)
        ..sort(
          (a, b) => _boolToInt(b.isPrimary).compareTo(
            _boolToInt(a.isPrimary),
          ),
        ));
    } else {
      places.add([]);
    }

    loading.add(false);
  }

  int _boolToInt(bool value) => value == true ? 1 : 0;
}
