using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums;

[EnumAsInt]
public enum FileType
{
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

[EnumAsInt]
public enum ImageResizeMode
{
    Crop,
    Pad,
    BoxPad,
    Max,
    Min,
    Stretch
}

[EnumAsInt]
public enum FileConvertType
{
    None,
    ImageResize,
    VideoGenericMp4,
    VideoThumbnail
}

[EnumAsInt]
public enum FileConvertStatus
{
    Unknown,
    Submitted,
    InProgress,
    Complete,
    Canceled,
    Error
}
