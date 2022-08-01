import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:location/location.dart' as loc;
import 'package:location_permissions/location_permissions.dart';
import 'package:rydr_app/app/analytics.dart';

import 'package:rydr_app/app/map_config.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/events.dart';

import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/device_settings.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/services/device_settings.dart';

import 'package:rydr_app/ui/map/utils.dart';
import 'package:rydr_app/ui/shared/widgets/dialogs.dart';

class LocationService {
  final log = getLogger('LocationService');
  static final LocationService _singleton = LocationService._internal();
  static LocationService getInstance() => _singleton;

  StreamSubscription _subAppResume;

  LocationService._internal() {
    log.i('_internal | attaching eventBus AppResumed event listener');

    /// if we get a resume event then trigger another iOs permissions prompt
    /// so if a user switches to ios settings and turns notifications on/off we can re-check
    /// as soon as the app gets back in the foreground (is resumed)
    _subAppResume =
        AppEvents.instance.eventBus.on<AppResumedEvent>().listen((event) {
      if (event.resumed) {
        checkPermissionStatus();
      }
    });
  }

  dispose() {
    _subAppResume?.cancel();
  }

  /// can be called from another widget, will try to either request permissions for the first time
  /// or otherwise, will show a dialog for the user to go to settings to enable location permissions
  Future<PermissionStatus> requestPermission() async {
    /// if we request permission then we'll update our onboarding flag
    /// indicating that we've now asked for permissions, regardless of the resopnse from the user
    _updateOnboardingAsked();

    final permissionStatus = await LocationPermissions().requestPermissions();

    AppAnalytics.instance.logEvent(permissionStatus == PermissionStatus.denied
        ? 'perms_location_declined'
        : 'perms_location_accepted');

    return permissionStatus;
  }

  void checkPermissionStatus() async {
    if (appState.onboardSettings.askedLocation) {
      final permissionStatus =
          await LocationPermissions().checkPermissionStatus();

      AppEvents.instance.eventBus.fire(
        LocationPermissionStatusChangeEvent(permissionStatus),
      );
    }
  }

  void handleLocationServicesOff(BuildContext context) async {
    /// if we've never asked the user for location permission then now
    /// is the time and we'll make a request for permissions
    if (appState.onboardSettings.askedLocation) {
      showSharedModalAlert(
        context,
        Text("RYDR needs your location"),
        content: Text("Please enable location services in your Settings"),
        actions: [
          ModalAlertAction(
            isDefaultAction: true,
            label: "Open Settings",
            onPressed: () {
              Navigator.of(context).pop();
              LocationPermissions().openAppSettings();
            },
          ),
          ModalAlertAction(
            label: "Cancel",
            onPressed: () {
              Navigator.of(context).pop();
            },
          ),
        ],
      );
    } else {
      await requestPermission();
    }
  }

  Future<CurrentLocationResponse> getCurrentLocation() async {
    /// if we've never asked for location services on this device, then return back
    /// the default fallback camera position from our map configuration, and an indicator
    /// that location services is not turned "on"
    if (!appState.onboardSettings.askedLocation) {
      log.i(
          'getCurrentLocation | never asked, so returning default fallback location');

      return CurrentLocationResponse(
        mapConfig.values.initialCameraPosition,
        false,
      );
    }

    var location = loc.Location();

    /// Platform messages may fail, so we use a try/catch PlatformException.
    try {
      final currentLocation = await location.getLocation();

      /// add to app state as the users (device) last known location
      /// if we have a location that we got because location services was turned on
      appState.setLastLocation(
        PlaceLatLng(
          currentLocation.latitude,
          currentLocation.longitude,
        ),
      );

      log.i('getCurrentLocation | $currentLocation');

      return CurrentLocationResponse(
        CameraPosition(
          target: MapUtils.offsetLatLng(
            LatLng(
              currentLocation.latitude,
              currentLocation.longitude,
            ),
            mapConfig.values.defaultOffset,
            mapConfig.values.defaultZoom,
          ),
          zoom: mapConfig.values.defaultZoom,
          tilt: mapConfig.values.defaultTilt,
        ),
        true,
      );
    } on PlatformException catch (error) {
      if (error.code == 'PERMISSION_DENIED') {
        log.e(
            'getCurrentLocation | denied users location, returning fallback location',
            error);
      }

      return CurrentLocationResponse(
        mapConfig.values.initialCameraPosition,
        false,
      );
    }
  }

  /// is called from each location change event (once enabled)
  /// and will send the location to the server to save
  void saveCoordinates(PlaceLatLng location) async {
    /// ensure we have a master user and a location
    if (appState.masterUser != null && location != null) {
      AppApi.instance.put('accounts/location', body: {
        "latitude": location.latitude,
        "longitude": location.longitude,
      });
    }
  }

  /// if we've not previously asked the user for permissions
  /// then update that onboarding flag now since we'd have asked by now
  void _updateOnboardingAsked() {
    if (!appState.onboardSettings.askedLocation) {
      OnboardSettings settings = appState.onboardSettings;
      settings.askedLocation = true;

      DeviceSettings.saveOnboardSettings(settings);

      log.i('_updateOnboardingAsked');
    }
  }
}

/// When we ask for location using the plugin we return back this model
/// that will hold the current camera position based on the location we identified
/// (either default fallback or actual users location), as well as the current status
/// of location services being enabled or denied
class CurrentLocationResponse {
  CameraPosition position;
  bool hasLocationService;

  CurrentLocationResponse(
    this.position,
    this.hasLocationService,
  );
}
