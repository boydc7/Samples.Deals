import 'dart:async';

import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/deal.dart';
import 'package:rydrworkspaces/models/enums/deal.dart';
import 'package:rydrworkspaces/models/responses/base.dart';
import 'package:rydrworkspaces/models/responses/deal.dart';
import 'package:rydrworkspaces/models/responses/deal_metrics.dart';
import 'package:rydrworkspaces/services/api.dart';

class DealService {
  static Future<DealSaveResponse> saveDeal(Deal deal) async {
    /// if we have unset props then pass only the unset payload
    /// otherwise pass the paylod for adding / updating deal
    final Map<String, dynamic> body =
        deal.unset != null && deal.unset.length > 0
            ? deal.toPayloadUnset()
            : deal.toPayload();

    try {
      final Response res = await AppApi.instance.call(
        deal.id != null ? 'deals/${deal.id}' : 'deals',
        method: deal.id != null ? "PUT" : "POST",
        body: body,
      );

      return DealSaveResponse.fromResponse(deal, res.data);
    } catch (error) {
      return DealSaveResponse.withError(error);
    }
  }

  static Future<DealResponse> getDeal(
    dealId, {
    int requestedPublisherAccountId,
  }) async {
    final Map<String, dynamic> params = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        params[fieldName] = value.toString();
      }
    }

    addIfNonNull("requestedPublisherAccountId", requestedPublisherAccountId);

    try {
      final Response res =
          await AppApi.instance.call('deals/$dealId', queryParams: params);

      return DealResponse.fromResponse(res.data);
    } catch (error) {
      return DealResponse.withError(error);
    }
  }

  /// Gets aggregated stats either for a given deal
  /// or for the currently authenticated pub account as a whole
  static Future<DealMetricsResponse> getDealMetrics({
    int dealId,
    bool forceRefresh = false,
  }) async {
    try {
      final Response res = await AppApi.instance.call(dealId != null
          ? 'dealmetrics/completion?dealId=$dealId'
          : 'dealmetrics/completion');

      return DealMetricsResponse.fromResponse(res.data);
    } catch (error) {
      return DealMetricsResponse.withError(error);
    }
  }

  static Future<StringIdResponse> getDealGuid(Deal deal) async {
    try {
      final Response res = await AppApi.instance.call('deals/${deal.id}/xlink');

      return StringIdResponse.fromResponse(res.data);
    } catch (error) {
      return StringIdResponse.withError(error);
    }
  }

  static Future<BaseResponse> deleteDeal(deal) async {
    try {
      await AppApi.instance.call('deals/${deal.id}', method: 'DELETE');

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static Future<BaseResponse> addInvites(
      int dealId, List<int> invitePublisherAccountIds) async {
    try {
      await AppApi.instance.call("deals/$dealId/invites",
          method: "PUT",
          body: {"invitePublisherAccountIds": invitePublisherAccountIds});

      return BaseResponse.fromResponse();
    } catch (error) {
      return BaseResponse.withError(error);
    }
  }

  static void trackDealMetric(
    int dealId,
    DealMetricType metricType,
  ) async {
    try {
      AppApi.instance.call(
        'dealmetrics/$dealId/${dealMetricTypeToString(metricType)}',
        method: 'POST',
      );
    } catch (error) {}
  }
}
