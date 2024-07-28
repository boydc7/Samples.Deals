using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Models.Supporting;

public abstract class FileConvertTypeArgumentsBase
{
    public abstract FileConvertType ConvertedType { get; }
    public abstract (string TagName, string TagValue) Tag { get; }
}

public abstract class ImageConvertTypeArgumentsBase : FileConvertTypeArgumentsBase
{
    public int Width { get; set; }
    public int Height { get; set; }
    public ImageResizeMode ResizeMode { get; set; } = ImageResizeMode.Crop;
    public string Extension => "jpg";
}

public class ImageConvertTypeArguments : ImageConvertTypeArgumentsBase
{
    public override FileConvertType ConvertedType => FileConvertType.ImageResize;
    public override (string TagName, string TagValue) Tag => ("alternate", "resized");
}

public class VideoThumbnailConvertGenericTypeArguments : ImageConvertTypeArgumentsBase
{
    public override FileConvertType ConvertedType => FileConvertType.VideoThumbnail;
    public override (string TagName, string TagValue) Tag => ("alternate", "thumbnail");
}

public class VideoConvertGenericTypeArguments : FileConvertTypeArgumentsBase
{
    public override FileConvertType ConvertedType => FileConvertType.VideoGenericMp4;
    public override (string TagName, string TagValue) Tag => ("alternate", "genericMp4");

    public static VideoConvertGenericTypeArguments Instance { get; } = new();
}
