import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:fluster/fluster.dart';
import 'package:geocoder/geocoder.dart';
import 'package:location_permissions/location_permissions.dart';
import 'package:rxdart/rxdart.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:rydr_app/app/analytics.dart';
import 'package:rydr_app/app/events.dart';

import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/map_config.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/requests/business_search.dart';
import 'package:rydr_app/models/responses/deals.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/models/tag.dart';

import 'package:rydr_app/services/deals.dart';
import 'package:rydr_app/services/deal.dart';

import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/requests/deals.dart';
import 'package:rydr_app/services/location.dart';
import 'package:rydr_app/services/search.dart';

import 'package:rydr_app/ui/map/map_marker.dart';

class MapBloc {
  static const maxZoom = 21;
  static const thumbnailWidth = 100;
  static const pageSize = 25;

  final _log = getLogger('MapBloc');

  /// public so we can access these from other widgets
  int pageIndex = 0;
  int pageIndexPlace = 0;
  bool hasResults = false;
  bool hasMoreResults = false;
  bool hasResultsPlace = false;
  bool hasMoreResultsPlace = false;

  /// utility counter of how many map marker loads we've performed thus far
  /// not much to do with it at this time other than check for first load
  int fetchCount = 0;

  /// we pass the marker-click function here, because
  /// this is where we're building the markers (for now)
  Function onMarkerTap;

  /// will keep track of the current lat/lng of where we're searching, and the current zoom, and pixel ratio
  /// this can also be accessed by child widgets that access this bloc (e.g. no results widget)
  PlaceLatLng currentLatLngSearch;
  DealSort currentSort;
  DealType currentDealType;
  List<Tag> currentDealTags;
  PublisherAccount currentDealPublisher;
  int currentZoom;

  MapListNoResultsOptions noResultOptions;

  double _devicePixelRatio;

  /// when we first 'init' the bloc we get the users current camera position
  /// and set it as this public property on the bloc
  CameraPosition initialCameraPosition;

  /// we keep an internal map list response
  MapListResponse _listResponse = MapListResponse();
  MapListResponse _placeResponse = MapListResponse();

  /// streams that widgets can listen to that'll update them on whether we are
  /// loading, have location services turned on/off and if we should show redo search
  /// and whether or not we're looking at a selected place (e.g. a cluster of deals all at the same placeId)
  final initializing = BehaviorSubject<bool>.seeded(true);
  final loading = BehaviorSubject<bool>.seeded(true);
  final loadingPlace = BehaviorSubject<bool>.seeded(true);
  final locationServices = BehaviorSubject<bool>.seeded(true);
  final redoSearch = BehaviorSubject<bool>.seeded(false);
  final mapTapped = BehaviorSubject<bool>.seeded(false);
  final selectedPlace = BehaviorSubject<Place>();
  final selectedDeal = BehaviorSubject<Deal>();
  final selectedCluster = BehaviorSubject<MapMarker>();
  final mapList = BehaviorSubject<MapListResponse>();
  final mapListPlace = BehaviorSubject<MapListResponse>();
  final markers = BehaviorSubject<Map<MarkerId, Marker>>();
  final cameraZoom = BehaviorSubject<double>();
  final headerList = BehaviorSubject<MapListHeader>();
  final headerPlace = BehaviorSubject<MapListHeader>();
  final headerPublisher = BehaviorSubject<MapListHeader>();

  /// a stream of publishers we search by using 'tags' that we can then
  /// 'inject' and/or otherwise display on the map list results
  final _publishers = BehaviorSubject<PublisherAccountsResponse>();

  /// when we have no results we can populate available locations
  /// for display on the no results list page for the user to choose & search

  StreamSubscription _subLocationPermissions;

  Map<MarkerId, Marker> get mapMarkers => markers.value;

  /// Fluster!
  Fluster<MapMarker> _fluster;

  Place get currentPlace => selectedPlace.value;
  Deal get currentDeal => selectedDeal.value;

  MapBloc() {
    _log.d('MapBloc | attaching event listeners');

    /// subscribe to changes to location permissions where we simply
    /// update a local bool flag to true/false which updates the UI location button
    _subLocationPermissions = AppEvents.instance.eventBus
        .on<LocationPermissionStatusChangeEvent>()
        .listen((event) {
      locationServices.sink.add(event.status == PermissionStatus.granted ||
          event.status == PermissionStatus.restricted);
    });

    /// listen for deal being added to the stream which would indicate
    /// viewing deal details. we'll track this as a 'click' metric, then re-build markers
    /// we'll also set this as a deal click in analytics
    selectedDeal.listen((Deal selectedDeal) {
      if (selectedDeal != null) {
        AppAnalytics.instance.logScreen('deal/view');

        DealService.trackDealMetric(
          selectedDeal.id,
          DealMetricType.Clicked,
        );
      }

      /// call to rebuild markers anytime we have a deal or removed a deal
      /// from the stream selectedDeal
      _displayMarkers();
    });

    selectedPlace.listen((Place place) {
      if (place != null) {
        headerPlace.sink.add(MapListHeader(currentPlace.name ?? "Explore RYDRs",
            currentPlace.addressForDisplay()));
      }
    });

    /// listen for changes to the camera zoom on the map
    /// we can check the last zoom vs. the new zoom level and if the difference meets our threshold
    /// we run re-clustering of the map markers on the available markers
    cameraZoom.listen((zoom) {
      if (currentZoom != null && currentZoom.toInt() != zoom.toInt()) {
        _log.d(
            '_cameraZoomSubscription | current: $currentZoom vs. new: $zoom - re-creating clusters');

        currentZoom = zoom.toInt();

        _displayMarkers();
      }
    });
  }

  dispose() {
    initializing.close();
    loading.close();
    loadingPlace.close();
    locationServices.close();
    redoSearch.close();
    mapTapped.close();
    selectedPlace.close();
    selectedDeal.close();
    selectedCluster.close();

    markers.close();

    mapList.close();
    mapListPlace.close();

    cameraZoom.close();
    headerList.close();
    headerPlace.close();
    headerPublisher.close();

    _publishers.close();

    _subLocationPermissions.cancel();
  }

  BehaviorSubject<PublisherAccountsResponse> get publishers =>
      _publishers.stream;

  Future<void> init(Function tapMarker) async {
    _log.d('init');

    onMarkerTap = tapMarker;

    final CurrentLocationResponse res =
        await LocationService.getInstance().getCurrentLocation();

    initialCameraPosition = res.position;
    currentZoom = res.position.zoom.toInt();

    locationServices.sink.add(res.hasLocationService);
    initializing.sink.add(false);
  }

  void setDeal(Deal deal) {
    if (deal.id != selectedDeal.value?.id) {
      selectedDeal.sink.add(deal);
    }
  }

  void setPlace(Place place) {
    if (place.id != selectedPlace.value?.id) {
      selectedPlace.sink.add(place);
    }
  }

  void setMapTapped() => mapTapped.sink.add(true);
  void setZoom(double zoom) => cameraZoom.sink.add(zoom);

  void processMarkerTap(MapMarker marker) {
    if (marker.dealId != null) {
      clearSelectedPlace();

      setDeal(_listResponse.deals
          .firstWhere((Deal deal) => deal.id == marker.dealId));
    } else {
      /// if we don't have a dealId then this is a cluster
      /// check if its a cluster of deals in the same place...
      final List<MapMarker> markersInCluster =
          _fluster.points(marker.clusterId);
      final MapMarker firstMarker = markersInCluster[0];

      final bool samePlace = markersInCluster.length ==
          markersInCluster
              .where((MapMarker m) => m.placeId == firstMarker.placeId)
              .length;

      /// if the markers are all in the same place
      /// then load the list with a place filter and callback to the map to expand to the place
      ///
      /// otherwise, if we're looking at a cluster of markers, then we just zoom in one step
      if (samePlace) {
        clearSelectedDeal();

        setPlace(_listResponse.deals
            .firstWhere((Deal d) => d.place.id == firstMarker.placeId)
            .place);
      } else {
        selectedCluster.add(firstMarker);
      }
    }
  }

  void onCameraMoveStarted() {
    if (currentPlace == null) {
      headerList.sink.add(MapListHeader("Explore nearby...", null));
    }
  }

  void onCameraIdle(CameraPosition currentCameraPosition) {
    /// keep track of the current camera zoom which we subscribe to
    /// via fluster and then adjust / potentially group markers into clusters
    cameraZoom.sink.add(currentCameraPosition.zoom);

    /// if this is not the initial load and if we're not already showing the redo search button then do it now
    if (fetchCount > 0 &&
        redoSearch.value != null &&
        redoSearch.value != true) {
      redoSearch.sink.add(true);
    }

    /// if we're not viewing a deal or a place then attempt to geocode
    /// the current camera position to identify a city - guard against no connectivity
    if (currentDeal == null && appState.hasInternet) {
      Geocoder.local
          .findAddressesFromCoordinates(Coordinates(
              currentCameraPosition.target.latitude,
              currentCameraPosition.target.longitude))
          .then((addresses) {
        /// must have addresses returned, though seems at times we dont' get the 'locality' (city)
        /// but seems we get the county (subadmiarea) which we'll use asa substitude
        final String city = addresses != null &&
                addresses.isNotEmpty &&
                (addresses.first.locality != null ||
                    addresses.first.subAdminArea != null)
            ? addresses.first.locality ?? addresses.first.subAdminArea
            : null;

        if (city != null) {
          headerList.sink.add(MapListHeader('Explore $city', null));
        }
      });
    }
  }

  /// called from map event when camera has been moved to another position
  /// though we guard against multiple updates by checking current value first
  void setRedoSearch() {
    if (redoSearch.value != null && redoSearch.value != true) {
      redoSearch.add(true);
    }
  }

  void clearSelectedPlace() {
    if (selectedPlace.value != null) {
      selectedPlace.sink.add(null);
    }
  }

  void clearSelectedDeal() {
    if (selectedDeal.value != null) {
      selectedDeal.sink.add(null);
    }
  }

  Future<CurrentLocationResponse> getUsersLocation() async =>
      await LocationService.getInstance().getCurrentLocation();

  /// validate map bounds as valid or invalid which is a bug
  /// that seems to still be in google maps whereby the bounds, especially on first loads
  /// will not return the proper visible region of the map in bounds...
  bool boundsAreInValid(LatLngBounds bounds) =>
      bounds.southwest.latitude == -90 &&
      bounds.southwest.longitude == -180 &&
      bounds.northeast.latitude == -90 &&
      bounds.northeast.longitude == -180;

  Future<void> loadMarkers(
    PlaceLatLngBounds bounds,
    PlaceLatLng searchLatLng,
    PlaceLatLng usersLatLng, {
    double pixelRatio,
    DealSort sort,
    DealType dealType,
    List<Tag> dealTags,
    PublisherAccount dealPublisher,
    bool backToList,
    bool reset,
  }) async {
    _log.d('loadMarkers | sort:$sort, backToList:$backToList, reset:$reset');

    /// keep track of the pixel ratio
    _devicePixelRatio = pixelRatio ?? _devicePixelRatio ?? 3.0;

    MapListResponse _response;
    Map<String, MapMarker> _markers;

    /// if we're going back to the list (e.g. we're clearing a place we just viewed)
    /// then we should have a list of previously loaded deals, so we can skip making
    /// the call to the server to load list of markers again
    final bool useLoadedDeals = backToList == true &&
        _listResponse != null &&
        _listResponse.deals.isNotEmpty;

    /// if we're filtering by a deal publisher, then set the header stream
    /// for the publisher header
    if (dealPublisher != null) {
      headerPublisher.sink.add(MapListHeader(dealPublisher.nameDisplay,
          dealPublisher.getTagsAsString() ?? dealPublisher.userName));
    }

    if (useLoadedDeals) {
      _response = _listResponse;
    } else {
      /// reset the pageindex if desired, as well as the
      /// list and place responses
      if (reset == true && currentPlace != null) {
        pageIndexPlace = 0;

        _placeResponse = MapListResponse();
      } else if (reset == true) {
        pageIndex = 0;

        _listResponse = MapListResponse();
      }

      _response = await _fetchList(
        bounds: bounds,
        searchLatLng: searchLatLng,
        usersLatLng: usersLatLng,
        sort: sort,
        dealType: dealType,
        dealTags: dealTags,
        dealPublisher: dealPublisher,
        backToList: backToList,
      );
    }

    /// publish the deals that will be displayed in the list
    if (currentPlace != null) {
      hasResultsPlace = _placeResponse.hasResults;
      pageIndexPlace = _placeResponse.pageIndex;

      mapListPlace.add(_response);

      /// add the existing list of deals from the list to
      /// the list we'll use to create markers
      _markers = MapMarkerHelpers.generateFlusterMarkers(
          List.from(_listResponse.deals)..addAll(_response.deals));
    } else {
      hasResults = _listResponse.deals.isNotEmpty;
      pageIndex = _listResponse.pageIndex;

      mapList.add(_response);

      _markers = MapMarkerHelpers.generateFlusterMarkers(_response.deals);
    }

    _fluster = Fluster<MapMarker>(
        minZoom: 0,
        maxZoom: maxZoom,
        radius: thumbnailWidth ~/ 2,
        extent: 2048,
        nodeSize: 32,
        points: _markers.values.toList(),
        createCluster:
            (BaseCluster cluster, double longitude, double latitude) =>
                MapMarker(
                    placeName: null,
                    placeId: null,
                    dealId: null,
                    autoApprove: false,
                    latitude: latitude,
                    longitude: longitude,
                    isCluster: true,
                    clusterId: cluster.id,
                    pointsSize: cluster.pointsSize,
                    markerId: cluster.id.toString(),
                    childMarkerId: cluster.childMarkerId));

    _displayMarkers();
  }

  _displayMarkers() async {
    // Finalize the markers to display on the map.
    Map<MarkerId, Marker> _markers = Map();

    _markers = await MapMarkerHelpers.generateMapMarkers(
      fluster: _fluster,
      currentZoom: currentZoom,
      devicePixelRatio: _devicePixelRatio,
      onMarkerTap: onMarkerTap,
      selectedDeal: currentDeal,
      selectedPlace: currentPlace,
    );

    // Publish markers to subscribers.
    _log.d('_displayMarkers | publishing markers to subscribers');
    markers.add(_markers);
  }

  Future<MapListResponse> _fetchList({
    @required PlaceLatLngBounds bounds,
    @required PlaceLatLng searchLatLng,
    @required PlaceLatLng usersLatLng,
    DealSort sort,
    DealType dealType,
    List<Tag> dealTags,
    PublisherAccount dealPublisher,
    bool backToList,
  }) async {
    final int placeFilterId = currentPlace?.id;
    MapListResponse _response = MapListResponse();

    /// keep track of the current search lat/lng that can be accessed
    /// by other components, like the list widget and the no results widget
    currentLatLngSearch = searchLatLng;

    /// keep track of current sort if we're changing it
    if (sort != null) {
      currentSort = sort;
    }

    /// keep track of deal type and tags filter, unless we're filtering by a current place
    /// in which case don't, so that we'd leave a potentially last filtered deal type and tags
    if (placeFilterId == null) {
      currentDealType = dealType;
      currentDealTags = dealTags;
      currentDealPublisher = dealPublisher;
    }

    /// loading place, or loading list
    if (placeFilterId != null) {
      hasMoreResultsPlace = false;
      loadingPlace.add(true);
    } else {
      hasMoreResults = false;
      loading.add(true);
    }

    redoSearch.add(false);

    final DealsResponse res = await DealsService.queryDeals(
      false,
      request: DealsRequest(
        skip: placeFilterId != null
            ? _placeResponse.pageIndex * pageSize
            : _listResponse.pageIndex * pageSize,
        take: pageSize,
        sort: currentSort ?? DealSort.closest,
        placeId: currentPlace != null ? currentPlace.id : null,
        latLng: searchLatLng,
        userLatLng: usersLatLng,

        /// filter by a dealtype if available and if we're NOT filtering by a place,
        /// currently we only allow for a single filter but supports a list of them...
        dealTypes: placeFilterId == null && currentDealType != null
            ? [currentDealType]
            : null,

        /// filter by dealtags if available and not filtering by a place
        tags:
            placeFilterId == null && currentDealTags != null ? dealTags : null,

        /// filter by publisher id unless filtering by place
        /// TODO: does not seem to work yet
        publisherAccountId:
            placeFilterId == null && currentDealPublisher != null
                ? currentDealPublisher.id
                : null,

        /// if filtering by 'virtual' deals, then set the radius to max
        /// and don't use bounding box
        miles: currentDealType == DealType.Virtual ? 300 : null,
        boundingBox: currentDealType == DealType.Virtual ? null : bounds,
      ),

      /// this forces client-cache to be refreshed each time
      forceRefresh: true,
    );

    if (res.error == null && res.models.isNotEmpty) {
      /// if we have a place we just filtered by
      /// then store its results in a separate list vs. all deals
      if (placeFilterId != null) {
        _placeResponse.pageIndex = _placeResponse.pageIndex + 1;
        _placeResponse.deals.addAll(res.models);
        _placeResponse.hasResults = res.models.length >= pageSize;
        _response = _placeResponse;
        hasMoreResultsPlace = res.models.length >= pageSize;
      } else {
        _listResponse.pageIndex = _listResponse.pageIndex + 1;
        _listResponse.deals.addAll(res.models);
        _listResponse.hasResults = res.models.length >= pageSize;
        _response = _listResponse;
        hasMoreResults = res.models.length >= pageSize;

        /// TODO: how to use tags here, how do we get miles?
        _fetchPublishers(
          tags: [Tag("category", "featured")],
          searchLatLng: searchLatLng,
          miles: 50,
        );
      }
    } else {
      /// set the error we may have gotten
      _response.error = res.error;

      /// while we may not have more results, let's ensure that
      /// if we do have existing loading results we consider that as having results
      if (placeFilterId != null) {
        _response.deals = _placeResponse.deals;
        _response.hasResults = false;
        _response.pageIndex = 0;
      } else {
        _response.deals = _listResponse.deals;
        _response.hasResults = false;
        _response.pageIndex = 0;
      }
    }

    /// increment fetch count
    fetchCount++;

    /// we're done loading
    if (placeFilterId != null) {
      loadingPlace.add(false);
    } else {
      loading.add(false);
    }

    /// if we're not viewing a places results list then we can process
    /// some options for the no results locations where we're available
    if (placeFilterId == null &&
        _response.hasResults == false &&
        _response.pageIndex == 0) {
      _processNoResults();
    } else {
      noResultOptions = null;
    }

    return _response;
  }

  /// TODO: doesn't seem to work yet
  Future<void> _fetchPublishers({
    @required List<Tag> tags,
    @required PlaceLatLng searchLatLng,
    @required double miles,
  }) async {
    final PublisherAccountsResponse res =
        await SearchService.queryBusinesses(BusinessSearchRequest(
      latitude: searchLatLng.latitude,
      longitude: searchLatLng.longitude,
      miles: miles,
      tags: tags,
    ));

    _publishers.sink.add(res);
  }

  void _processNoResults() {
    List<AvailableLocation> _regions = [];
    List<AvailableLocation> _cities = [];
    List<AvailableLocation> _neighborhoods = [];

    /// did the user search in a region that we support?
    /// did the user search in a city part of a region?
    AvailableLocation regionSearched;
    AvailableLocation citySearched;

    mapConfig.values.availableLocations.forEach((AvailableLocation region) {
      if (region.bounds.contains(currentLatLngSearch)) {
        regionSearched = region;
        _log.d('Searched in ${region.name}');

        regionSearched.children.forEach((AvailableLocation city) {
          if (city.bounds.contains(currentLatLngSearch)) {
            citySearched = city;
            _log.d('Searched in ${region.name}>${city.name}');

            citySearched.children.forEach((AvailableLocation neighborhood) {
              if (neighborhood.bounds.contains(currentLatLngSearch)) {
                _log.d(
                    'Searched in ${region.name}>${city.name}>${neighborhood.name}');
              }
            });
          }
        });
      }
    });

    if (regionSearched == null) {
      _regions.addAll(mapConfig.values.availableLocations);
    } else if (citySearched == null) {
      if (regionSearched.children.length > 0) {
        _cities.addAll(regionSearched.children
            .where((AvailableLocation city) =>
                !city.bounds.contains(currentLatLngSearch))
            .map((AvailableLocation city) => city)
            .toList());
      }
    } else {
      if (citySearched.children.length > 0) {
        _neighborhoods.addAll(citySearched.children
            .where((AvailableLocation neighborhood) =>
                !neighborhood.bounds.contains(currentLatLngSearch))
            .map((AvailableLocation city) => city)
            .toList());
      }
    }

    noResultOptions =
        MapListNoResultsOptions(_regions, _cities, _neighborhoods);
  }
}

class MapListResponse {
  DioError error;
  bool hasResults = false;
  int pageIndex = 0;
  List<Deal> deals = [];

  MapListResponse();

  bool get hasItems => deals.isNotEmpty;
}

class MapListHeader {
  final String title;
  final String subTitle;

  MapListHeader(this.title, this.subTitle);
}

class MapListNoResultsOptions {
  final List<AvailableLocation> regions;
  final List<AvailableLocation> cities;
  final List<AvailableLocation> neighborhoods;

  MapListNoResultsOptions(this.regions, this.cities, this.neighborhoods);
}
