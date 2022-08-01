using System.IO;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Services
{
    public class ImageConvertFileStorageHandler : BaseFileStorageHandler
    {
        private ImageConvertFileStorageHandler() { }

        public static ImageConvertFileStorageHandler Instance { get; } = new ImageConvertFileStorageHandler();

        public override Task<FileConvertStatus> GetConvertStatusAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
            => Task.FromResult(GetConvertStatus(dynFile));

        public override FileMetaData GetConvertedFileMeta<T>(long fileId, string fileExtension, T convertArguments, char? dirSeparatorCharacter = null)
        {
            if (convertArguments == null || convertArguments.GetType() != typeof(ImageConvertTypeArguments))
            {
                return GetDefaultConvertedFileMeta(fileId, fileExtension, dirSeparatorCharacter);
            }

            if (!(convertArguments is ImageConvertTypeArguments imageConvertArgs) || (imageConvertArgs.Width <= 0 && imageConvertArgs.Width <= 0))
            {
                return GetDefaultConvertedFileMeta(fileId, fileExtension, dirSeparatorCharacter);
            }

            var fmd = new FileMetaData(Path.Combine(RydrFileStoragePaths.FilesRoot, GetPathPrefix(fileId)),
                                       string.Concat(fileId, "_", imageConvertArgs.Width, "x", imageConvertArgs.Height, "x",
                                                     (int)imageConvertArgs.ResizeMode, ".", imageConvertArgs.Extension));

            fmd.Tags.Add(imageConvertArgs.Tag.TagName, imageConvertArgs.Tag.TagValue);

            return fmd;
        }

        public override async Task<FileMetaData> ConvertAndStoreAsync<T>(IFileStorageProvider fileStorageProvider, DynFile dynFile, T convertArguments, char? dirSeparatorCharacter = null)
        {
            if (convertArguments == null || convertArguments.GetType() != typeof(ImageConvertTypeArguments))
            {
                return null;
            }

            if (!(convertArguments is ImageConvertTypeArguments imageConvertArgs))
            {
                return null;
            }

            var fileType = dynFile.FileExtension.ToFileType();

            Guard.AgainstArgumentOutOfRange(fileType != FileType.Image, "Only image files can be resized");

            var sourceMetaData = GetDefaultConvertedFileMeta(dynFile.Id, dynFile.FileExtension, dirSeparatorCharacter);
            var resizeIntoMetaData = GetConvertedFileMeta(dynFile.Id, dynFile.FileExtension, convertArguments, dirSeparatorCharacter);

            var result = await ResizeImageAsync(fileStorageProvider, sourceMetaData, resizeIntoMetaData, imageConvertArgs.Width,
                                                imageConvertArgs.Height, imageConvertArgs.ResizeMode);

            return result;
        }
    }
}
