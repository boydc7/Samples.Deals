import 'package:rydr_app/models/deal.dart';
import 'api_response.dart';
import 'base.dart';

class DealResponse extends BaseResponse<Deal> {
  DealResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => Deal.fromResponseJson(j),
        );

  DealResponse.fromModel(Deal model)
      : super.fromModel(
          model,
        );
}
