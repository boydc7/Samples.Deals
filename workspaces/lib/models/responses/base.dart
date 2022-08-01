import 'package:dio/dio.dart';

class BaseResponse {
  final DioError error;

  BaseResponse(this.error);

  BaseResponse.fromResponse() : error = null;
  BaseResponse.withError(DioError error) : error = error;
}

class StringIdResponse {
  final String id;
  final DioError error;

  StringIdResponse.fromResponse(Map<String, dynamic> json)
      : id = json['result']['id'],
        error = null;

  StringIdResponse.withError(DioError error)
      : id = null,
        error = error;
}
