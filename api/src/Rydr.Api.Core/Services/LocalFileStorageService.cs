using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Files;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services;

public class LocalFileStorageService : BaseFileStorageService
{
    public LocalFileStorageService(IFileStorageProvider fileStorageProvider,
                                   IPocoDynamo dynamoDb,
                                   IAuthorizationService authorizationService)
        : base(fileStorageProvider, dynamoDb, authorizationService) { }

    public override Task<FileUploadInfo> GetUploadInfoAsync(long id, DynFile dynFile = null)
        => Task.FromResult(new FileUploadInfo
                           {
                               FileId = id,
                               Url = string.Concat(RydrUrls.WebHostUri.AbsoluteUri.TrimEnd('/'),
                                                   new UploadRydrFile
                                                   {
                                                       Id = id
                                                   }.ToPutUrl())
                           });

    protected override FileDownloadInfo DoGetDownloadInfo(DynFile dynFile, FileMetaData forFile, bool isPreview = false, int expiresInMinutes = 9)
        => new()
           {
               FileId = dynFile.Id,
               Url = string.Concat(RydrUrls.WebHostUri.AbsoluteUri.TrimEnd('/'),
                                   new DownloadRydrFile
                                   {
                                       Id = dynFile.Id,
                                       IsPreview = isPreview
                                   }.ToGetUrl())
           };

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

        var localFileMeta = GetFileMeta(id, dynFile.FileExtension);

        var result = await Try.GetAsync(() => _fileStorageProvider.GetSizeInBytesAsync(localFileMeta));

        return result;
    }
}
