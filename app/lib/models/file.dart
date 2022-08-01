import 'package:rydr_app/models/enums/file.dart';

class File {
  final int id;
  final String name;
  final String description;
  final String fileExtension;
  final FileType fileType;
  final int contentLength;
  final String originalFilename;
  final bool isConverted;
  final FileConvertStatus convertStatus;

  File.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        name = json['name'],
        description = json['description'],
        fileExtension = json['fileExtension'],
        fileType = fileTypeFromString(json['fileType']),
        contentLength = json['contentLength'],
        originalFilename = json['originalFilename'],
        isConverted = json['isConverted'],
        convertStatus = fileConvertStatusFromString(json['convertStatus']);
}

class TransferFile {
  final int id;
  final String url;
  final int width;
  final int height;
  final String mimeType;

  TransferFile.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        url = json['url'],
        width = json['width'],
        height = json['height'],
        mimeType = json['mimeType'];
}
