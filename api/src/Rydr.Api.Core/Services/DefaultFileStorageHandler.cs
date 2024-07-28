using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Size = SixLabors.ImageSharp.Size;

namespace Rydr.Api.Core.Services;

public class DefaultFileStorageHandler : BaseFileStorageHandler
{
    private DefaultFileStorageHandler() { }

    public static DefaultFileStorageHandler Instance { get; } = new();

    public override FileMetaData GetConvertedFileMeta<T>(long fileId, string fileExtension, T convertArguments, char? dirSeparatorCharacter = null)
        => GetDefaultConvertedFileMeta(fileId, fileExtension, dirSeparatorCharacter);

    public override Task<FileMetaData> ConvertAndStoreAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        => Task.FromResult<FileMetaData>(null);

    public override Task<FileConvertStatus> GetConvertStatusAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        => Task.FromResult(FileConvertStatus.Complete);
}

public abstract class BaseFileStorageHandler : IFileConversionStorageHandler
{
    protected string GetPathPrefix(long fileId)
        => (fileId >= 1000000
                ? fileId.ToStringInvariant()
                : string.Concat("000000", fileId)).Right(6)
                                                  .Reverse();

    public FileMetaData GetDefaultConvertedFileMeta(long fileId, string fileExtension, char? dirSeparatorCharacter = null)
        => GetConvertedFileMetaInternal(fileId, fileExtension, dirSeparatorCharacter);

    public abstract FileMetaData GetConvertedFileMeta<T>(long fileId, string fileExtension, T convertArguments, char? dirSeparatorCharacter = null)
        where T : FileConvertTypeArgumentsBase;

    private FileMetaData GetConvertedFileMetaInternal(long fileId, string fileExtension, char? dirSeparatorCharacter = null)
    {
        var fmd = new FileMetaData(Path.Combine(RydrFileStoragePaths.FilesRoot, GetPathPrefix(fileId)),
                                   string.Concat(fileId, fileExtension.HasValue()
                                                             ? string.Concat(".", fileExtension.TrimStart('.'))
                                                             : string.Empty),
                                   dirSeparatorCharacter);

        return fmd;
    }

    public abstract Task<FileMetaData> ConvertAndStoreAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        where T : FileConvertTypeArgumentsBase;

    protected FileConvertStatus GetConvertStatus(DynFile dynFile)
        => FileConvertStatus.Complete;

    public abstract Task<FileConvertStatus> GetConvertStatusAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        where T : FileConvertTypeArgumentsBase;

    protected async Task<FileMetaData> ResizeImageAsync(IFileStorageProvider fileStorageProvider, FileMetaData sourceFile, FileMetaData targetFile,
                                                        int width, int height, ImageResizeMode resizeMode)
    {
        var exists = await fileStorageProvider.ExistsAsync(targetFile);

        if (exists)
        {
            return targetFile;
        }

        try
        {
            await fileStorageProvider.GetStreamIntoAsync(sourceFile);

            using(var img = await Image.LoadAsync(sourceFile.Stream))
            {
                var resizeOptions = new ResizeOptions
                                    {
                                        Mode = ((int)resizeMode).TryToEnum(ResizeMode.Crop),
                                        Position = AnchorPositionMode.Center,
                                        Size = new Size(width, height),
                                        Compand = true
                                    };

                img.Mutate(x => x.Resize(resizeOptions));

                targetFile.Stream = new MemoryStream();

                await img.SaveAsJpegAsync(targetFile.Stream);

                await fileStorageProvider.StoreAsync(targetFile, new FileStorageOptions
                                                                 {
                                                                     Encrypt = true,
                                                                     ContentType = "image/jpeg",
                                                                     StorageClass = RydrEnvironment.IsReleaseEnvironment
                                                                                        ? FileStorageClass.Intelligent
                                                                                        : FileStorageClass.Standard,
                                                                     ContentDisposition = FileStorageContentDisposition.Attachment
                                                                 });
            }
        }
        finally
        {
            sourceFile.Stream.TryClose();
            sourceFile.Stream.TryDispose();
            targetFile.Stream.TryClose();
            targetFile.Stream.TryDispose();
        }

        return targetFile;
    }
}
