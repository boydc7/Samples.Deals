import 'dart:typed_data';
import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:fluster/fluster.dart';
import 'package:meta/meta.dart';
import 'package:flutter/services.dart' show ByteData, rootBundle;
import 'package:google_maps_flutter/google_maps_flutter.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/place.dart';

import 'package:rydr_app/app/theme.dart';

class MapMarker extends Clusterable {
  String placeName;
  int placeId;
  int dealId;
  bool autoApprove;
  bool ageRestricted;
  bool isInvite;
  bool isEvent;
  bool isVirtual;

  MapMarker({
    @required this.placeName,
    @required this.placeId,
    @required this.dealId,
    @required latitude,
    @required longitude,
    this.ageRestricted,
    this.isInvite,
    this.isVirtual,
    this.isEvent,
    this.autoApprove,
    isCluster = false,
    clusterId,
    pointsSize,
    markerId,
    childMarkerId,
  }) : super(
          latitude: latitude,
          longitude: longitude,
          isCluster: isCluster,
          clusterId: clusterId,
          pointsSize: pointsSize,
          markerId: markerId,
          childMarkerId: childMarkerId,
        );
}

class MapMarkerHelpers {
  static String _assetMarkerIcon = "assets/marker-deal.png";
  static String _assetMarkerVirtual = "assets/marker-virtual.png";
  static String _assetMarkerEvent = "assets/marker-event.png";
  static String _assetMarkerEventInvite = "assets/marker-eventInvite.png";
  static String _assetMarkerCluster = "assets/marker-blank.png";
  static String _assetMarkerPlace = "assets/marker-place.png";
  static String _assetMarkerAgeRestricted = "assets/marker-ageRestricted.png";
  static String _assetMarkerIconInvite = "assets/marker-invite.png";

  /// given a list of deals, this will generate the map of markers that
  /// are in the format needed for the fluster class to generate the 'fluster'
  static Map<String, MapMarker> generateFlusterMarkers(List<Deal> dealsToMap) {
    Map<String, MapMarker> markers = <String, MapMarker>{};

    for (int x = 0; x < dealsToMap.length; x++) {
      final Deal deal = dealsToMap[x];

      /// add the marker to our map
      markers[deal.id.toString()] = MapMarker(
        placeName: deal.place.name,
        placeId: deal.place.id,
        dealId: deal.id,
        autoApprove: deal.autoApproveRequests,
        ageRestricted: deal.minAge == 21,
        isInvite: deal.isInvited,
        markerId: deal.id.toString(),
        latitude: deal.place.address.latitude,
        longitude: deal.place.address.longitude,
        isVirtual: deal.dealType == DealType.Virtual,
        isEvent: deal.dealType == DealType.Event,
      );
    }

    return markers;
  }

  /// oncwe have a generated a 'fluster' we will use this to create the final
  /// map of icons suitable for display on the google maps component
  static Future<Map<MarkerId, Marker>> generateMapMarkers({
    @required Fluster fluster,
    @required int currentZoom,
    @required double devicePixelRatio,
    @required Function onMarkerTap,
    Deal selectedDeal,
    Place selectedPlace,
  }) async {
    // Finalize the markers to display on the map.
    Map<MarkerId, Marker> markers = Map();

    /// Get the clusters at the current zoom level.
    /// NOTE: as if yet, i'm not sure how to make the cluster work... but seems to function without it
    final List<MapMarker> clusters =
        fluster.clusters([-180, -85, 180, 85], currentZoom);

    /// Get base descriptor for single markers, both regular, and auto approve
    /// so that we don't have to do this for each marker in the set
    final BitmapDescriptor icon = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerIcon);

    final BitmapDescriptor iconSelected = await _generateMarkerDescriptor(
        currentZoom + 5, devicePixelRatio, _assetMarkerIcon);

    final BitmapDescriptor iconAgeRestricted = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerAgeRestricted);

    final BitmapDescriptor iconSelectedAgeRestricted =
        await _generateMarkerDescriptor(
            currentZoom + 5, devicePixelRatio, _assetMarkerAgeRestricted);

    final BitmapDescriptor iconVirtual = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerVirtual);

    final BitmapDescriptor iconVirtualSelected =
        await _generateMarkerDescriptor(
            currentZoom + 5, devicePixelRatio, _assetMarkerVirtual);

    final BitmapDescriptor iconEvent = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerEvent);

    final BitmapDescriptor iconEventSelected = await _generateMarkerDescriptor(
        currentZoom + 5, devicePixelRatio, _assetMarkerEvent);

    final BitmapDescriptor iconEventInvite = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerEventInvite);

    final BitmapDescriptor iconEventInviteSelected =
        await _generateMarkerDescriptor(
            currentZoom + 5, devicePixelRatio, _assetMarkerEventInvite);

    final BitmapDescriptor iconPlace = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerPlace);

    final BitmapDescriptor iconSelectedPlace = await _generateMarkerDescriptor(
        currentZoom + 5, devicePixelRatio, _assetMarkerPlace);

    final BitmapDescriptor iconInvite = await _generateMarkerDescriptor(
        currentZoom, devicePixelRatio, _assetMarkerIconInvite);

    final BitmapDescriptor iconSelectedInvite = await _generateMarkerDescriptor(
        currentZoom + 5, devicePixelRatio, _assetMarkerIconInvite);

    /// Get base image of the cluster icon, then pass it to the generator
    /// with each cluster pointsize within the loop of all markers that are clusters
    final ui.Image iconCluster =
        await _getClusterMarkerIcon(currentZoom, devicePixelRatio);

    for (MapMarker feature in clusters) {
      BitmapDescriptor bitmapDescriptor;

      if (feature.isCluster) {
        /// check if this clusters children are all part of the same place
        /// which would determine what icon we render for it
        final List<MapMarker> markersInCluster =
            fluster.points(feature.clusterId);
        final MapMarker firstMarker = markersInCluster[0];
        final bool samePlace = markersInCluster.length ==
            markersInCluster
                .where((MapMarker m) => m.placeId == firstMarker.placeId)
                .length;

        if (samePlace) {
          /// if we have a selected deal, part of the same place
          /// we'll still showh the selected icon of the place
          if (selectedDeal != null &&
              markersInCluster
                      .where((MapMarker m) => m.dealId == selectedDeal.id)
                      .length >
                  0) {
            bitmapDescriptor = iconSelectedPlace;
          } else {
            bitmapDescriptor =
                selectedPlace != null && selectedPlace.id == firstMarker.placeId
                    ? iconSelectedPlace
                    : iconPlace;
          }
        } else {
          bitmapDescriptor = await _getClusterDescriptor(
            iconCluster,
            feature.pointsSize,
          );
        }
      } else {
        if (selectedDeal != null && feature.dealId == selectedDeal.id) {
          bitmapDescriptor = feature.isEvent && feature.isInvite
              ? iconEventInviteSelected
              : feature.isEvent
                  ? iconEventSelected
                  : feature.isInvite
                      ? iconSelectedInvite
                      : feature.isVirtual
                          ? iconVirtualSelected
                          : feature.ageRestricted
                              ? iconSelectedAgeRestricted
                              : iconSelected;
        } else {
          bitmapDescriptor = feature.isEvent && feature.isInvite
              ? iconEventInvite
              : feature.isEvent
                  ? iconEvent
                  : feature.isInvite
                      ? iconInvite
                      : feature.isVirtual
                          ? iconVirtual
                          : feature.ageRestricted ? iconAgeRestricted : icon;
        }
      }

      var marker = Marker(
        consumeTapEvents: true,
        onTap: () => onMarkerTap(feature),
        markerId: MarkerId(feature.markerId),
        position: LatLng(feature.latitude, feature.longitude),
        icon: bitmapDescriptor,
      );

      markers.putIfAbsent(MarkerId(feature.markerId), () => marker);
    }

    return markers;
  }

  static Future<BitmapDescriptor> _generateMarkerDescriptor(
      int currentMapZoom, double pixelRatio, String assetPath) async {
    return BitmapDescriptor.fromBytes(await _getBytesFromAsset(
      assetPath,
      _getMarkerSize(currentMapZoom, pixelRatio),
    ));
  }

  static Future<ui.Image> _getClusterMarkerIcon(
    int zoom,
    double pixelRatio,
  ) async {
    /// this will be the size of the marker, based on the current zoom on the map
    final int markerSize = _getMarkerSize(zoom, pixelRatio);

    /// load the cluster icon from assets, specify the target width
    Uint8List iconData =
        await _getBytesFromAsset(_assetMarkerCluster, markerSize);

    ui.Codec codec = await ui.instantiateImageCodec(
      iconData,
      targetWidth: markerSize,
    );
    ui.FrameInfo fi = await codec.getNextFrame();

    return fi.image;
  }

  static Future<BitmapDescriptor> _getClusterDescriptor(
    ui.Image clusterImg,
    int pointSize,
  ) async {
    /// start a picture recorded which will track progress on the things we draw on the canvas
    final ui.PictureRecorder pictureRecorder = ui.PictureRecorder();
    final Canvas canvas = Canvas(pictureRecorder);

    /// cap the pointsize at 9, add "+" if we have more
    String numberText = pointSize > 9 ? "9+" : pointSize.toString();

    /// draw the cluster marker on the canvas, no offset, no painter
    canvas.drawImage(clusterImg, Offset(0, 0), Paint());

    /// create a new text painer
    TextPainter painter = TextPainter(
      textDirection: TextDirection.ltr,
      maxLines: 1,
    );

    /// paint the text which includes the pointsize (our size of the cluster)
    /// make the font size a percentage of the cluster icons' width
    painter.text = TextSpan(
      text: numberText,
      style: TextStyle(
        fontSize: clusterImg.width / 1.8,
        color: AppColors.white,
        fontWeight: FontWeight.w900,
      ),
    );
    painter.layout();

    Offset textOffset = Offset(
      (clusterImg.width * 0.5) - painter.width * 0.5,
      (clusterImg.width * 0.5) - painter.height * 0.5,
    );

    painter.paint(
      canvas,
      textOffset,
    );

    final img = await pictureRecorder.endRecording().toImage(
          clusterImg.width,
          clusterImg.height,
        );
    final data = await img.toByteData(format: ui.ImageByteFormat.png);
    return BitmapDescriptor.fromBytes(data.buffer.asUint8List());
  }

  /// gets marker icon from asset given the path and width
  /// we can use selected vs. regular icon and adjust size based on current zoom
  static Future<Uint8List> _getBytesFromAsset(
    String path,
    int width,
  ) async {
    ByteData data = await rootBundle.load(path);
    ui.Codec codec = await ui.instantiateImageCodec(
      data.buffer.asUint8List(),
      targetWidth: width,
    );
    ui.FrameInfo fi = await codec.getNextFrame();
    return (await fi.image.toByteData(format: ui.ImageByteFormat.png))
        .buffer
        .asUint8List();
  }

  /// calculate a marker size based on the current zoom of the map
  static int _getMarkerSize(int zoom, double pixelRatio) {
    if (pixelRatio < 3) {
      return (zoom * 6.5).ceil();
    } else if (pixelRatio < 3.5) {
      return (zoom * 9.5).ceil();
    }

    return (zoom * 6.5).ceil();
  }
}
