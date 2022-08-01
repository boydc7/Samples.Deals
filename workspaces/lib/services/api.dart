import 'dart:async';

import 'package:dio/dio.dart';
import 'package:rydrworkspaces/services/api_interceptors.dart';

class AppApi {
  static final AppApi _singleton = AppApi._internal();
  static AppApi get instance => _singleton;

  Dio _dio;

  AppApi._internal() {
    _dio = Dio(BaseOptions(
        baseUrl: "https://devapi.getrydr.com/",
        connectTimeout: 6000,
        receiveTimeout: 6000))
      ..interceptors.addAll([
        AppDioInterceptor(),
        AppDioLoggingInterceptor(),
      ]);
  }

  Future<Response> call(
    String path, {
    method = 'GET',
    Map<String, dynamic> queryParams,
    Map<String, dynamic> body,
  }) {
    if (method == 'POST') {
      return _dio.post(path, data: body);
    } else if (method == 'PUT') {
      return _dio.put(path, data: body);
    } else if (method == 'DELETE') {
      return _dio.delete(path);
    } else {
      return _dio.get(path, queryParameters: queryParams);
    }
  }
}
