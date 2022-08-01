import 'dart:async';
import 'package:rydr_app/models/requests/publisher_account_connections.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/publisher_account.dart';
import 'package:rydr_app/services/api.dart';

class PublisherAccountConnectionsService {
  /// NOTE: not currently implemented, but this would return only publishers
  /// that you (the profile) has done a request with
  static Future<PublisherAccountsResponse> queryConnections(
    PubAccountConnectionsRequest request, {
    bool forceRefresh = false,
  }) async {
    final ApiResponse apiResponse = await AppApi.instance
        .get('query/pubacctconnections', queryParams: request.toMap());

    return PublisherAccountsResponse.fromApiResponse(apiResponse);
  }
}
