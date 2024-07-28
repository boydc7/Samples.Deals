using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Services;

public class VideoThumbnailConvertFileStorageHandler : BaseFileStorageHandler
{
    private readonly IFileConversionStorageHandler _videoStorageHandler;

    public VideoThumbnailConvertFileStorageHandler(IFileConversionStorageHandler videoStorageHandler)
    {
        _videoStorageHandler = videoStorageHandler;
    }

    public override Task<FileConvertStatus> GetConvertStatusAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        => _videoStorageHandler.GetConvertStatusAsync(fileStorageProvider, dynFile, VideoConvertGenericTypeArguments.Instance, dirSeparatorCharacter);

    public override FileMetaData GetConvertedFileMeta<T>(long fileId, string fileExtension, T convertArguments, char? dirSeparatorCharacter = null)
    {
        if (convertArguments == null || convertArguments.GetType() != typeof(VideoThumbnailConvertGenericTypeArguments))
        {
            return GetDefaultConvertedFileMeta(fileId, fileExtension, dirSeparatorCharacter);
        }

        var thumbnailConvertArgs = convertArguments as VideoThumbnailConvertGenericTypeArguments;

        return GetThumbnailConvertedFileMeta(fileId, thumbnailConvertArgs, dirSeparatorCharacter);
    }

    public override async Task<FileMetaData> ConvertAndStoreAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
    {
        if (convertArguments == null || convertArguments.GetType() != typeof(VideoThumbnailConvertGenericTypeArguments))
        {
            return null;
        }

        var thumbnailArguments = convertArguments as VideoThumbnailConvertGenericTypeArguments;

        var fileType = dynFile.FileExtension.ToFileType();

        Guard.AgainstArgumentOutOfRange(fileType != FileType.Video, "Only video files have thumbnails");

        var sourceMetaData = GetDefaultThumbnailMeta(dynFile.Id, dirSeparatorCharacter);
        var resizeIntoMetaData = GetThumbnailConvertedFileMeta(dynFile.Id, thumbnailArguments, dirSeparatorCharacter);

        var result = await ResizeImageAsync(fileStorageProvider, sourceMetaData, resizeIntoMetaData, thumbnailArguments.Width,
                                            thumbnailArguments.Height, thumbnailArguments.ResizeMode);

        return result;
    }

    private FileMetaData GetDefaultThumbnailMeta(long fileId, char? dirSeparatorCharacter)
        => new(Path.Combine(RydrFileStoragePaths.VideosRoot, GetPathPrefix(fileId)),
               string.Concat(fileId, VideoConversionHelpers.GenericVideoThumbnailSuffix, ".",
                             VideoConversionHelpers.GenericVideoThumbnailDefaultSequence, ".",
                             VideoConversionHelpers.GenericVideoThumbnailExtension),
               dirSeparatorCharacter);

    private FileMetaData GetThumbnailConvertedFileMeta(long fileId, VideoThumbnailConvertGenericTypeArguments thumbnailConvertArgs, char? dirSeparatorCharacter = null)
    {
        if (thumbnailConvertArgs == null ||
            (thumbnailConvertArgs.Width <= 0 && thumbnailConvertArgs.Height <= 0) ||
            (thumbnailConvertArgs.Width == VideoConversionHelpers.GenericVideoThumbnailWidth && thumbnailConvertArgs.Height == VideoConversionHelpers.GenericVideoThumbnailHeight))
        {
            var defaultThumbnailMeta = GetDefaultThumbnailMeta(fileId, dirSeparatorCharacter);

            defaultThumbnailMeta.Tags.Add(thumbnailConvertArgs.Tag.TagName, thumbnailConvertArgs.Tag.TagValue);

            return defaultThumbnailMeta;
        }

        // Resized thumbnail
        var convertedMeta = new FileMetaData(Path.Combine(RydrFileStoragePaths.VideosRoot, GetPathPrefix(fileId)),
                                             string.Concat(fileId, VideoConversionHelpers.GenericVideoThumbnailSuffix,
                                                           "_", thumbnailConvertArgs.Width, "x", thumbnailConvertArgs.Height, "x",
                                                           (int)thumbnailConvertArgs.ResizeMode, ".", VideoConversionHelpers.GenericVideoThumbnailDefaultSequence,
                                                           ".", VideoConversionHelpers.GenericVideoThumbnailExtension),
                                             dirSeparatorCharacter);

        convertedMeta.Tags.Add(thumbnailConvertArgs.Tag.TagName, thumbnailConvertArgs.Tag.TagValue);

        return convertedMeta;
    }
}
