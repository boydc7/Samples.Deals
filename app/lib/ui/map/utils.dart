import 'dart:core';
import 'dart:math' as math;

import 'package:google_maps_flutter/google_maps_flutter.dart';

class MapUtils {
  /// offset a location on the map by a certain amount of pixels from the top
  /// this will offset the 'center' of the map into view since we have a bottom panel
  static LatLng offsetLatLng(
    LatLng latLng,
    double offsetFromTop,
    double zoom,
  ) {
    LatLng offsetPoint =
        LatLng(offsetFromTop / math.pow(2, zoom), 0 / math.pow(2, zoom));

    return LatLng(
      latLng.latitude - offsetPoint.latitude,
      latLng.longitude + offsetPoint.longitude,
    );
  }

  static LatLng centerOfGroup(List<LatLng> group) {
    double x = 0;
    double y = 0;
    double z = 0;

    group.forEach((LatLng latLng) {
      var latitude = latLng.latitude * math.pi / 180;
      var longitude = latLng.longitude * math.pi / 180;

      x += math.cos(latitude) * math.cos(longitude);
      y += math.cos(latitude) * math.sin(longitude);
      z += math.sin(latitude);
    });

    int total = group.length;

    x = x / total;
    y = y / total;
    z = z / total;

    var centralLongitude = math.atan2(y, x);
    var centralSquareRoot = math.sqrt(x * x + y * y);
    var centralLatitude = math.atan2(z, centralSquareRoot);

    return LatLng(
        centralLatitude * 180 / math.pi, centralLongitude * 180 / math.pi);
  }

  static double rad2degr(rad) {
    return rad * 180 / math.pi;
  }

  static double degr2rad(degr) {
    return degr * math.pi / 180;
  }
}
