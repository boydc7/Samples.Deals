import 'dart:async';

import 'package:dio/dio.dart';
import 'package:logger/logger.dart';
import 'package:rydrworkspaces/app/config.dart';
import 'package:rydrworkspaces/app/log.dart';
import 'package:rydrworkspaces/app/state.dart';
import 'package:rydrworkspaces/services/auth_service.dart';

class DioRequestHeadersInterceptor extends Interceptor {
  @override
  Future<dynamic> onRequest(RequestOptions options) async {
    final Map<String, dynamic> headers = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        headers[fieldName] = value.toString();
      }
    }

    var token =
        (await AuthService.instance.currentGfbUser?.getIdToken())?.token;

    addIfNonNull("Accept-Encoding", "gzip");
    addIfNonNull("Authorization", token != null ? "Bearer $token" : null);
    addIfNonNull("X-Rydr-PublisherAccountId", appState.currentProfile?.id);
    addIfNonNull("X-Rydr-WorkspaceId", appState.currentWorkspace?.id);

    if (AppConfig.noCache()) {
      headers['Cache-Control'] = 'no-cache';
    }

    options.headers.addAll(headers);

    return options;
  }

  @override
  Future<dynamic> onResponse(Response response) async {
    /// if the response data object is a string and not a json object
    /// then we got issues on the server
    if (response.data is String && response.data != "") {
      return DioError(
        response: response,
        error: response.statusMessage ?? "Invalid response",
        type: DioErrorType.DEFAULT,
      );
    }
  }
}

class DioRequestLoggingInterceptor extends Interceptor {
  static final Logger _log = getLogger('AppDioLoggingInterceptor');

  @override
  Future<dynamic> onRequest(RequestOptions options) async {
    _log.d(
        "onRequest | ${options.method != null ? options.method.toUpperCase() : 'METHOD'} ${"" + (options.baseUrl ?? "") + (options.path ?? "")}");

    _log.d("\tHeaders:");
    options.headers.forEach((k, v) => _log.d('\t$k: $v'));

    if (options.queryParameters != null) {
      _log.d("\tqueryParameters:");
      options.queryParameters.forEach((k, v) => _log.d('\t$k: $v'));
    }

    if (options.data != null) {
      _log.d("\tBody: ${options.data}");
    }

    return options;
  }

  @override
  Future<dynamic> onError(DioError dioError) async {
    _log.e(
        "onError | ${dioError.message} ${(dioError.response?.request != null ? (dioError.response.request.baseUrl + dioError.response.request.path) : 'URL')}");

    _log.e(
        "\t${dioError.response != null ? dioError.response.data : 'Unknown Error'}");
  }

  @override
  Future<dynamic> onResponse(Response response) async {
    _log.d(
        "onResponse | ${response.statusCode} ${(response.request != null ? (response.request.baseUrl + response.request.path) : 'URL')}");

    _log.d("\tHeaders:");
    response.headers?.forEach((k, v) => _log.d('\t$k: $v'));

    _log.d("\tResponse: ${response.data}");
  }
}
