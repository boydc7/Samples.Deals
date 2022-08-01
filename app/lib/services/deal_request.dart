import 'dart:async';

import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/services/deals.dart';

class DealRequestService {
  /// creates a new request on a deal
  static Future<BasicVoidResponse> addRequest(
    int dealId, [
    int daysUntilDelinquent = 7,
  ]) async {
    final ApiResponse apiResponse = await AppApi.instance.post(
      "deals/$dealId/requests",
      body: {
        "model": {
          "daysUntilDelinquent": daysUntilDelinquent,
        }
      },
    );

    /// if successful, clear the requests cache for the creator
    DealsService.clearRequestsCache(apiResponse, false);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> updateRequestStatus(
    Deal deal,
    DealRequestStatus status, {
    List<String> completionMediaIds,
    String reason,
    double usersCurrentLatitude,
    double usersCurrentLongitude,
  }) async {
    final int publisherAccountId = appState.currentProfile.isCreator
        ? appState.currentProfile.id
        : deal.request.publisherAccount.id;

    final ApiResponse apiResponse = await AppApi.instance.put(
      "deals/${deal.id}/requests",
      body: {
        "reason": reason,
        "completionMediaIds": completionMediaIds,
        "latitude": usersCurrentLatitude,
        "longitude": usersCurrentLongitude,
        "model": {
          "dealId": deal.id,
          "publisherAccountId": publisherAccountId,
          "status": dealRequestStatusToString(status),
        }
      },
    );

    /// clear request cache for the user
    DealsService.clearRequestsCache(
        apiResponse, appState.currentProfile.isBusiness);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<BasicVoidResponse> updateDaysUntilDelinquent(
    Deal deal,
    int daysUntilDelinquent,
  ) async {
    final int publisherAccountId = appState.currentProfile.isCreator
        ? appState.currentProfile.id
        : deal.request.publisherAccount.id;

    final ApiResponse apiResponse = await AppApi.instance.put(
      "deals/${deal.id}/requests",
      body: {
        "model": {
          "dealId": deal.id,
          "publisherAccountId": publisherAccountId,
          "daysUntilDelinquent": daysUntilDelinquent,
        }
      },
    );

    /// clear request cache for the business
    DealsService.clearRequestsCache(apiResponse, true);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  static Future<StringIdResponse> getRequestExternalId(Deal deal,
      [bool forceRefresh = false]) async {
    final String path =
        'deals/${deal.id}/requests/${deal.request.publisherAccount.id}/rxlink';

    final ApiResponse res = await AppApi.instance.get(path,
        options: AppApi.instance.cacheConfig(
          path,
          forceRefresh: forceRefresh,
        ));

    return StringIdResponse.fromApiResponse(res);
  }
}
