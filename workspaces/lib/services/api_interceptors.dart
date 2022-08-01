import 'dart:async';

import 'package:dio/dio.dart';

class AppDioInterceptor extends Interceptor {
  @override
  Future<dynamic> onRequest(RequestOptions options) async {
    final Map<String, dynamic> headers = <String, dynamic>{};

    void addIfNonNull(String fieldName, dynamic value) {
      if (value != null) {
        headers[fieldName] = value.toString();
      }
    }

    var token =
        "eyJhbGciOiJSUzI1NiIsImtpZCI6IjYzZTllYThmNzNkZWExMTRkZWI5YTY0OTcxZDJhMjkzN2QwYzY3YWEiLCJ0eXAiOiJKV1QifQ.eyJuYW1lIjoiQW5kcmUgUmVmYXkiLCJwaWN0dXJlIjoiaHR0cHM6Ly9ncmFwaC5mYWNlYm9vay5jb20vMTAxNTc4NTEyMTM4MDI3ODQvcGljdHVyZSIsImlzcyI6Imh0dHBzOi8vc2VjdXJldG9rZW4uZ29vZ2xlLmNvbS9yeWRyLWZsdXR0ZXIiLCJhdWQiOiJyeWRyLWZsdXR0ZXIiLCJhdXRoX3RpbWUiOjE1ODExNzMyMTEsInVzZXJfaWQiOiJ1VDU0Y2xJS3JxUWNKNWp4SXRNQmFyMm10QW4yIiwic3ViIjoidVQ1NGNsSUtycVFjSjVqeEl0TUJhcjJtdEFuMiIsImlhdCI6MTU4MTM0Nzg3OCwiZXhwIjoxNTgxMzUxNDc4LCJmaXJlYmFzZSI6eyJpZGVudGl0aWVzIjp7ImZhY2Vib29rLmNvbSI6WyIxMDE1Nzg1MTIxMzgwMjc4NCJdfSwic2lnbl9pbl9wcm92aWRlciI6ImZhY2Vib29rLmNvbSJ9fQ.owcXfm9mxye5mvF7vuSPg49CYrvpPbaEwkIp73L3ZLhce8P3WIPSvHyvDfH1VYDBWX0shRISGMIgBQBqltdI-78UfIeSX5hj-lTdoRlK9ufvZF9sGWQwqlRO-ZwbGraYLXdgMpS2HnyCg9xa36bUDLivTegVi3hT1I2850nrRtzC3quTb7K2qO3G9kwPuiTVPpl3Bs4mxaYea0XBKn0l0W8oXYxTHvxTSyh7Tl526heFrV0RHXyy50Boysf9ciznjBdrlqXsp98bAVnRx-U52N9GwDCztVKhmeqXPYEeEhtLC_NkrkaziAJZPW7TJtpkhF1z6fBXyI626UQrB25Q_Q";
    //    (await AuthService.instance().currentFirebaseUser?.getIdToken())?.token;

    addIfNonNull("Accept-Encoding", "gzip");
    addIfNonNull("Authorization", token != null ? "Bearer $token" : null);
    addIfNonNull("X-Rydr-PublisherAccountId", 1527839);
    addIfNonNull("X-Rydr-WorkspaceId", 1539275);
    //addIfNonNull("X-Rydr-PublisherAccountId", appState.currentProfile?.id);
    //addIfNonNull("X-Rydr-WorkspaceId", appState.currentWorkspace?.id);

    //if (AppConfig.noCache()) {
    //headers['Cache-Control'] = 'no-cache';
    //}

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

class AppDioLoggingInterceptor extends Interceptor {
  AppDioLoggingInterceptor();

  @override
  Future<dynamic> onRequest(RequestOptions options) async {
    return options;
  }

  @override
  Future<dynamic> onError(DioError dioError) async {}

  @override
  Future<dynamic> onResponse(Response response) async {}
}
