using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Web;

namespace Rydr.Api.Core.Services
{
    public abstract class BaseFileStorageService : IFileStorageService
    {
        private readonly Dictionary<FileConvertType, IFileConversionStorageHandler> _fileConversionHandlerTypeMap;

        protected readonly IFileStorageProvider _fileStorageProvider;
        private readonly IPocoDynamo _dynamoDb;
        private readonly IAuthorizationService _authorizationService;
        private readonly string _localDownloadPath = Path.Combine(RydrFileStoragePaths.LocalFileStorageRoot, "files");

        protected BaseFileStorageService(IFileStorageProvider fileStorageProvider,
                                         IPocoDynamo dynamoDb,
                                         IAuthorizationService authorizationService)
        { // Should be injected...
            var videoStorageHandler = new GenericVideoFileStorageHandler(dynamoDb);

            _fileConversionHandlerTypeMap = new Dictionary<FileConvertType, IFileConversionStorageHandler>
                                            {
                                                {
                                                    FileConvertType.ImageResize, ImageConvertFileStorageHandler.Instance
                                                },
                                                {
                                                    FileConvertType.VideoGenericMp4, videoStorageHandler
                                                },
                                                {
                                                    FileConvertType.VideoThumbnail, new VideoThumbnailConvertFileStorageHandler(videoStorageHandler)
                                                }
                                            };

            _fileStorageProvider = fileStorageProvider;
            _dynamoDb = dynamoDb;
            _authorizationService = authorizationService;

            CleanupLocal();
        }

        protected virtual char? DirSeparatorCharacter => null;

        public abstract Task<FileUploadInfo> GetUploadInfoAsync(long id, DynFile dynFile = null);

        protected abstract FileDownloadInfo DoGetDownloadInfo(DynFile dynFile, FileMetaData forFile, bool isPreview = false, int expiresInMinutes = 9);

        protected abstract Task<long> GetSizeInBytesAsync(long id, DynFile dynFile = null);

        protected virtual void OnConfirmedUploadComplete(DynFile dynInfo) { }

        private void CleanupLocal()
        {
            PathHelper.Delete(_localDownloadPath);
            PathHelper.Create(_localDownloadPath);
        }

        public Task<DynFile> TryGetFileAsync(long id)
            => _dynamoDb.GetItemAsync<DynFile>(id.ToItemDynamoId());

        public IAsyncEnumerable<DynFile> GetFilesAsync(IEnumerable<long> ids)
            => _dynamoDb.GetItemsAsync<DynFile>(ids.Select(i => i.ToItemDynamoId()));

        public async Task<FileDownloadInfo> GetDownloadInfoAsync<T>(long id, DynFile dynFile = null, bool isPreview = false,
                                                                    T convertArguments = null, int expiresInMinutes = 9)
            where T : FileConvertTypeArgumentsBase
        {
            if (dynFile == null)
            {
                dynFile = await GetDynFileAsync(id);
            }
            else
            {
                Guard.AgainstArgumentOutOfRange(dynFile.Id != id, nameof(id));
            }

            if (convertArguments == null)
            { // Nothing to do but return the thing
                return DoGetDownloadInfo(dynFile, GetFileMeta(dynFile), isPreview, expiresInMinutes);
            }

            var storedFileMeta = await ConvertAndStoreAsync(dynFile, convertArguments);

            if (storedFileMeta == null)
            {
                return DoGetDownloadInfo(dynFile, GetFileMeta(dynFile), isPreview, expiresInMinutes);
            }

            var downloadInfo = DoGetDownloadInfo(dynFile, storedFileMeta, isPreview, expiresInMinutes);

            return downloadInfo;
        }

        public async Task<FileMetaData> ConvertAndStoreAsync<T>(DynFile dynFile, T convertArguments)
            where T : FileConvertTypeArgumentsBase
        {
            if (convertArguments == null ||
                !_fileConversionHandlerTypeMap.ContainsKey(convertArguments.ConvertedType))
            {
                return null;
            }

            var result = await _fileConversionHandlerTypeMap[convertArguments.ConvertedType].ConvertAndStoreAsync(_fileStorageProvider, dynFile, convertArguments, DirSeparatorCharacter);

            return result;
        }

        public async Task<FileConvertStatus> RefreshConvertStatusAsync<T>(DynFile dynFile, T convertArguments)
            where T : FileConvertTypeArgumentsBase
        {
            if (convertArguments == null ||
                !_fileConversionHandlerTypeMap.ContainsKey(convertArguments.ConvertedType))
            {
                return FileConvertStatus.Unknown;
            }

            var status = await _fileConversionHandlerTypeMap[convertArguments.ConvertedType].GetConvertStatusAsync(_fileStorageProvider, dynFile, convertArguments, DirSeparatorCharacter);

            if (status == FileConvertStatus.Complete)
            {
                dynFile.ConvertStatus = status;

                await _dynamoDb.PutItemAsync(dynFile);
            }

            return status;
        }

        public Task DeleteFileAsync(long id)
            => _dynamoDb.SoftDeleteByIdAsync<DynFile>(id, id.ToEdgeId());

        public async Task<FileMetaData> DownloadAsync(long fileId)
        {
            var fileDynInfo = await GetDynFileAsync(fileId);

            await _authorizationService.VerifyAccessToAsync(fileDynInfo);

            var remoteFileMeta = GetFileMeta(fileDynInfo);

            var localFileMeta = new FileMetaData(Path.Combine(_localDownloadPath, fileId.ToStringInvariant()),
                                                 fileDynInfo.OriginalFileName.Coalesce(remoteFileMeta.FileNameAndExtension),
                                                 DirSeparatorCharacter);

            await _fileStorageProvider.DownloadAsync(remoteFileMeta, localFileMeta);

            localFileMeta.DisplayName = fileDynInfo.OriginalFileName;

            return localFileMeta;
        }

        public async Task UploadAsync(Stream fileStream, long fileId)
        {
            var fileDynInfo = await GetDynFileAsync(fileId);

            await _authorizationService.VerifyAccessToAsync(fileDynInfo);

            var localPathRoot = Path.Combine(_localDownloadPath, fileId.ToStringInvariant());

            PathHelper.Create(localPathRoot);

            var localFileMeta = new FileMetaData(localPathRoot,
                                                 string.Concat(fileId,
                                                               fileDynInfo.FileExtension.HasValue()
                                                                   ? "."
                                                                   : string.Empty,
                                                               fileDynInfo.FileExtension),
                                                 DirSeparatorCharacter);

            await FinishUploadAsync(fileDynInfo, localFileMeta,
                                    f =>
                                    {
                                        using(var localFile = File.Create(f.FullName))
                                        {
                                            fileStream.WriteTo(localFile);
                                        }
                                    },
                                    fileDynInfo.FileExtension);
        }

        public async Task UploadAsync(IHttpFile httpFile, long fileId)
        {
            var fileDynInfo = await GetDynFileAsync(fileId);

            await _authorizationService.VerifyAccessToAsync(fileDynInfo);

            var localPathRoot = Path.Combine(_localDownloadPath, fileId.ToStringInvariant());

            var httpFileMeta = new FileMetaData(localPathRoot, httpFile.FileName, DirSeparatorCharacter);

            var extensionToUse = fileDynInfo.FileExtension.Coalesce(httpFileMeta.FileExtension);

            var localFileMeta = new FileMetaData(localPathRoot,
                                                 string.Concat(fileId,
                                                               extensionToUse.HasValue()
                                                                   ? "."
                                                                   : string.Empty,
                                                               extensionToUse),
                                                 DirSeparatorCharacter);

            PathHelper.Create(localPathRoot);

            await FinishUploadAsync(fileDynInfo, localFileMeta, f => httpFile.SaveTo(f.FullName), extensionToUse);
        }

        public async Task ConfirmUploadAsync(long id)
        {
            var dynFile = await GetDynFileAsync(id);

            if (dynFile.ExpiresAt <= 0)
            {
                return;
            }

            await _authorizationService.VerifyAccessToAsync(dynFile);

            var exists = await _fileStorageProvider.ExistsAsync(GetFileMeta(dynFile));

            Guard.AgainstRecordNotFound(!exists, dynFile.Name);

            await FinishConfirmedUploadAsync(dynFile);
        }

        private async Task FinishUploadAsync(DynFile fileDynInfo, FileMetaData localFileMeta, Action<FileMetaData> localFileCreateAction, string extensionToUse)
        {
            try
            {
                localFileCreateAction(localFileMeta);

                var remoteFileMeta = GetFileMeta(fileDynInfo.Id, extensionToUse);

                await _fileStorageProvider.StoreAsync(localFileMeta, remoteFileMeta, new FileStorageOptions
                                                                                     {
                                                                                         ContentDisposition = FileStorageContentDisposition.Attachment,
                                                                                         Encrypt = true
                                                                                     });

                await FinishConfirmedUploadAsync(fileDynInfo);
            }
            finally
            {
                FileHelper.Delete(localFileMeta.FullName);
            }
        }

        private async Task FinishConfirmedUploadAsync(DynFile dynInfo)
        {
            Guard.AgainstNullArgument(dynInfo == null, nameof(dynInfo));

            if (dynInfo.ExpiresAt <= 0)
            {
                return;
            }

            var contentLength = await GetSizeInBytesAsync(dynInfo.Id, dynInfo);

            try
            {
                await _dynamoDb.UpdateItemAsync(dynInfo.Id, dynInfo.Id.ToEdgeId(),
                                                () => new DynFile
                                                      {
                                                          ExpiresAt = 0,
                                                          ContentLength = contentLength
                                                      });

                OnConfirmedUploadComplete(dynInfo);
            }
            catch
            {
                await _dynamoDb.UpdateItemAsync(dynInfo.Id, dynInfo.Id.ToEdgeId(),
                                                () => new DynFile
                                                      {
                                                          ExpiresAt = DateTimeHelper.UtcNowTs + 3600
                                                      });

                throw;
            }
        }

        protected async Task<DynFile> GetDynFileAsync(long fileId, bool skipAuth = false)
        {
            var fileDynInfo = await _dynamoDb.GetItemAsync<DynFile>(fileId.ToItemDynamoId());

            if (!skipAuth)
            {
                await _authorizationService.VerifyAccessToAsync(fileDynInfo);
            }

            return fileDynInfo;
        }

        protected FileMetaData GetFileMeta(long id, string fileExtension)
            => GetFileMeta<FileConvertTypeArgumentsBase>(id, fileExtension);

        protected FileMetaData GetFileMeta(DynFile dynFile)
            => GetFileMeta<FileConvertTypeArgumentsBase>(dynFile.Id, dynFile.FileExtension);

        protected FileMetaData GetFileMeta<T>(long id, string fileExtension, T convertArguments = null)
            where T : FileConvertTypeArgumentsBase
        {
            var fmd = convertArguments == null || !_fileConversionHandlerTypeMap.ContainsKey(convertArguments.ConvertedType)
                          ? DefaultFileStorageHandler.Instance.GetDefaultConvertedFileMeta(id, fileExtension, DirSeparatorCharacter)
                          : _fileConversionHandlerTypeMap[convertArguments.ConvertedType].GetConvertedFileMeta(id, fileExtension, convertArguments, DirSeparatorCharacter);

            return fmd;
        }
    }
}
