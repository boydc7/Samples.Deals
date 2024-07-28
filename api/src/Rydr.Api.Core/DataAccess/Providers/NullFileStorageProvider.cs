using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;

namespace Rydr.Api.Core.DataAccess.Providers;

public class NullFileStorageProvider : IFileStorageProvider
{
    public FileStorageProviderType ProviderType => FileStorageProviderType.Null;

    public Task StoreAsync(FileMetaData sourceFileSystemMeta, FileMetaData targetMeta, FileStorageOptions options = null) => Task.CompletedTask;

    public Task CopyToAsync(FileMetaData thisProviderMeta, IFileStorageProvider targetProvider, FileMetaData targetMeta, FileStorageOptions options = null) => Task.CompletedTask;

    public Task<Dictionary<string, string>> GetTagsAsync(FileMetaData fileMetaData) => Task.FromResult<Dictionary<string, string>>(null);
    public Task SetTagsAsync(FileMetaData fileMetaData) => Task.CompletedTask;

    public Task DownloadAsync(FileMetaData thisProviderMeta, FileMetaData downloadToMeta) => Task.CompletedTask;

    public Task StoreAsync(FileMetaData fileMetaData, FileStorageOptions options = null) => Task.CompletedTask;

    public Task DeleteAsync(FileMetaData fileMetaData) => Task.CompletedTask;

    public Task<long> GetSizeInBytesAsync(FileMetaData fileMetaData) => Task.FromResult(0L);

    public string GetDownloadUrl(int expiresInMinutes, string folder, string filename, bool isPreview = false) => "";

    public string GetDownloadUrl(int expiresInMinutes, FileMetaData fileMetaData, bool isPreview = false) => "";

    public string GetUploadUrl(int expiresInMinutes, FileMetaData fileMetaData) => "";

    public Task<bool> ExistsAsync(FileMetaData fileMetaData) => Task.FromResult(false);

    public Task TryDeleteFolderAsync(string folderName, bool recursive) => Task.CompletedTask;

    public Task DeleteFolderAsync(string folderName, bool recursive) => Task.CompletedTask;

    public Task CreateFolderAsync(string folderName) => Task.CompletedTask;

    public Task MoveAsync(string source, string target, FileStorageOptions options = null) => Task.CompletedTask;

    public Task<byte[]> GetOrDefaultAsync(FileMetaData fileMetaData, byte[] defaultValue = null) => Task.FromResult(defaultValue);

    public Task<byte[]> GetAsync(FileMetaData fileMetaData) => Task.FromResult<byte[]>(null);

    public Task GetStreamIntoAsync(FileMetaData fileMetaData) => Task.CompletedTask;

#pragma warning disable 1998
    public async IAsyncEnumerable<string> ListFolderAsync(string path, bool includePath = true, bool recursive = false)
#pragma warning restore 1998
    {
        yield break;
    }
}
