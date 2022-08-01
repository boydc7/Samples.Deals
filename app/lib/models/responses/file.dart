import 'package:rydr_app/models/file.dart';
import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/base.dart';

class TransferFileResponse extends BaseResponse<TransferFile> {
  TransferFileResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => TransferFile.fromJson(j),
        );
}

class FileResponse extends BaseResponse<File> {
  FileResponse.fromApiResponse(ApiResponse apiResponse)
      : super.fromApiResponse(
          apiResponse,
          (j) => File.fromJson(j),
        );
}
