import 'dart:async';

import 'package:dio/dio.dart';
import 'package:logger/logger.dart';
import 'package:rydrworkspaces/app/config.dart';
import 'package:rydrworkspaces/app/log.dart';
import 'interceptors.dart';

class ApiClient {
  static final Logger _log = getLogger('ApiClient');
  static final ApiClient _instance = ApiClient._internal();

  Dio _dio;

  static ApiClient get instance => _instance;

  ApiClient._internal() {
    _dio = Dio(BaseOptions(
        baseUrl: AppConfig.instance.apiHost,
        connectTimeout: AppConfig.instance.httpConnectTimeoutInMilliSeconds,
        receiveTimeout: AppConfig.instance.httpReceiveTimeoutInMilliSeconds))
      ..interceptors.add(DioRequestHeadersInterceptor());

    if (!AppConfig.isProduction()) {
      _dio.interceptors.add(DioRequestLoggingInterceptor());
    }
  }

  Future<Response> get(
    String path, {
    Map<String, dynamic> queryParams,
  }) =>
      call(
        path,
        method: 'GET',
        queryParams: queryParams,
      );

  Future<Response> post(
    String path, {
    Map<String, dynamic> body,
  }) =>
      call(
        path,
        method: 'POST',
        body: body,
      );

  Future<Response> put(
    String path, {
    Map<String, dynamic> body,
  }) =>
      call(
        path,
        method: 'PUT',
        body: body,
      );

  Future<Response> delete(
    String path,
  ) =>
      call(
        path,
        method: 'DELETE',
      );

  Future<Response> call(
    String path, {
    method = 'GET',
    Map<String, dynamic> queryParams,
    Map<String, dynamic> body,
  }) {
    try {
      if (method == 'POST') {
        return _dio.post(path, data: body);
      } else if (method == 'PUT') {
        return _dio.put(path, data: body);
      } else if (method == 'DELETE') {
        return _dio.delete(path);
      } else {
        return _dio.get(path, queryParameters: queryParams);
      }
    } catch (x) {
      _log.e('ApiClient exception on call request', x);

      rethrow;
    }
  }
}
