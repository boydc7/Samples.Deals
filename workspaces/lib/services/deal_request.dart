import 'dart:async';
import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/responses/base.dart';
import 'package:rydrworkspaces/models/responses/deal.dart';
import 'package:rydrworkspaces/services/api.dart';

class DealRequestService {
  static Future<BaseResponse> addRequest(int dealId,
      [int daysUntilDelinquent = 7]) async {
    try {
      await AppApi.instance.call(
        "deals/$dealId/requests",
        method: "POST",
        body: {
          "model": {
            "daysUntilDelinquent": daysUntilDelinquent,
          }
        },
      );

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static Future<BaseResponse> updateRequestStatus(
    Deal deal,
    DealRequestStatus status, {
    List<String> completionMediaIds,
    String reason,
    double usersCurrentLatitude,
    double usersCurrentLongitude,
  }) async {
    final int publisherAccountId = deal.request.publisherAccount.id;

    try {
      await AppApi.instance.call(
        "deals/${deal.id}/requests",
        method: "PUT",
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

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static Future<BaseResponse> updateDaysUntilDelinquent(
    Deal deal,
    int daysUntilDelinquent,
  ) async {
    final int publisherAccountId = deal.request.publisherAccount.id;

    try {
      await AppApi.instance.call(
        "deals/${deal.id}/requests",
        method: "PUT",
        body: {
          "model": {
            "dealId": deal.id,
            "publisherAccountId": publisherAccountId,
            "daysUntilDelinquent": daysUntilDelinquent,
          }
        },
      );

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static Future<StringIdResponse> getRequestExternalId(Deal deal) async {
    try {
      final Response res = await AppApi.instance.call(
          'deals/${deal.id}/requests/${deal.request.publisherAccount.id}/rxlink');

      return StringIdResponse.fromResponse(res.data);
    } catch (error) {
      return StringIdResponse.withError(error);
    }
  }

  static Future<DealResponse> getRequestExternalReport(String reportId) async {
    try {
      final Response res = await AppApi.instance.call('deals/xr/$reportId');

      return DealResponse.fromResponse(res.data);
    } catch (error) {
      return DealResponse.withError(error);
    }
  }
}
