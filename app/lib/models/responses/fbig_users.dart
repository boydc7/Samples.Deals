import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class FbIgUsersResponse extends BaseResponses<PublisherAccount> {
  FbIgUsersResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
            apiResponse,
            (j) => j != null
                ? j
                    .map((dynamic d) =>
                        PublisherAccount.fromInstaBusinessAccount(d))
                    .cast<PublisherAccount>()
                    .toList()
                : []);

  FbIgUsersResponse.fromModels(List<PublisherAccount> models)
      : super.fromModels(models);
}
