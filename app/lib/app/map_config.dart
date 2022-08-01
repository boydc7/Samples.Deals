import 'dart:core';
import 'dart:convert';

import 'package:flutter/services.dart' show rootBundle;
import 'package:google_maps_flutter/google_maps_flutter.dart';

import 'config.dart';

import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/ui/map/utils.dart';

import 'log.dart';

/// Application-level global variable
MapConfig mapConfig = MapConfig();

class MapConfig {
  final log = getLogger('MapConfig');
  static final MapConfig _mapConfig = MapConfig._internal();

  MapConfigValues values;

  factory MapConfig() {
    return _mapConfig;
  }

  MapConfig._internal();

  /// TO DO:
  /// we should guard against loading this twice...
  /// should also change this to lazy-load singleton
  initValues() async {
    log.i('initValues');

    Map<String, dynamic> mapConfigAsMap = json
        .decode(await rootBundle.loadString("assets/config/map_config.json"));

    Map<String, dynamic> mapAvailableLocationsAsMap = json.decode(
        await rootBundle
            .loadString("assets/config/map_available_locations.json"));

    Map<String, dynamic> mapStyleAsMap = json
        .decode(await rootBundle.loadString("assets/config/map_style.json"));

    values = MapConfigValues(
      defaultDetailsOffset: mapConfigAsMap['details']['offset'].toDouble(),
      defaultDetailsTilt: mapConfigAsMap['details']['tilt'].toDouble(),
      defaultDetailsZoom: mapConfigAsMap['details']['zoom'].toDouble(),
      defaultLat: mapConfigAsMap['lat'].toDouble(),
      defaultLng: mapConfigAsMap['lng'].toDouble(),
      defaultOffset: mapConfigAsMap['offset'].toDouble(),
      defaultTilt: mapConfigAsMap['tilt'].toDouble(),
      defaultZoom: mapConfigAsMap['zoom'].toDouble(),
      mapStyleDark: mapStyleAsMap['dark'],
      mapStyleLight: mapStyleAsMap['light'],
      availableLocations: AvailableLocation.jsonToAvailableLocations(
          mapAvailableLocationsAsMap['locations']),
    );

    values.initialCameraPosition = CameraPosition(
      target: MapUtils.offsetLatLng(
        LatLng(
          values.defaultLat,
          values.defaultLng,
        ),
        values.defaultOffset,
        values.defaultZoom,
      ),
      zoom: values.defaultZoom,
      tilt: values.defaultTilt,
    );

    var remoteConfig = await AppConfig.instance.remoteConfig;

    if (remoteConfig == null) {
      log.w('initValues | RemoteConfig unavailable - is it initialized?');

      return;
    }

    try {
      /// get map configuration which includes default & details zoom levels etc.
      /// mayp style, and available regions, cities, and neighborhoods
      /// NOTE! these should be kept in synch between firebase remoteconfig and assets/config/map_* files
      mapConfigAsMap = json.decode(remoteConfig.getString('map_config'));
      mapStyleAsMap = json.decode(remoteConfig.getString('map_style'));
      mapAvailableLocationsAsMap =
          json.decode(remoteConfig.getString('map_available_locations'));

      /// defaults for list view
      values.defaultLat = mapConfigAsMap['lat'].toDouble();
      values.defaultLng = mapConfigAsMap['lng'].toDouble();
      values.defaultZoom = mapConfigAsMap['zoom'].toDouble();
      values.defaultTilt = mapConfigAsMap['tilt'].toDouble();
      values.defaultOffset = mapConfigAsMap['offset'].toDouble();

      /// defaults for details view
      values.defaultDetailsZoom = mapConfigAsMap['details']['zoom'].toDouble();
      values.defaultDetailsTilt = mapConfigAsMap['details']['tilt'].toDouble();
      values.defaultDetailsOffset =
          mapConfigAsMap['details']['offset'].toDouble();

      /// default map styles for dark/light
      values.mapStyleDark = mapStyleAsMap['dark'];
      values.mapStyleLight = mapStyleAsMap['light'];

      /// map style configuration
      values.availableLocations = AvailableLocation.jsonToAvailableLocations(
          mapAvailableLocationsAsMap['locations']);

      values.initialCameraPosition = CameraPosition(
        target: MapUtils.offsetLatLng(
          LatLng(
            values.defaultLat,
            values.defaultLng,
          ),
          values.defaultOffset,
          values.defaultZoom,
        ),
        zoom: values.defaultZoom,
        tilt: values.defaultTilt,
      );

      log.i('initValues | remoteConfig fetch completed');
    } catch (exception) {
      log.e(
          'initValues | Unable to use remote config. Cached or defaults will be used',
          exception);
    }
  }
}

class MapConfigValues {
  /// defaults for the map if we can't determine the users location
  /// and are unable to get firebase config as well
  double defaultZoom;
  double defaultLat;
  double defaultLng;
  double defaultTilt;

  /// this should represent the offset in pixels we want to adjust the
  /// camera position 'north' by, this will likely need to be calculated
  /// using the device size / pixel ration etc.
  double defaultOffset;

  /// defaults for when viewing deal details
  double defaultDetailsZoom;
  double defaultDetailsOffset;
  double defaultDetailsTilt;

  List<AvailableLocation> availableLocations;
  List<dynamic> mapStyleDark;
  List<dynamic> mapStyleLight;
  CameraPosition initialCameraPosition;

  MapConfigValues({
    this.defaultZoom,
    this.defaultLat,
    this.defaultLng,
    this.defaultTilt,
    this.defaultOffset,
    this.defaultDetailsOffset,
    this.defaultDetailsTilt,
    this.defaultDetailsZoom,
    this.availableLocations,
    this.mapStyleDark,
    this.mapStyleLight,
    this.initialCameraPosition,
  });
}
