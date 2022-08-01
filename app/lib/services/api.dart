import 'dart:async';

import 'package:dio/dio.dart';
import 'package:dio_http_cache/dio_http_cache.dart';
import 'package:rydr_app/app/config.dart';
import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/models/responses/api_response.dart';

import 'api_interceptors.dart';

class AppApi {
  static final AppApi _singleton = AppApi._internal();
  static AppApi get instance => _singleton;
  static final _log = getLogger('AppApi');

  static Dio _dio;
  static DioCacheManager _dioCacheManager;

  AppApi._internal() {
    _log.d(
        '_internal | Configuring Api Client with base options and interceptors');

    var config = AppConfig.instance;

    _dio = Dio(
      BaseOptions(
          baseUrl: config.apiHost,
          connectTimeout: config.httpConnectTimeoutInMilliSeconds,
          receiveTimeout: config.httpReceiveTimeoutInMilliSeconds),
    )..interceptors.addAll(
        [
          getCacheManager(
            config.apiHost,
            config.noCache,
          ).interceptor,
          AppDioInterceptor(),
          AppDioLoggingInterceptor(_log),
        ],
      );

    if (config.noCache) {
      _dioCacheManager?.clearAll();
    }
  }

  Future<ApiResponse> upload(
    String path, {
    dynamic data,
    String mimeType,
    Function onSendProgress,
  }) async {
    try {
      final Dio _d = Dio()
        ..options = BaseOptions(
          contentType: mimeType,
          headers: {
            "Content-Type": mimeType,
          },
        )
        ..interceptors.addAll(
          [
            AppDioLoggingInterceptor(_log),
          ],
        );

      final Response response = await _d.put(
        path,
        data: data,
        onSendProgress: onSendProgress,
      );

      return ApiResponse(
        response: response,
      );
    } catch (x) {
      return ApiResponse(
        error: x,
      );
    }
  }

  Future<ApiResponse> get(
    String path, {
    Map<String, dynamic> queryParams,
    Options options,
  }) =>
      send(
        path,
        method: 'GET',
        queryParams: queryParams,
        options: options,
      );

  Future<ApiResponse> post(
    String path, {
    Map<String, dynamic> body,
    Options options,
  }) =>
      send(
        path,
        method: 'POST',
        body: body,
        options: options,
      );

  Future<ApiResponse> put(
    String path, {
    Map<String, dynamic> body,
    Options options,
  }) =>
      send(
        path,
        method: 'PUT',
        body: body,
        options: options,
      );

  Future<ApiResponse> delete(
    String path, {
    Options options,
  }) =>
      send(
        path,
        method: 'DELETE',
        options: options,
      );

  Future<ApiResponse> send(
    String path, {
    String method,
    Map<String, dynamic> queryParams,
    Map<String, dynamic> body,
    Options options,
    Function onSendProgress,
  }) async {
    try {
      Response response;

      if (method == 'POST') {
        response = await _dio.post(
          path,
          data: body,
          options: options,
        );
      } else if (method == 'PUT') {
        response = await _dio.put(
          path,
          data: body,
          options: options,
          onSendProgress: onSendProgress,
        );
      } else if (method == 'DELETE') {
        response = await _dio.delete(
          path,
          options: options,
        );
      } else {
        response = await _dio.get(
          path,
          queryParameters: queryParams,
          options: options,
        );
      }

      return ApiResponse(
        response: response,
      );
    } catch (x) {
      return ApiResponse(
        error: x,
      );
    }
  }

  Options cacheConfig(
    String path, {
    Duration duration,
    bool forceRefresh = false,
    bool includeWorkspaceInKey = true,
    bool includeProfileInKey = true,
  }) {
    /// composite primary key for caching this response
    final String primaryKey = getCachePrimaryKey(path,
        includeProfileInKey: includeProfileInKey,
        includeWorkspaceInKey: includeWorkspaceInKey);

    _log.i('cacheConfig | forceRefresh: $forceRefresh primaryKey: $primaryKey');

    /// default caching time to one hour to start
    /// eventually we'll tweak this on a per call basis
    return buildCacheOptions(duration ?? Duration(hours: 1),
        primaryKey: primaryKey, forceRefresh: forceRefresh);
  }

  void clearCache() => _dioCacheManager.clearAll();

  /// clear all cache for a given primary key
  void clearCacheByPath(String path) {
    _log.i('clearCacheByPath primaryKey: $path');

    _dioCacheManager.delete(path);
  }

  static DioCacheManager getCacheManager(
    String baseUrl, [
    bool skipDiskCache = false,
  ]) {
    if (null == _dioCacheManager) {
      _dioCacheManager = DioCacheManager(
        CacheConfig(
          baseUrl: baseUrl,
          skipDiskCache: skipDiskCache,
        ),
      );
    }

    return _dioCacheManager;
  }

  String getCachePrimaryKey(
    String path, {
    bool includeWorkspaceInKey = true,
    bool includeProfileInKey = true,
  }) {
    final String workspaceId =
        includeWorkspaceInKey && appState.currentWorkspace != null
            ? appState.currentWorkspace.id.toString() + "/"
            : "";
    final String profileId =
        includeProfileInKey && appState.currentProfile != null
            ? appState.currentProfile.id.toString() + "/"
            : "";

    return '${AppConfig.instance.apiHost}$path/$workspaceId$profileId';
  }
}
