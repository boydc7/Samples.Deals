using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using ServiceStack.Web;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IFileStorageService
{
    Task<DynFile> TryGetFileAsync(long id);
    IAsyncEnumerable<DynFile> GetFilesAsync(IEnumerable<long> ids);

    Task<FileUploadInfo> GetUploadInfoAsync(long id, DynFile dynFile = null);

    Task<FileDownloadInfo> GetDownloadInfoAsync<T>(long id, DynFile dynFile = null, bool isPreview = false,
                                                   T convertArguments = null, int expiresInMinutes = 9)
        where T : FileConvertTypeArgumentsBase;

    Task<FileMetaData> DownloadAsync(long fileId);

    Task UploadAsync(Stream fileStream, long fileId);
    Task UploadAsync(IHttpFile httpFile, long fileId);
    Task ConfirmUploadAsync(long id);
    Task DeleteFileAsync(long id);

    Task<FileMetaData> ConvertAndStoreAsync<T>(DynFile dynFile, T convertArguments)
        where T : FileConvertTypeArgumentsBase;

    Task<FileConvertStatus> RefreshConvertStatusAsync<T>(DynFile dynFile, T convertArguments)
        where T : FileConvertTypeArgumentsBase;
}
