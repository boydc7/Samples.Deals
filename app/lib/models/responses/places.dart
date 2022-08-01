import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PlacesResponse extends BaseResponses<Place> {
  PlacesResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(apiResponse, (j) {
          List<Place> places = j != null
              ? j.map((dynamic d) => Place.fromJson(d)).cast<Place>().toList()
              : [];

          /// places must have a name and address
          places.removeWhere((Place p) => p.name == null || p.address == null);

          return places;
        });
}

class PlaceUpsertResponse extends BaseResponse<Place> {
  PlaceUpsertResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => Place.fromJson(j)..id = j['id'],
        );
}
