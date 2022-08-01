import 'package:dio/dio.dart';
import 'package:rydrworkspaces/models/deal.dart';

class DealResponse {
  final Deal deal;
  final DioError error;

  DealResponse(this.deal, this.error);

  DealResponse.fromResponse(Map<String, dynamic> json)
      : deal = Deal.fromResponseJson(json['result']),
        error = null;

  DealResponse.withError(DioError error)
      : deal = null,
        error = error;
}

class DealSaveResponse {
  final Deal deal;
  final DioError error;

  DealSaveResponse(this.deal, this.error);

  DealSaveResponse.fromResponse(Deal deal, Map<String, dynamic> json)
      : deal = deal..id = json['id'],
        error = null;

  DealSaveResponse.withError(DioError error)
      : deal = null,
        error = error;
}
