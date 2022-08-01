import 'package:rydr_app/models/place.dart';

class FacebookPlacesSearchRequest {
  String query;
  PlaceLatLng latLng;
  PlaceLatLngBounds boundingBox;
  double miles;

  FacebookPlacesSearchRequest(
    this.query, {
    this.latLng,
    this.boundingBox,
    this.miles,
  });

  Map<String, dynamic> toMap() {
    Map<String, dynamic> params = {"query": query.trim()};

    if (latLng != null) {
      params['latitude'] = latLng.latitude.toString();
      params['longitude'] = latLng.longitude.toString();
    }
    if (boundingBox != null) {
      params['boundingBox'] = boundingBox.toQueryString();
    }

    if (miles != null) {
      params['miles'] = miles.toString();
    }

    return params;
  }
}
