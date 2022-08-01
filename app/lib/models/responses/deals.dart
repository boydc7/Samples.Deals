import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class DealsResponse extends BaseResponses<Deal> {
  DealsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => Deal.fromResponseJson(d))
                    .cast<Deal>()
                    .toList()
                : []);

  DealsResponse.fromModels(List<Deal> models)
      : super.fromModels(
          models,
        );
}
