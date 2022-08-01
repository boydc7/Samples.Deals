using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Services.Internal;
using ServiceStack;

namespace Rydr.Api.Core.DataAccess.Providers
{
    public class FileSystemFileStorageProvider : IFileStorageProvider
    {
        public async Task DownloadAsync(FileMetaData thisProviderMeta, FileMetaData downloadToMeta)
        {
            await CreateFolderAsync(downloadToMeta.FolderName);

            FileHelper.Copy(thisProviderMeta.FullName, downloadToMeta.FullName);
        }

        public FileStorageProviderType ProviderType => FileStorageProviderType.FileSystem;

        // Store method with multiple FileMeta objects assumes the source is on the filesystem (or handles that case)
        // so not much logic needed here aside from telling the target to store it in it's own way
        public Task CopyToAsync(FileMetaData thisProviderMeta, IFileStorageProvider targetProvider, FileMetaData targetMeta, FileStorageOptions options = null) =>
            targetProvider.StoreAsync(thisProviderMeta, targetMeta, options);

        public async Task StoreAsync(FileMetaData sourceFileSystemMeta, FileMetaData targetMeta, FileStorageOptions options = null)
        {
            await CreateFolderAsync(targetMeta.FolderName);

            await using(var sourceStream = new FileStream(sourceFileSystemMeta.FullName, FileMode.Open, FileAccess.Read))
            {
                await SaveToDiskAsync(sourceStream, targetMeta.FullName);
            }
        }

        public async Task StoreAsync(FileMetaData fileMetaData, FileStorageOptions options = null)
        {
            await CreateFolderAsync(fileMetaData.FolderName);

            if (fileMetaData.Stream != null)
            {
                await SaveToDiskAsync(fileMetaData.Stream, fileMetaData.FullName);
            }
            else
            {
                await using(var sourceStream = new MemoryStream(fileMetaData.Bytes, false))
                {
                    await SaveToDiskAsync(sourceStream, fileMetaData.FullName);
                }
            }
        }

        public Task<Dictionary<string, string>> GetTagsAsync(FileMetaData fileMetaData) => Task.FromResult<Dictionary<string, string>>(null);
        public Task SetTagsAsync(FileMetaData fileMetaData) => Task.CompletedTask;

        public Task<long> GetSizeInBytesAsync(FileMetaData fileMetaData)
        {
            var fi = new FileInfo(fileMetaData.FullName);

            return Task.FromResult(fi.Length);
        }

        public string GetDownloadUrl(int expiresInMinutes, string folder, string filename, bool isPreview = false)
        {
            var filemeta = new FileMetaData(folder, filename);

            return GetDownloadUrl(expiresInMinutes, filemeta, isPreview);
        }

        public string GetDownloadUrl(int expiresInMinutes, FileMetaData data, bool isPreview = false) => data.FullName;

        public Task GetStreamIntoAsync(FileMetaData fileMetaData)
        {
            fileMetaData.Stream = new FileStream(fileMetaData.FullName, FileMode.Open, FileAccess.Read);

            return Task.CompletedTask;
        }

        public string GetUploadUrl(int expiresInMinutes, FileMetaData fileMetaData) => fileMetaData.FullName;

        public Task<bool> ExistsAsync(FileMetaData fileMetaData) => Task.FromResult(FileHelper.Exists(fileMetaData.FullName));

        public Task MoveAsync(string source, string target, FileStorageOptions options = null)
        {
            FileHelper.Move(source, target);

            return Task.CompletedTask;
        }

        public async Task DeleteAsync(FileMetaData fileMetaData)
        {
            if (await FileExistsAsync(fileMetaData.FullName))
            {
                FileHelper.Delete(fileMetaData.FullName);
            }
        }

        public async Task<byte[]> GetOrDefaultAsync(FileMetaData fileMetaData, byte[] defaultValue = null)
        {
            if (!await ExistsAsync(fileMetaData))
            {
                return defaultValue;
            }

            var result = await GetAsync(fileMetaData);

            return result;
        }

        public async Task<byte[]> GetAsync(FileMetaData fileMetaData)
        {
            if (!await FileExistsAsync(fileMetaData.FullName))
            {
                return null;
            }

            var bytes = await FileHelper.ReadAllBytesAsync(fileMetaData.FullName);

            return bytes;
        }

        public Task CreateFolderAsync(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                PathHelper.Create(path);
            }

            return Task.CompletedTask;
        }

        public async Task TryDeleteFolderAsync(string path, bool recursive)
        {
            try
            {
                await DeleteFolderAsync(path, recursive);
            }
            catch(DirectoryNotFoundException)
            {
                // Ignore
            }
        }

        public Task DeleteFolderAsync(string path, bool recursive)
        {
            if (!string.IsNullOrEmpty(path))
            {
                PathHelper.Delete(path, recursive);
            }

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ListFolderAsync(string folderName, bool includePath = true, bool recursive = false)
        {
            await CreateFolderAsync(folderName);

            foreach (var file in PathHelper.ListFolder(folderName, includePath, recursive))
            {
                yield return file;
            }
        }

        private async Task SaveToDiskAsync(Stream sourceStream, string targetFilePathAndName)
        {
            await using(var fs = new FileStream(targetFilePathAndName, FileMode.Create, FileAccess.Write))
            {
                await sourceStream.WriteToAsync(fs);
            }
        }

        private Task<bool> FileExistsAsync(string pathAndFileName) => Task.FromResult(FileHelper.Exists(pathAndFileName));
    }
}
