import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/deal.dart';

class DealsResponse {
  final List<Deal> deals;
  final DioError error;

  DealsResponse(this.deals, this.error);

  DealsResponse.fromResponse(Map<String, dynamic> json)
      : deals = json['results'] != null
            ? json['results']
                .map((dynamic d) => Deal.fromResponseJson(d))
                .cast<Deal>()
                .toList()
            : [],
        error = null;

  DealsResponse.withError(DioError error)
      : deals = null,
        error = error;
}
