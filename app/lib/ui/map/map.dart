import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';

import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/deal.dart';

import 'package:rydr_app/app/events.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/map_config.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/tag.dart';

import 'package:rydr_app/services/location.dart';

import 'package:rydr_app/ui/map/blocs/map.dart';
import 'package:rydr_app/ui/map/utils.dart';
import 'package:rydr_app/ui/map/map_marker.dart';

import 'package:rydr_app/ui/map/widgets/appbar.dart';
import 'package:rydr_app/ui/map/widgets/list.dart';
import 'package:rydr_app/ui/map/widgets/deal.dart';

class MapPage extends StatefulWidget {
  MapPage({Key key}) : super(key: key);

  @override
  _MapPageState createState() => _MapPageState();
}

class _MapPageState extends State<MapPage> with TickerProviderStateMixin {
  final log = getLogger('_MapScaffoldState');

  MapBloc _bloc;

  CameraPosition _usersCameraPosition;
  CameraPosition _currentCameraPosition;

  AnimationController _controllerList;
  AnimationController _controllerPlace;
  AnimationController _controllerDetails;
  Tween<Offset> _tween = Tween(begin: Offset(0, 1), end: Offset(0, 0));

  StreamSubscription _subPlace;
  StreamSubscription _subDeal;
  StreamSubscription _subCluster;
  StreamSubscription _subMapIdle;
  StreamSubscription _subAppResume;

  GoogleMapController _mapController;

  @override
  void initState() {
    super.initState();

    /// if we have a deep link in appState then process it
    /// as this is the main / entry page of a creator for when the app starts
    /// and we'd be sending them here after they'd tap a deep link and the app was started
    if (appState.deepLink != null) {
      Future.delayed(Duration(seconds: 1), () {
        appState.processDeepLink();
      });
    }

    _bloc = MapBloc();

    _subPlace = _bloc.selectedPlace.listen((data) {
      if (data != null) {
        loadMarkers(center: false, reset: true);

        animateToPlace(data.address.latitude, data.address.longitude);
      } else {
        loadMarkers(center: false, backToList: true);

        animateToListFromPlace();
      }
    });

    _subDeal = _bloc.selectedDeal.listen((data) {
      if (data != null) {
        animateToDetails(
          data.place.address.latitude,
          data.place.address.longitude,
        );
      }
    });

    _subCluster = _bloc.selectedCluster.listen((data) {
      if (data != null) {
        animateToCluster(
          data.latitude,
          data.longitude,
        );
      }
    });

    _subAppResume =
        AppEvents.instance.eventBus.on<AppResumedEvent>().listen((event) {
      if (event.resumed) {
        /// if we've resumed, then wait for an artificial delay to let the brightness 'catch up'
        /// and then make a call to style the map with either light or dark mode
        Future.delayed(const Duration(seconds: 1), () {
          _styleMap();
        });
      }
    });

    _controllerList = AnimationController(
        vsync: this, duration: const Duration(milliseconds: 250));
    _controllerPlace = AnimationController(
        vsync: this, duration: const Duration(milliseconds: 250));
    _controllerDetails = AnimationController(
        vsync: this, duration: const Duration(milliseconds: 250));

    _init();
  }

  @override
  void dispose() {
    log.i('dispose');

    _subPlace?.cancel();
    _subDeal?.cancel();
    _subCluster?.cancel();
    _subMapIdle?.cancel();

    _subAppResume?.cancel();

    _controllerList.dispose();
    _controllerPlace.dispose();
    _controllerDetails.dispose();

    super.dispose();
  }

  /// this will run before we can render the map as we'll get the users location (or fallback)
  /// and then pass this as the initial location to the google map component
  /// we'll also pass down some references to our bloc like marker tap, pixelRatio and initial zoom
  void _init() async {
    log.i('_init');

    await _bloc.init(onMarkerTap);

    _currentCameraPosition = _bloc.initialCameraPosition;
    _usersCameraPosition = _bloc.initialCameraPosition;
  }

  /// this will style the map based on current dark mode setting
  /// its called initially when creating the map on first load, as well as potentially
  /// from when the app resumes and we detect a change to dark mode
  Future<void> _styleMap() async {
    final bool dark = Theme.of(context).brightness == Brightness.dark;

    final List<dynamic> mapStyle =
        dark ? mapConfig.values.mapStyleDark : mapConfig.values.mapStyleLight;

    await _mapController.setMapStyle(json.encode(mapStyle));

    log.d('_styleMap | styling applied dark: $dark');
  }

  /// once the map has been created we can style it and make a call to load markers
  void _onMapCreated(GoogleMapController controller) async {
    log.d('_onMapCreated | Google map is now avaialble');

    /// keep reference to the map controller, then style the map
    /// based on current dark/light mode setting of the device
    _mapController = controller;
    await _styleMap();

    _controllerList.forward();

    /// lasty, make a call to load the initial set of markers
    loadMarkers();
  }

  /// when the user moves the map/camera then this event is triggered by
  /// the google maps plugin. we call bloc which can act on it and update some streams
  void _onCameraMoveStarted() => _bloc.onCameraMoveStarted();

  /// this is called as often as every frame for when the user navigates the map we track the camera position and due do a BUG in google maps we track the
  /// camera idle event through a subscription / timer that fires after we don't detect movement on the camera for a certain period of time
  void _onCameraMove(CameraPosition cameraPosition) {
    /// update local camera position
    _currentCameraPosition = cameraPosition;

    _subMapIdle?.cancel();
    _subMapIdle = Future.delayed(Duration(milliseconds: 350))
        .asStream()
        .listen((_) => _onCameraIdle());
  }

  /// once we determine the user having stopped moving the map and the camera being 'idle'
  /// we call the bloc which can now do things like enable the search button again, geocode the new location, etc.
  void _onCameraIdle() => _bloc.onCameraIdle(_currentCameraPosition);

  /// when tapping on the map
  void _onMapTap(LatLng latLng) => _bloc.setMapTapped();

  /// when we tap a marker we'll first make a call to the bloc
  /// to potentially clear existing deal or place, then process the click on the bloc
  /// which will figure out what to do depending on the type of marker clicked
  void onMarkerTap(MapMarker marker) => _bloc.processMarkerTap(marker);

  /// when clicking a deal from either the list or the list by place
  /// currently just sets the deal on the bloc stream which we then listen and react to
  /// from listeners attached within the _startBloc function
  void onDealTap(Deal deal) => _bloc.setDeal(deal);

  /// we call this from various places on the map. it gets the current bounding box
  /// of the map controller, then calls the bloc with current coordiates to load new deals and markers
  Future<void> loadMarkers({
    bool center = true,
    bool reset,
    bool backToList,
    DealSort sort,
    DealType dealType,
    List<Tag> dealTags,
    PublisherAccount dealPublisher,
  }) async {
    /// try to get lat/lng bounds from the map right away
    /// and see if we have valid lat/lng bounds
    LatLngBounds bounds = await _mapController.getVisibleRegion();

    log.d('loadMarkers | bounding box: $bounds');

    if (_bloc.boundsAreInValid(bounds)) {
      log.w(
          'loadMarkers | Unable to get valid lat/lng bounds, moving camera to attempt to fix...');

      /// move the camera a bit, then try to get lat/lng bounds again
      /// after a short delay...
      await _mapController.animateCamera(CameraUpdate.zoomBy(0.1));

      await Future.delayed(const Duration(milliseconds: 500), () {
        log.d(
            'loadMarkers | artifical delay to wait for second bounding box attempt');
      });

      bounds = await _mapController.getVisibleRegion();

      log.d('loadMarkers | bounding box second try: $bounds');
    }

    return _bloc.loadMarkers(
      PlaceLatLngBounds(
          PlaceLatLng(bounds.southwest.latitude, bounds.southwest.longitude),
          PlaceLatLng(bounds.northeast.latitude, bounds.northeast.longitude)),
      PlaceLatLng(
        _currentCameraPosition.target.latitude,
        _currentCameraPosition.target.longitude,
      ),
      PlaceLatLng(
        _usersCameraPosition.target.latitude,
        _usersCameraPosition.target.longitude,
      ),
      pixelRatio: MediaQuery.of(context).devicePixelRatio,
      sort: sort,
      dealType: dealType,
      dealTags: dealTags,
      dealPublisher: dealPublisher,
      reset: reset,
      backToList: backToList,
    );
  }

  void redoSearch() => loadMarkers(reset: true);

  /// called when the listener (attached via _startBloc) receives a selected deal
  /// we'll animate out either the main list, or place list, then animate the map to show the selected marker
  void animateToDetails(double latitude, double longitude) async {
    if (_bloc.currentPlace != null) {
      _controllerPlace.reverse();
    } else {
      _controllerList.reverse();
    }

    /// animate in the details panel
    _controllerDetails.forward();

    _mapController.animateCamera(
      CameraUpdate.newCameraPosition(
        CameraPosition(
          target: MapUtils.offsetLatLng(
            LatLng(
              latitude,
              longitude,
            ),
            mapConfig.values.defaultOffset,
            _currentCameraPosition.zoom,
          ),
          tilt: _currentCameraPosition.tilt,
          zoom: _currentCameraPosition.zoom,
        ),
      ),
    );
  }

  /// called when the listener (attached via _startBloc) receives a selected place
  /// we'll animate out the main list and then animate the camera to the selected place icon
  void animateToPlace(double latitude, double longitude) {
    /// animate the camera to the place without updating the
    /// current camera position as we'll keep that to navigate back on clearing of the place
    _mapController
        .animateCamera(
      CameraUpdate.newCameraPosition(
        CameraPosition(
          target: MapUtils.offsetLatLng(
            LatLng(
              latitude,
              longitude,
            ),
            mapConfig.values.defaultOffset,
            _currentCameraPosition.zoom,
          ),
          tilt: _currentCameraPosition.tilt,
          zoom: _currentCameraPosition.zoom,
        ),
      ),
    )
        .then((_) {
      if (_controllerDetails.value > 0) {
        _controllerDetails.reverse();
      } else if (_controllerList.value > 0) {
        _controllerList.reverse();
      }

      /// animate to the place
      _controllerPlace.forward();
    });
  }

  /// we'll animate out the details panel and depending on what we were viewing before (place or main list)
  /// we'll animate that respective list back in to the previously saved height
  void animateToListFromDeal() {
    /// hide the details panel
    _controllerDetails.reverse();

    /// reset the parent panel to the previously stored list position
    /// for either the list or place panel
    if (_bloc.currentPlace != null) {
      _controllerPlace.forward();
    } else {
      _controllerList.forward();
    }

    Future.delayed(
        const Duration(milliseconds: 150), () => _bloc.clearSelectedDeal());
  }

  /// called when the listener (attached via _startBloc) receives NULL as selected place
  /// we'll animate out the place list, then animate back to the main list to its previously stored height
  void animateToListFromPlace() {
    /// animate out the place list
    _controllerPlace.reverse();

    /// animate the list back in
    _controllerList.forward();

    Future.delayed(
        const Duration(milliseconds: 150), () => _bloc.clearSelectedPlace());
  }

  /// called when the listener (attached via _startBlock) receives a selected cluster
  /// we'll animate the map to zoom in closer to the cluster
  void animateToCluster(double lat, double lng) {
    _mapController.animateCamera(
      CameraUpdate.newCameraPosition(
        CameraPosition(
          target: MapUtils.offsetLatLng(
            LatLng(
              lat,
              lng,
            ),
            mapConfig.values.defaultOffset,
            _currentCameraPosition.zoom + 1,
          ),
          tilt: _currentCameraPosition.tilt,
          zoom: _currentCameraPosition.zoom + 1,
        ),
      ),
    );
  }

  /// Tapping the location arrow icon on the map will either
  /// a)  take the user directly to their current location (if location services enabled), or
  /// b)  make a request for permission, prompting and then starting location services if never asked bfore,
  ///     or give modal with info on how to turn them on if we've previously asked the user and they had declined
  void goToMyLocation() async {
    final CurrentLocationResponse res = await _bloc.getUsersLocation();

    if (res.hasLocationService) {
      _usersCameraPosition = res.position;

      _mapController
          .animateCamera(CameraUpdate.newCameraPosition(_usersCameraPosition));
    } else {
      LocationService.getInstance().handleLocationServicesOff(context);
    }
  }

  /// Navigates the map to an available location and starts a search. E.g. somewhere the rydr is currently available
  /// from a list of available location settings store within remote config and assets/config/map_locations.json
  void goToLocation(AvailableLocation loc) async {
    /// create a new camera position, using the offset center
    /// for the selected location where we want to navigate to
    _currentCameraPosition = CameraPosition(
      target: MapUtils.offsetLatLng(
        LatLng(
          loc.center.latitude,
          loc.center.longitude,
        ),
        mapConfig.values.defaultOffset,
        loc.zoom ?? mapConfig.values.defaultZoom,
      ),
      zoom: loc.zoom ?? mapConfig.values.defaultZoom,
      tilt: loc.tilt ?? mapConfig.values.defaultTilt,
    );

    /// move the map to the current' offset camera position
    await _mapController.animateCamera(
      CameraUpdate.newCameraPosition(_currentCameraPosition),
    );

    /// start a new search with the desired center of the available location
    loadMarkers(center: false);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: Stack(
          children: <Widget>[
            _buildMap(),
            _buildList(),
            _buildPlace(),
            _buildDetails(),
            Positioned(
              top: 0,
              left: 0,
              child: InfluencerAppBar(),
            ),
            // UpgraderCheck()
            // / Not implemented yet => UpgraderCheck()
          ],
        ),
      );

  Widget _buildMap() => StreamBuilder<bool>(
        stream: _bloc.initializing,
        builder: (context, snapshot) {
          final bool initializing =
              snapshot.data == null || snapshot.data == true;

          return initializing
              ? Container()
              : StreamBuilder<Map<MarkerId, Marker>>(
                  stream: _bloc.markers,
                  builder: (context, snapshot) {
                    log.d('_buildMap');

                    var markers = snapshot;

                    return Container(
                      height: MediaQuery.of(context).size.height,
                      width: MediaQuery.of(context).size.width,
                      child: StreamBuilder<bool>(
                          stream: _bloc.locationServices,
                          builder: (context, snapshot) {
                            final bool locationServices =
                                snapshot.data == null || snapshot.data == true;

                            return GoogleMap(
                              mapType: MapType.normal,
                              initialCameraPosition: _currentCameraPosition,
                              onMapCreated: _onMapCreated,
                              onCameraMoveStarted: _onCameraMoveStarted,
                              onCameraMove: _onCameraMove,
                              onTap: _onMapTap,
                              myLocationButtonEnabled: false,
                              myLocationEnabled: locationServices,
                              compassEnabled: false,
                              markers: (markers.data != null)
                                  ? Set.of(markers.data.values)
                                  : Set(),
                            );
                          }),
                    );
                  },
                );
        },
      );

  Widget _buildList() => SizedBox.expand(
        child: SlideTransition(
          position: _tween.animate(_controllerList),
          child: MapList(
            mapBloc: _bloc,
            loadList: loadMarkers,
            redoSearch: redoSearch,
            goToDeal: onDealTap,
            goToLocation: goToLocation,
            goToMyLocation: goToMyLocation,
            goBackToList: animateToListFromPlace,
            isPlaceList: false,
          ),
        ),
      );

  Widget _buildPlace() => SizedBox.expand(
        child: SlideTransition(
          position: _tween.animate(_controllerPlace),
          child: MapList(
            mapBloc: _bloc,
            loadList: loadMarkers,
            redoSearch: redoSearch,
            goToDeal: onDealTap,
            goToLocation: goToLocation,
            goToMyLocation: goToMyLocation,
            goBackToList: animateToListFromPlace,
            isPlaceList: true,
          ),
        ),
      );

  Widget _buildDetails() => SizedBox.expand(
        child: SlideTransition(
          position: _tween.animate(_controllerDetails),
          child: InfluencerDeal(
            mapBloc: _bloc,
            onClose: animateToListFromDeal,
          ),
        ),
      );
}
