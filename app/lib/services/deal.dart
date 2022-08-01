import 'dart:async';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/publisher_account.dart';
import 'package:rydr_app/models/responses/base.dart';
import 'package:rydr_app/models/responses/deal.dart';
import 'package:rydr_app/models/responses/id_response.dart';
import 'package:rydr_app/services/api.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/services/deals.dart';

class DealService {
  /// saves new deals or existing deals from a draft
  static Future<IntIdResponse> saveDeal(Deal deal) async {
    final apiResponse = await AppApi.instance.send(
      deal.id != null ? 'deals/${deal.id}' : 'deals',
      method: deal.id != null ? "PUT" : "POST",
      body: deal.toPayload(),
    );

    /// clear local cache of active deals for the business
    /// which will also clear the drafts cache
    DealsService.clearDealsCache(apiResponse, true);

    return IntIdResponse.fromApiResponse(apiResponse);
  }

  /// update existing deal which only supports a certain set of props
  static Future<BasicVoidResponse> updateDeal(Deal deal) async {
    final apiResponse = await AppApi.instance.put(
      'deals/${deal.id}',
      body: deal.toPayload(),
    );

    /// clear local cache of active deals for the business
    DealsService.clearDealsCache(apiResponse, true);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// change deal expiration date - pass NULL to set to never expires
  static Future<BasicVoidResponse> updateExpirationDate(
      int dealId, DateTime expirationDate) async {
    final Deal d = Deal()
      ..id = dealId
      ..expirationDate = expirationDate;

    return await updateDeal(d);
  }

  /// change deal status to paused, published, completed (archived)
  static Future<BasicVoidResponse> updateStatus(
    int dealId,
    DealStatus status,
  ) async {
    final Deal d = Deal()
      ..id = dealId
      ..status = status;

    return await updateDeal(d);
  }

  static Future<DealResponse> getDeal(
    dealId, {
    int requestedPublisherAccountId,
    double userLatitude,
    double userLongitude,
  }) async {
    final Map<String, dynamic> params = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        params[fieldName] = value.toString();
      }
    }

    /// if we have a requested publisher acount id and this is a bussines making the call
    /// then tack that on as a parameter to the endpoint which will return back the request as well
    addIfNonNull(
        "requestedPublisherAccountId",
        requestedPublisherAccountId != null &&
                appState.currentProfile.isBusiness
            ? requestedPublisherAccountId
            : null);

    addIfNonNull("userLatitude", userLatitude);
    addIfNonNull("userLongitude", userLongitude);

    final apiResponse = await AppApi.instance.get(
      'deals/$dealId',
      queryParams: params,
    );

    return DealResponse.fromApiResponse(apiResponse);
  }

  /// get a deal using the 'share' link from the web
  static Future<DealResponse> getDealByLink(String dealLink) async =>
      DealResponse.fromApiResponse(
          await AppApi.instance.get('deallinks/$dealLink'));

  /// get guid for sharing a deal on the web
  static Future<StringIdResponse> getDealGuid(Deal deal) async {
    final String path = 'deals/${deal.id}/xlink';
    final apiResponse = await AppApi.instance
        .get(path, options: AppApi.instance.cacheConfig(path));

    return StringIdResponse.fromApiResponse(apiResponse);
  }

  /// deletes a draft deal, if successfull we'll refresh list of
  /// current draft deals as well
  static Future<BasicVoidResponse> deleteDeal(deal) async {
    final apiResponse = await AppApi.instance.delete('deals/${deal.id}');

    /// clear local cache of active deals for the business
    DealsService.clearDealsCache(apiResponse, true);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// adds (additional) invites to an existing deal
  static Future<BasicVoidResponse> addInvites(
    int dealId,
    List<PublisherAccount> publisherAccounts,
  ) async {
    final apiResponse = await AppApi.instance.put(
      "deals/$dealId/invites",
      body: {
        "publisherAccounts": publisherAccounts
            .map((PublisherAccount user) => user.toInviteJson())
            .toList(),
      },
    );

    /// clear local cache of active deals for the business
    DealsService.clearDealsCache(apiResponse, true);

    return BasicVoidResponse.fromApiResponse(apiResponse);
  }

  /// tracks a 'view' of a deal for when someone taps / views details
  /// of one from the map view where we don't actually load a new deal from the server
  static void trackDealMetric(
    int dealId,
    DealMetricType metricType,
  ) =>
      AppApi.instance
          .post('dealmetrics/$dealId/${dealMetricTypeToString(metricType)}');
}
