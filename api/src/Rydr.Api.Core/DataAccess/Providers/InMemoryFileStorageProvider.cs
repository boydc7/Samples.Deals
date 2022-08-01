using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Services.Internal;
using ServiceStack;

namespace Rydr.Api.Core.DataAccess.Providers
{
    internal class MemoryFileMeta
    {
        public byte[] Bytes;
        public FileMetaData MetaData;
    }

    public class InMemoryFileStorageProvider : IFileStorageProvider
    {
        private readonly Dictionary<string, MemoryFileMeta> _fileMap;

        public InMemoryFileStorageProvider()
        {
            _fileMap = new Dictionary<string, MemoryFileMeta>();
        }

        public FileStorageProviderType ProviderType => FileStorageProviderType.InMemory;

        public async Task DownloadAsync(FileMetaData thisProviderMeta, FileMetaData downloadToMeta)
        {
            PathHelper.Create(downloadToMeta.FolderName);

            var bytes = await GetAsync(thisProviderMeta);

            await FileHelper.WriteAllBytesAsync(downloadToMeta.FullName, bytes);
        }

        public async Task CopyToAsync(FileMetaData thisProviderMeta, IFileStorageProvider targetProvider, FileMetaData targetMeta, FileStorageOptions options = null)
        {
            // Store method with a single provider assumes the bytes are in the single object and the meta-data
            // within the other properties refer to the target location (i.e. where to put the bytes)
            targetMeta.Bytes = await GetAsync(thisProviderMeta);
            await targetProvider.StoreAsync(targetMeta, options);
        }

        public async Task StoreAsync(FileMetaData sourceFileSystemMeta, FileMetaData targetMeta, FileStorageOptions options = null)
        {
            await using(var sourceStream = new FileStream(sourceFileSystemMeta.FullName, FileMode.Open, FileAccess.Read))
            {
                SaveToMap(sourceStream.ToBytes(), targetMeta);
            }
        }

        public Task StoreAsync(FileMetaData fileMetaData, FileStorageOptions options = null)
        {
            SaveToMap(fileMetaData.Stream == null
                          ? fileMetaData.Bytes
                          : fileMetaData.Stream.ToBytes(), fileMetaData);

            return Task.CompletedTask;
        }

        public Task<Dictionary<string, string>> GetTagsAsync(FileMetaData fileMetaData)
            => !_fileMap.ContainsKey(fileMetaData.FullName)
                   ? Task.FromResult<Dictionary<string, string>>(null)
                   : Task.FromResult(_fileMap[fileMetaData.FullName].MetaData.Tags);

        public Task SetTagsAsync(FileMetaData fileMetaData)
        {
            if (_fileMap.ContainsKey(fileMetaData.FullName))
            {
                _fileMap[fileMetaData.FullName].MetaData.Tags.Clear();

                fileMetaData.Tags.Each(t => _fileMap[fileMetaData.FullName].MetaData.Tags.Add(t.Key, t.Value));
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(FileMetaData fileMetaData)
        {
            if (_fileMap.ContainsKey(fileMetaData.FullName))
            {
                _fileMap.Remove(fileMetaData.FullName);
            }

            return Task.CompletedTask;
        }

        public Task GetStreamIntoAsync(FileMetaData fileMetaData)
        {
            fileMetaData.Stream = _fileMap[fileMetaData.FullName].Bytes.InMemoryStream();

            return Task.CompletedTask;
        }

        public Task<long> GetSizeInBytesAsync(FileMetaData fileMetaData)
            => Task.FromResult(_fileMap[fileMetaData.FullName].Bytes.LongLength);

        public string GetDownloadUrl(int expiresInMinutes, string folder, string filename, bool isPreview = false)
        {
            var fileMetaData = new FileMetaData(folder, filename);

            return GetDownloadUrl(expiresInMinutes, fileMetaData, isPreview);
        }

        public string GetUploadUrl(int expiresInMinutes, FileMetaData fileMetaData) => fileMetaData.FullName;

        public string GetDownloadUrl(int expiresInMinutes, FileMetaData fileMetaData, bool isPreview = false) => fileMetaData.FullName;

        public Task<bool> ExistsAsync(FileMetaData fileMetaData)
            => Task.FromResult(_fileMap.ContainsKey(fileMetaData.FullName));

        public async Task MoveAsync(string source, string target, FileStorageOptions options = null)
        {
            if (!_fileMap.ContainsKey(source))
            {
                throw new FileNotFoundException();
            }

            var stored = false;

            try
            {
                await StoreAsync(new FileMetaData(target));
                stored = true;
                await DeleteAsync(new FileMetaData(source));
            }
            catch(Exception)
            {
                if (stored)
                {
                    await DeleteAsync(new FileMetaData(target));
                }

                throw;
            }
        }

        public Task TryDeleteFolderAsync(string folderName, bool recursive)
            => DeleteFolderAsync(folderName, recursive);

        public Task DeleteFolderAsync(string folderName, bool recursive)
        {
            var keysToDelete = _fileMap.Where(i => i.Value.MetaData.FolderName.StartsWith(folderName)).Select(kvp => kvp.Key).ToList();

            foreach (var key in keysToDelete)
            {
                _fileMap.Remove(key);
            }

            return Task.CompletedTask;
        }

#pragma warning disable 1998
        public async IAsyncEnumerable<string> ListFolderAsync(string path, bool includePath = true, bool recursive = false)
#pragma warning restore 1998
        {
            var files = _fileMap.Where(f => recursive
                                                ? f.Value.MetaData.FolderName.StartsWith(path, StringComparison.InvariantCultureIgnoreCase)
                                                : f.Value.MetaData.FolderName.EqualsOrdinalCi(path))
                                .Select(f => includePath
                                                 ? f.Value.MetaData.FullName
                                                 : f.Value.MetaData.FileNameAndExtension);

            foreach (var file in files)
            {
                yield return file;
            }
        }

        // No need to do anything with creating folders here...in memory is mapped to a dict that needs no folder structure
        public Task CreateFolderAsync(string folderName)
            => Task.CompletedTask;

        public async Task<byte[]> GetOrDefaultAsync(FileMetaData fileMetaData, byte[] defaultValue = null)
        {
            if (await ExistsAsync(fileMetaData))
            {
                return await GetAsync(fileMetaData);
            }

            return defaultValue;
        }

        public Task<byte[]> GetAsync(FileMetaData fileMetaData)
            => Task.FromResult(_fileMap.ContainsKey(fileMetaData.FullName)
                                   ? _fileMap[fileMetaData.FullName].Bytes
                                   : null);

        private void SaveToMap(byte[] bytes, FileMetaData targetMeta)
        {
            var s = new MemoryFileMeta
                    {
                        Bytes = bytes,
                        MetaData = targetMeta.Clone()
                    };

            _fileMap[targetMeta.FullName] = s;
        }
    }
}
