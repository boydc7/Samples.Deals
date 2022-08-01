import 'package:rydr_app/app/log.dart';
import 'package:rydr_app/models/responses/api_response.dart';

class BasicVoidResponse extends BaseResponse {
  BasicVoidResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          null,
          null,
        );
}

class NoApiResultException implements Exception {
  static const String message = "No ApiResult returned.";

  String toString() => message;
}

class NoApiResultAttributeException implements Exception {
  static const String message = "No ApiResult returned.";

  String toString() => message;
}

typedef T ApiModelToJson<T>(Map<String, dynamic> json);
typedef List<T> ApiModelsToJson<T>(List<dynamic> json);

abstract class BaseIntResponse {
  int model;
  dynamic apiError;
  dynamic processingError;

  BaseIntResponse.fromApiResponse(
    ApiResponse apiResponse, [
    String resultKeyName = 'id',
  ]) {
    if (apiResponse == null) {
      apiError = NoApiResultException();
      return;
    }

    // These have already been logged by the api stack, just store a reference for use/success/fail determination
    apiError = apiResponse.error;

    if (apiResponse.hasData) {
      try {
        var result = apiResponse.response.data[resultKeyName];

        if (result == null) {
          processingError = NoApiResultAttributeException();
        } else {
          model = result;
        }
      } catch (x, stackTrace) {
        // Processing exception
        processingError = x;

        // Report this...
        AppErrorLogger.instance
            .reportError('Processing', processingError, stackTrace);
      }
    }
  }

  bool get hasError => apiError != null || processingError != null;
  dynamic get error => apiError ?? processingError;
}

abstract class BaseResponse<T> {
  T model;
  dynamic apiError;
  dynamic processingError;

  BaseResponse.fromModel(
    this.model,
  );

  BaseResponse.fromApiResponse(
    ApiResponse apiResponse,
    ApiModelToJson fromJson, [
    String resultKeyName = 'result',
  ]) {
    if (apiResponse == null) {
      apiError = NoApiResultException();
      return;
    }

    // These have already been logged by the api stack, just store a reference for use/success/fail determination
    apiError = apiResponse.error;

    if (apiResponse.response?.statusCode == 204 ||
        resultKeyName == null ||
        resultKeyName.isEmpty) {
      return;
    }

    if (apiResponse.hasData) {
      try {
        var result = apiResponse.response.data[resultKeyName];

        if (result == null) {
          processingError = NoApiResultAttributeException();
        } else {
          model = fromJson(result);
        }
      } catch (x, stackTrace) {
        // Processing exception
        processingError = x;

        // Report this...
        AppErrorLogger.instance
            .reportError('Processing', processingError, stackTrace);
      }
    }
  }

  BaseResponse.fromError(
    this.processingError,
  );

  bool get hasError => apiError != null || processingError != null;
  dynamic get error => apiError ?? processingError;
}

abstract class BaseResponses<T> {
  List<T> models;
  dynamic apiError;
  dynamic processingError;

  BaseResponses.fromModels(
    this.models,
  );

  BaseResponses.fromApiResponse(
    ApiResponse apiResponse,
    ApiModelsToJson fromJson, [
    String resultsKeyName = 'results',
  ]) {
    if (apiResponse == null) {
      apiError = NoApiResultException();
      return;
    }

    // These have already been logged by the api stack, just store a reference for use/success/fail determination
    apiError = apiResponse.error;

    if (apiResponse.hasData) {
      try {
        var result = apiResponse.response.data[resultsKeyName];

        /// we can have valid data but no actual 'results' key in the response
        /// so in those cases we return an empty array instead of null
        if (result == null) {
          models = [];
        } else {
          models = fromJson(result);
        }
      } catch (x, stackTrace) {
        // Processing exception
        processingError = x;

        // Report this...
        AppErrorLogger.instance.reportError('Processing', x, stackTrace);
      }
    }
  }

  BaseResponses.fromError(
    this.processingError,
  );

  bool get hasError => apiError != null || processingError != null;
  dynamic get error => apiError ?? processingError;
}
