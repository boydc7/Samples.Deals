using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Models;

namespace Rydr.Api.Core.Interfaces.DataAccess;

public interface IFileStorageProvider
{
    FileStorageProviderType ProviderType { get; }
    Task<Dictionary<string, string>> GetTagsAsync(FileMetaData fileMetaData);
    Task SetTagsAsync(FileMetaData fileMetaData);
    Task DownloadAsync(FileMetaData thisProviderMeta, FileMetaData downloadToMeta);
    Task CopyToAsync(FileMetaData thisProviderMeta, IFileStorageProvider targetProvider, FileMetaData targetMeta, FileStorageOptions options = null);
    Task<byte[]> GetAsync(FileMetaData fileData);
    Task<byte[]> GetOrDefaultAsync(FileMetaData fileMetaData, byte[] defaultValue = null);
    Task StoreAsync(FileMetaData fileData, FileStorageOptions options = null);
    Task StoreAsync(FileMetaData sourceMeta, FileMetaData targetMeta, FileStorageOptions options = null);
    Task DeleteAsync(FileMetaData fileData);
    Task TryDeleteFolderAsync(string folderName, bool recursive);
    Task DeleteFolderAsync(string folderName, bool recursive);
    Task CreateFolderAsync(string folderName);
    Task MoveAsync(string source, string target, FileStorageOptions options = null);
    Task<bool> ExistsAsync(FileMetaData fileData);
    Task<long> GetSizeInBytesAsync(FileMetaData fileMeta);
    string GetDownloadUrl(int expiresInMinutes, string folder, string filename, bool isPreview = false);
    string GetDownloadUrl(int expiresInMinutes, FileMetaData fileMetaData, bool isPreview = false);
    string GetUploadUrl(int expiresInMinutes, FileMetaData fileMetaData);
    IAsyncEnumerable<string> ListFolderAsync(string folderName, bool includePath = true, bool recursive = false);
    Task GetStreamIntoAsync(FileMetaData fileMetaData);
}
