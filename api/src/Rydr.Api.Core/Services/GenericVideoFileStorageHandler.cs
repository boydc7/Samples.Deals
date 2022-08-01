using System.IO;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services
{
    public class GenericVideoFileStorageHandler : BaseFileStorageHandler
    {
        private readonly IPocoDynamo _dynamoDb;
        private readonly IVideoConversionService _videoConversionService = HostContext.Container.Resolve<IVideoConversionService>();

        public GenericVideoFileStorageHandler(IPocoDynamo dynamoDb)
        {
            _dynamoDb = dynamoDb;
        }

        public override async Task<FileConvertStatus> GetConvertStatusAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        {
            if (convertArguments == null || convertArguments.GetType() != typeof(VideoConvertGenericTypeArguments))
            {
                return GetConvertStatus(dynFile);
            }

            var sourceFileMeta = GetDefaultConvertedFileMeta(dynFile.Id, dynFile.FileExtension, dirSeparatorCharacter);
            var resizeIntoMetaData = GetConvertedFileMeta(dynFile.Id, dynFile.FileExtension, convertArguments, dirSeparatorCharacter);

            var status = await _videoConversionService.GetStatusAsync(sourceFileMeta, resizeIntoMetaData.FolderName);

            return status;
        }

        public override FileMetaData GetConvertedFileMeta<T>(long fileId, string fileExtension, T convertArguments, char? dirSeparatorCharacter = null)
        {
            if (convertArguments == null || convertArguments.GetType() != typeof(VideoConvertGenericTypeArguments))
            {
                return GetDefaultConvertedFileMeta(fileId, fileExtension, dirSeparatorCharacter);
            }

            var fmd = new FileMetaData(Path.Combine(RydrFileStoragePaths.VideosRoot, GetPathPrefix(fileId)),
                                       string.Concat(fileId, VideoConversionHelpers.GenericVideoSuffix, ".", VideoConversionHelpers.GenericVideoExtension),
                                       dirSeparatorCharacter);

            fmd.Tags.Add(convertArguments.Tag.TagName, convertArguments.Tag.TagValue);

            return fmd;
        }

        public override async Task<FileMetaData> ConvertAndStoreAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        {
            if (convertArguments == null || convertArguments.GetType() != typeof(VideoConvertGenericTypeArguments))
            {
                return null;
            }

            var fileType = dynFile.FileExtension.ToFileType();

            Guard.AgainstArgumentOutOfRange(fileType != FileType.Video, "Only video files can be handled");

            var resizeIntoMetaData = GetConvertedFileMeta(dynFile.Id, dynFile.FileExtension, convertArguments, dirSeparatorCharacter);

            var exists = await fileStorageProvider.ExistsAsync(resizeIntoMetaData);

            if (exists)
            {
                return resizeIntoMetaData;
            }

            if (!dynFile.StatusCanConvert())
            {
                return null;
            }

            var sourceFileMeta = GetDefaultConvertedFileMeta(dynFile.Id, dynFile.FileExtension, dirSeparatorCharacter);

            await _videoConversionService.ConvertAsync(sourceFileMeta, resizeIntoMetaData.FolderName);

            if (dynFile.ConvertStatus != FileConvertStatus.InProgress)
            {
                dynFile.ConvertStatus = FileConvertStatus.InProgress;
                await _dynamoDb.PutItemAsync(dynFile);
            }

            // NULL returned correctly...if it gets here, the video isn't available yet and is transcoding now...
            return null;
        }
    }
}
