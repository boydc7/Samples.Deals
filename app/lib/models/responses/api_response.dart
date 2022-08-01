import 'package:dio/dio.dart';

class ApiResponse {
  final Response response;
  final dynamic error;

  ApiResponse({
    this.response,
    this.error,
  });

  bool get hasData => response != null && response.data != null;
}
