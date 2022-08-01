import 'dart:io';

import 'package:rydr_app/models/responses/api_response.dart';
import 'package:rydr_app/models/responses/file.dart';

import 'package:rydr_app/services/api.dart';
import 'package:http/http.dart' as http;

class FileService {
  static Future<FileResponse> getFile(int id) async {
    final ApiResponse apiResponse = await AppApi.instance.get('files/$id');

    return FileResponse.fromApiResponse(apiResponse);
  }

  static Future<int> upload(File file) async {
    /// make a request to our server to get a pre-signed upload url back to where
    /// we will then point the upload PUT on s3 for this file
    final transferFileResponse = TransferFileResponse.fromApiResponse(
      await AppApi.instance.post(
        'files',
        body: {
          'model': {
            'name': file.path,
          }
        },
      ),
    );

    if (transferFileResponse.hasError) {
      return null;
    }

    /// use basic http client (could not get DIO to work right) to PUT the file
    /// to the S3 pre-signed link we got from the transferFileResponse + mimeType
    var response = await http.put(transferFileResponse.model.url,
        body: file.readAsBytesSync(),
        headers: {
          "Content-Type": transferFileResponse.model.mimeType,
        });

    /// return the media file id (which we'll need for further linking this file/media to other objects)
    /// if the PUT was successfull, otherwise return null
    return response.reasonPhrase == "OK" ? transferFileResponse.model.id : null;
  }
}
