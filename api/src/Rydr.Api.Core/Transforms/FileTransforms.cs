using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using ServiceStack;

namespace Rydr.Api.Core.Transforms;

public static class FileTransforms
{
    private static readonly bool _keepAllPublisherMedia = RydrEnvironment.GetAppSetting("Files.KeepAllPublisherMedia", false);

    public static readonly Dictionary<string, FileType> FileExtensionFileTypeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "rtf", FileType.Doc
            },
            {
                "txt", FileType.Doc
            },
            {
                "tex", FileType.Doc
            },
            {
                "wps", FileType.Doc
            },
            {
                "wks", FileType.Doc
            },
            {
                "odt", FileType.Doc
            },
            {
                "sxw", FileType.Doc
            },
            {
                "wpd", FileType.Doc
            },
            {
                "pdf", FileType.Pdf
            },
            {
                "cda", FileType.Audio
            },
            {
                "mid", FileType.Audio
            },
            {
                "midi", FileType.Audio
            },
            {
                "mp3", FileType.Audio
            },
            {
                "mpa", FileType.Audio
            },
            {
                "ogg", FileType.Audio
            },
            {
                "wav", FileType.Audio
            },
            {
                "wma", FileType.Audio
            },
            {
                "wpl", FileType.Audio
            },
            {
                "7z", FileType.Compressed
            },
            {
                "arj", FileType.Compressed
            },
            {
                "deb", FileType.Compressed
            },
            {
                "pkg", FileType.Compressed
            },
            {
                "rar", FileType.Compressed
            },
            {
                "rpm", FileType.Compressed
            },
            {
                "tar.gz", FileType.Compressed
            },
            {
                "gz", FileType.Compressed
            },
            {
                "z", FileType.Compressed
            },
            {
                "zip", FileType.Compressed
            },
            {
                "bin", FileType.DiskImage
            },
            {
                "dmg", FileType.DiskImage
            },
            {
                "iso", FileType.DiskImage
            },
            {
                "toast", FileType.DiskImage
            },
            {
                "csv", FileType.Delimited
            },
            {
                "tsv", FileType.Delimited
            },
            {
                "dat", FileType.Structured
            },
            {
                "xml", FileType.Structured
            },
            {
                "json", FileType.Structured
            },
            {
                "fnt", FileType.Font
            },
            {
                "fon", FileType.Font
            },
            {
                "otf", FileType.Font
            },
            {
                "ttf", FileType.Font
            },
            {
                "ai", FileType.Image
            },
            {
                "bmp", FileType.Image
            },
            {
                "gif", FileType.Image
            },
            {
                "ico", FileType.Image
            },
            {
                "jpeg", FileType.Image
            },
            {
                "jpg", FileType.Image
            },
            {
                "png", FileType.Image
            },
            {
                "ps", FileType.Image
            },
            {
                "psd", FileType.Image
            },
            {
                "svg", FileType.Image
            },
            {
                "tif", FileType.Image
            },
            {
                "tiff", FileType.Image
            },
            {
                "key", FileType.Presentation
            },
            {
                "odp", FileType.Presentation
            },
            {
                "pps", FileType.Presentation
            },
            {
                "ppt", FileType.Presentation
            },
            {
                "pptx", FileType.Presentation
            },
            {
                "ods", FileType.Spreadsheet
            },
            {
                "xlr", FileType.Spreadsheet
            },
            {
                "xls", FileType.Spreadsheet
            },
            {
                "xlsx", FileType.Spreadsheet
            },
            {
                "3g2", FileType.Video
            },
            {
                "3gp", FileType.Video
            },
            {
                "avi", FileType.Video
            },
            {
                "flv", FileType.Video
            },
            {
                "h264", FileType.Video
            },
            {
                "m4v", FileType.Video
            },
            {
                "mkv", FileType.Video
            },
            {
                "mov", FileType.Video
            },
            {
                "mp4", FileType.Video
            },
            {
                "mpg", FileType.Video
            },
            {
                "mpeg", FileType.Video
            },
            {
                "rm", FileType.Video
            },
            {
                "swf", FileType.Video
            },
            {
                "vob", FileType.Video
            },
            {
                "wmv", FileType.Video
            }
        };

    public static DynFile ToDynFile(this FileDto source)
    {
        var result = source.ConvertTo<DynFile>();

        var fmd = new FileMetaData(source.Name);

        if (source.Name.HasValue())
        {
            result.Name = fmd.FileName;
            result.FileExtension = fmd.FileExtension.ToNullIfEmpty();
        }

        result.OriginalFileName = fmd.FileNameAndExtension;

        if (result.FileType == FileType.Unknown)
        {
            result.FileType = ToFileType(result.FileExtension);
        }

        var utcNow = DateTimeHelper.UtcNowTs;

        result.ExpiresAt = utcNow + 3600;
        result.UpdateDateTimeDeleteTrackedValues(source);

        if (result.Id <= 0)
        {
            result.Id = Sequences.Provider.Next();
        }

        result.EdgeId = result.Id.ToEdgeId();
        result.ReferenceId = utcNow.ToStringInvariant();
        result.DynItemType = DynItemType.File;

        return result;
    }

    public static FileDto ToFileDto(this DynFile source)
    {
        var result = source.ConvertTo<FileDto>();

        result.IsConverted = source.FileType != FileType.Video || source.ConvertStatus == FileConvertStatus.Complete;

        result.ConvertStatus = ToFileConvertStatus(source);

        return result;
    }

    public static FileConvertStatus? ToFileConvertStatus(this DynFile source)
        => source.FileType == FileType.Video
               ? source.ConvertStatus
               : null;

    public static void TagFromMedia(this FileMetaData source, DynPublisherMedia media, bool shouldKeep)
        => TagMediaValues(source, media.ContentType, media.IsPermanentMedia || shouldKeep);

    public static void TagMediaValues(this FileMetaData source, PublisherContentType contentType, bool shouldKeep)
    {
        var fileType = source.FileExtension.ToFileType();

        var isMediaType = fileType == FileType.Image || fileType == FileType.Video;

        source.Tags[FileStorageTag.Lifecycle.ToString()] = isMediaType && (shouldKeep || _keepAllPublisherMedia ||
                                                                           !contentType.IsTimeBoxedContentType())
                                                               ? FileStorageTags.LifecycleKeep
                                                               : FileStorageTags.LifecyclePurge;

        source.Tags[FileStorageTag.Privacy.ToString()] = isMediaType && contentType.IsPublicContentType()
                                                             ? FileStorageTags.PrivacyPublic
                                                             : FileStorageTags.PrivacyPrivate;
    }

    public static FileType ToFileType(this string fileExtension)
    {
        if (!fileExtension.HasValue())
        {
            return FileType.Other;
        }

        var fsxLookup = fileExtension.TrimStart('.');

        if (fsxLookup.StartsWithOrdinalCi("doc"))
        {
            return FileType.Doc;
        }

        return FileExtensionFileTypeMap.ContainsKey(fsxLookup)
                   ? FileExtensionFileTypeMap[fsxLookup]
                   : FileType.Other;
    }

    public static bool IsFinalStatus(this DynFile file)
        => IsFinalStatus(file.ConvertStatus);

    public static bool IsFinalStatus(this FileConvertStatus status)
        => status == FileConvertStatus.Canceled || status == FileConvertStatus.Complete || status == FileConvertStatus.Error;

    public static bool StatusCanConvert(this DynFile file)
        => StatusCanConvert(file.ConvertStatus);

    public static bool StatusCanConvert(this FileConvertStatus status)
        => status == FileConvertStatus.Unknown || status == FileConvertStatus.Canceled || status == FileConvertStatus.Error;

    public static FileConvertTypeArgumentsBase ToConvertArguments(this FileConvertInfoRequest request)
        => request.ConvertType switch
           {
               FileConvertType.ImageResize => (request.Width > 0 || request.Height > 0
                                                   ? new ImageConvertTypeArguments
                                                     {
                                                         Width = request.Width,
                                                         Height = request.Height,
                                                         ResizeMode = request.ResizeMode
                                                     }
                                                   : null),
               FileConvertType.VideoThumbnail => new VideoThumbnailConvertGenericTypeArguments
                                                 {
                                                     Width = request.Width,
                                                     Height = request.Height,
                                                     ResizeMode = request.ResizeMode
                                                 },
               FileConvertType.VideoGenericMp4 => VideoConvertGenericTypeArguments.Instance,
               _ => null
           };
}
