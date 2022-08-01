import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class PublisherAccountsResponse extends BaseResponses<PublisherAccount> {
  PublisherAccountsResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) => PublisherAccount.fromJson(d))
                    .cast<PublisherAccount>()
                    .toList()
                : []);

  PublisherAccountsResponse.fromModels(List<PublisherAccount> models)
      : super.fromModels(
          models,
        );
}

class PublisherAccountResponse extends BaseResponse<PublisherAccount> {
  PublisherAccountResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => PublisherAccount.fromJson(j),
        );
}
