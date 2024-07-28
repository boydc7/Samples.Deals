using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services;

public class S3FileStorageService : BaseFileStorageService
{
    private readonly Dictionary<FileType, Action<DynFile>> _onConfirmedUploadCompleteActionMap;

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly IDeferRequestsService _deferRequestsService;

    public S3FileStorageService(IFileStorageProvider fileStorageProvider,
                                IPocoDynamo dynamoDb,
                                IAuthorizationService authorizationService,
                                IDeferRequestsService deferRequestsService)
        : base(fileStorageProvider, dynamoDb, authorizationService)
    {
        _deferRequestsService = deferRequestsService;

        _onConfirmedUploadCompleteActionMap = new Dictionary<FileType, Action<DynFile>>
                                              {
                                                  {
                                                      FileType.Video, f => _deferRequestsService.DeferRequest(new ConvertFile
                                                                                                              {
                                                                                                                  FileId = f.Id,
                                                                                                                  ConvertType = FileConvertType.VideoGenericMp4
                                                                                                              })
                                                  }
                                              };
    }

    protected override char? DirSeparatorCharacter => '/';

    public override async Task<FileUploadInfo> GetUploadInfoAsync(long id, DynFile dynFile = null)
    {
        if (dynFile == null)
        {
            dynFile = await GetDynFileAsync(id);
        }
        else
        {
            Guard.AgainstArgumentOutOfRange(dynFile.Id != id, nameof(id));
        }

        var s3FileMeta = GetFileMeta(id, dynFile.FileExtension);

        var url = _fileStorageProvider.GetUploadUrl(9, s3FileMeta);

        return new FileUploadInfo
               {
                   FileId = id,
                   Url = url
               };
    }

    protected override FileDownloadInfo DoGetDownloadInfo(DynFile dynFile, FileMetaData forFile, bool isPreview = false, int expiresInMinutes = 9)
    {
        forFile.DisplayName = dynFile.OriginalFileName;

        var url = _fileStorageProvider.GetDownloadUrl(expiresInMinutes.Gz(9), forFile, isPreview);

        return new FileDownloadInfo
               {
                   FileId = dynFile.Id,
                   Url = url
               };
    }

    protected override async Task<long> GetSizeInBytesAsync(long id, DynFile dynFile = null)
    {
        if (dynFile == null)
        {
            dynFile = await GetDynFileAsync(id, true);
        }
        else
        {
            Guard.AgainstArgumentOutOfRange(dynFile.Id != id, nameof(id));
        }

        var s3FileMeta = GetFileMeta(id, dynFile.FileExtension);

        var result = await Try.GetAsync(() => _fileStorageProvider.GetSizeInBytesAsync(s3FileMeta));

        return result;
    }

    protected override void OnConfirmedUploadComplete(DynFile dynInfo)
    {
        if (!_onConfirmedUploadCompleteActionMap.ContainsKey(dynInfo.FileType))
        {
            return;
        }

        _onConfirmedUploadCompleteActionMap[dynInfo.FileType](dynInfo);
    }
}
