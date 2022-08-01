import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/services/api.dart';

class DealInvitesService {
  static Future<PublisherAccountsResponse> getDealInvites(
    int dealId, {
    int skip,
    int take,
    bool forceRefresh = false,
  }) async {
    final String path = 'deals/$dealId/invites';

    final ApiResponse apiResponse = await AppApi.instance.get(path,
        queryParams: {
          "skip": skip ?? 0,

          /// NOTE! currently limited to 200 invites
          "take": take ?? 200,
        },
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return PublisherAccountsResponse.fromApiResponse(apiResponse);
  }
}
