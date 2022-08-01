import 'package:rydrworkspaces/models/enums/publisher_account.dart';

class PlaceAddress {
  String name;
  String address1;
  String city;
  String stateProvince;
  String postalCode;
  double latitude;
  double longitude;

  PlaceAddress({
    this.name,
    this.address1,
    this.city,
    this.stateProvince,
    this.postalCode,
    this.latitude,
    this.longitude,
  });

  PlaceAddress.fromJson(Map<String, dynamic> json) {
    if (json != null) {
      this.name = json['name'].toString();
      this.address1 = json['address1'];
      this.city = json['city'];
      this.stateProvince = json['stateProvince'];
      this.postalCode = json['postalCode'];
      this.longitude = json['longitude'].toDouble();
      this.latitude = json['latitude'].toDouble();
    }
  }

  Map<String, dynamic> toJson() => {
        "name": this.name,
        "address1": this.address1,
        "city": this.city,
        "stateProvince": this.stateProvince,
        "postalCode": this.postalCode,
        "latitude": this.latitude,
        "longitude": this.longitude
      };

  @override
  String toString() =>
      """$runtimeType (name: $name, address1: $address1, city: $city, stateProvince: $stateProvince, postalCode: $postalCode, latitude: $latitude, longitude: $longitude)""";
}

class Place {
  int id;
  PublisherType pubType;
  String publisherId;
  String name;
  PlaceAddress address;
  bool isPrimary;

  Place({
    this.id,
    this.pubType,
    this.publisherId,
    this.name,
    this.address,
    this.isPrimary,
  });

  Place.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.pubType = publisherTypeFromString(json['publisherType']);
    this.publisherId = json['publisherId'];
    this.name = json['name'];
    this.address = PlaceAddress.fromJson(json['address']);
    this.isPrimary = json['isPrimary'] ?? false;
  }

  Map<String, dynamic> toJson() {
    Map<String, dynamic> json = {};

    if (this.id != null) {
      json["id"] = this.id;
    }

    if (this.pubType != null) {
      json['publisherType'] = publisherTypeToString(this.pubType);
    }

    if (this.publisherId != null) {
      json['publisherId'] = this.publisherId;
    }

    if (this.name != null) {
      json["name"] = this.name;
    }
    if (this.address != null) {
      json["address"] = this.address.toJson();
    }

    if (this.isPrimary != null) {
      json["isPrimary"] = this.isPrimary;
    }

    return json;
  }

  String addressForDisplay() {
    List<String> parts = [];

    parts.add(address.address1 ?? '');
    // parts.add(address.address1 != null ? ', ' + address.city : address.city);

    // parts
    //     .add(address.stateProvince != null ? ', ' + address.stateProvince : '');

    return parts.join('');
  }

  @override
  String toString() =>
      """$runtimeType (id: $id, pubType: $pubType, publisherId: $publisherId, name: $name, address: ${address.toString()}, isPrimary: $isPrimary)""";
}

class PlaceLatLng {
  double latitude;
  double longitude;

  PlaceLatLng(
    this.latitude,
    this.longitude,
  );
}

class PlaceLatLngBounds {
  PlaceLatLng southWest;
  PlaceLatLng northEast;

  PlaceLatLngBounds(
    this.southWest,
    this.northEast,
  );

  Map<String, dynamic> toJson() => {
        "southWestLatitude": southWest.latitude,
        "southWestLongitude": southWest.longitude,
        "northEastLatitude": northEast.latitude,
        "northEastLongitude": northEast.longitude,
      };

  /// Returns whether this rectangle contains the given [LatLng].
  bool contains(PlaceLatLng point) =>
      _containsLatitude(point.latitude) && _containsLongitude(point.longitude);

  bool _containsLatitude(double lat) =>
      (southWest.latitude <= lat) && (lat <= northEast.latitude);

  bool _containsLongitude(double lng) {
    if (southWest.longitude <= northEast.longitude) {
      return southWest.longitude <= lng && lng <= northEast.longitude;
    } else {
      return southWest.longitude <= lng || lng <= northEast.longitude;
    }
  }

  String toQueryString() =>
      "{southWestLatitude:${southWest.latitude},southWestLongitude:${southWest.longitude},northEastLatitude:${northEast.latitude},northEastLongitude:${northEast.longitude}}";

  @override
  String toString() => "$runtimeType ($southWest,$northEast)";
}

/// Parsed from firebase remote config setting
/// or fall back from /config/map_available_locations.js
class AvailableLocation {
  String name;
  String url;
  double zoom;
  double tilt;
  PlaceLatLng center;
  PlaceLatLngBounds bounds;
  List<AvailableLocation> children;

  AvailableLocation.fromJson(Map<String, dynamic> json) {
    this.name = json['name'];
    this.url = json['url'];
    this.zoom = json['zoom'] != null ? json['zoom'].toDouble() : 10;
    this.tilt = json['tilt'] != null ? json['tilt'].toDouble() : 90;
    this.center = PlaceLatLng(
      double.parse(
        json['center'].toString().split(',')[0],
      ),
      double.parse(
        json['center'].toString().split(',')[1],
      ),
    );
    this.bounds = PlaceLatLngBounds(
      PlaceLatLng(
        double.parse(
          json['bounds']['southWest'].toString().split(',')[0],
        ),
        double.parse(
          json['bounds']['southWest'].toString().split(',')[1],
        ),
      ),
      PlaceLatLng(
        double.parse(
          json['bounds']['northEast'].toString().split(',')[0],
        ),
        double.parse(
          json['bounds']['northEast'].toString().split(',')[1],
        ),
      ),
    );
    this.children = jsonToAvailableLocations(json['children']);
  }

  static List<AvailableLocation> jsonToAvailableLocations(List<dynamic> json) {
    if (json == null) {
      return [];
    }

    List<AvailableLocation> locations = [];
    json.forEach((location) {
      locations.add(AvailableLocation.fromJson(location));
    });

    return locations;
  }
}

class FacebookPlaceInfo {
  String id;
  int rydrPlaceId;
  String coverPhotoUrl;
  String description;
  bool isPermanentlyClosed;
  bool isVerified;
  String fbUrl;
  PlaceAddress location;
  String name;
  String phone;
  String singleLineAddress;
  String website;

  FacebookPlaceInfo.fromJson(Map<String, dynamic> json) {
    this.id = json['id'];
    this.rydrPlaceId = json['rydrPlaceId'];
    this.coverPhotoUrl = json['coverPhotoUrl'];
    this.description = json['description'];
    this.isPermanentlyClosed = json['isPermanentlyClosed'];
    this.isVerified = json['isVerified'];
    this.fbUrl = json['fbUrl'];
    this.location = PlaceAddress.fromJson(json['location']);
    this.name = json['name'];
    this.phone = json['phone'];
    this.singleLineAddress = json['singleLineAddress'] ?? "";
    this.website = json['website'];
  }
}
