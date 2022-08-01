enum FileType {
  Unknown,
  Other,
  Pdf,
  Doc,
  Compressed,
  DiskImage,
  Delimited,
  Structured,
  Presentation,
  Audio,
  Font,
  Image,
  Spreadsheet,
  Video
}

const _fileTypeMap = <String, FileType>{
  "Unknown": FileType.Unknown,
  "Other": FileType.Other,
  "Pdf": FileType.Pdf,
  "Doc": FileType.Doc,
  "Compressed": FileType.Compressed,
  "DiskImage": FileType.DiskImage,
  "Delimited": FileType.Delimited,
  "Structured": FileType.Structured,
  "Presentation": FileType.Presentation,
  "Audio": FileType.Audio,
  "Font": FileType.Font,
  "Image": FileType.Image,
  "Spreadsheet": FileType.Spreadsheet,
  "Video": FileType.Video,
};

fileTypeToString(FileType type) => type.toString().replaceAll('FileType.', '');

fileTypeFromString(String type) =>
    type == null ? FileType.Unknown : _fileTypeMap[type];

enum FileConvertStatus {
  Unknown,
  Submitted,
  InProgress,
  Complete,
  Canceled,
  Error
}

const _fileConvertStatusMap = <String, FileConvertStatus>{
  "Unknown": FileConvertStatus.Unknown,
  "Submitted": FileConvertStatus.Submitted,
  "InProgress": FileConvertStatus.InProgress,
  "Complete": FileConvertStatus.Complete,
  "Canceled": FileConvertStatus.Canceled,
  "Error": FileConvertStatus.Error,
};

fileConvertStatusToString(FileConvertStatus status) =>
    status.toString().replaceAll('FileConvertStatus.', '');

/// NOTE: no status means 'completed'
fileConvertStatusFromString(String status) =>
    status == null ? FileConvertStatus.Complete : _fileConvertStatusMap[status];
