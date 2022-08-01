import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class FacebookPlacesSearchResponse extends BaseResponses<FacebookPlaceInfo> {
  FacebookPlacesSearchResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => FacebookPlaceInfo.fromJson(d))
                    .cast<FacebookPlaceInfo>()
                    .toList()
                : []);
}
