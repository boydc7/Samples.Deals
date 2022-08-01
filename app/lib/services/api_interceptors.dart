import 'dart:async';

import 'package:dio/dio.dart';
import 'package:logger/logger.dart';
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/services/authenticate.dart';

class AppDioInterceptor extends Interceptor {
  @override
  Future<dynamic> onRequest(RequestOptions options) async {
    final Map<String, dynamic> headers = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        headers[fieldName] = value.toString();
      }
    }

    var token = (await AuthenticationService.instance()
            .currentFirebaseUser
            ?.getIdToken())
        ?.token;

    addIfNonNull("Accept-Encoding", "gzip");
    addIfNonNull("Authorization", token != null ? "Bearer $token" : null);
    addIfNonNull("X-Rydr-PublisherAccountId", appState.currentProfile?.id);
    addIfNonNull("X-Rydr-WorkspaceId", appState.currentWorkspace?.id);

    if (AppConfig.disableCache()) {
      headers['Cache-Control'] = 'no-cache';
    }

    options.headers.addAll(headers);

    return options;
  }
}

class AppDioLoggingInterceptor extends Interceptor {
  final Logger log;

  AppDioLoggingInterceptor(this.log);

  @override
  Future<dynamic> onRequest(RequestOptions options) async {
    log.d(
        "onRequest | ${options.method != null ? options.method.toUpperCase() : 'METHOD'} ${"" + (options.baseUrl ?? "") + (options.path ?? "")}");
    log.d("\tHeaders:");
    options.headers.forEach((k, v) => log.d(
        '\t$k: ${v.toString().length > 20 ? v.toString().substring(0, 20) : v}'));
    if (options.queryParameters != null) {
      log.d("\tqueryParameters:");
      options.queryParameters.forEach((k, v) => log.d('\t$k: $v'));
    }
    if (options.data != null) {
      log.d("\tBody: ${options.data}");
    }

    return options;
  }

  @override
  Future<dynamic> onError(DioError dioError) async {
    AppErrorLogger.instance.reportError(
        'API',
        {
          "request": {
            "method": dioError.request.method,
            "path": dioError.request.path,
            "queryParameters": dioError.request.queryParameters,
            "data": dioError.request.data
          },
          "response": {
            "message": dioError.message,
            "statusCode": dioError.response?.statusCode,
            "statusMessage": dioError.response?.statusMessage,
            "data": dioError.response?.data
          }
        },
        StackTrace.current);

    log.e(
        "onError | ${dioError.message} ${(dioError.response?.request != null ? (dioError.response.request.baseUrl + dioError.response.request.path) : 'URL')}");
    log.e(
        "\t${dioError.response != null ? dioError.response.data : 'Unknown Error'}");
  }

  @override
  Future<dynamic> onResponse(Response response) async {
    log.d(
        "onResponse | ${response.statusCode} ${(response.request != null ? (response.request.baseUrl + response.request.path) : 'URL')}");
    //log.d("\tHeaders:");
    //response.headers?.forEach((k, v) => log.d('\t$k: $v'));
    //log.d("\tResponse: ${response.data}");
  }
}
