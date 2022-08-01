import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/map_config.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/responses/places.dart';
import 'package:rydr_app/services/place.dart';

class DealAddPlacesBloc {
  final _places = BehaviorSubject<List<Place>>();

  void dispose() {
    _places.close();
  }

  BehaviorSubject<List<Place>> get places => _places.stream;

  void loadPlaces(Place existingPlace, Function setPlace) async {
    final PlacesResponse res =
        await PlaceService.getPublisherPlaces(appState.currentProfile.id);

    if (res.error == null && res.models.isNotEmpty) {
      /// by default, sort places by the primary before adding to the stream
      final List<Place> sorted = List.from(res.models
        ..sort((a, b) =>
            _boolToInt(b.isPrimary).compareTo(_boolToInt(a.isPrimary))));

      /// if we have an incoming deal (from a copy template or continuation of a draft)
      /// then validate that this place is still valid, and if so make it the first in the list
      /// of places in our stream and set it as the current place for the deal
      ///
      /// otherwise, use the first place in our sorted list (which should be the primary one)
      /// and set that as the place for the new deal
      if (existingPlace != null &&
          sorted.where((Place p) => p.id == existingPlace.id).isNotEmpty) {
        setPlace(existingPlace);

        /// make the place the first item in the list before adding it to the stream
        sorted
          ..removeWhere((Place p) => p.id == existingPlace.id)
          ..insert(0, existingPlace);
      } else {
        setPlace(sorted[0]);
      }

      /// now, add places to stream
      _places.sink.add(sorted);
    } else {
      _places.sink.add([]);
    }
  }

  /// check if the place for the deal is valid given our current set of locations
  /// where we are supporting deals in, will return true / false to indicate availability
  bool isPlaceIsInValidRegion(Place place) {
    if (place == null ||
        place.address == null ||
        place.address.latitude == null ||
        place.address.longitude == null) {
      return false;
    } else {
      PlaceLatLng latLng = PlaceLatLng(
        place.address.latitude,
        place.address.longitude,
      );

      for (AvailableLocation loc in mapConfig.values.availableLocations) {
        if (loc.bounds.contains(latLng)) {
          return true;
        }
      }

      return false;
    }
  }

  int _boolToInt(bool value) => value == true ? 1 : 0;
}
