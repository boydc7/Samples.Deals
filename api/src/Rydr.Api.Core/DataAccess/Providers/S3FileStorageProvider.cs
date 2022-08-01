using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Helpers;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.DataAccess.Providers
{
    public static class AmazonS3ErrorCodes
    {
        public const string NoSuchBucket = "NoSuchBucket";
        public const string NoSuchKey = "NoSuchKey";
        public const string NotFound = "NotFound";
    }

    public class S3FileStorageProvider : IFileStorageProvider, IDisposable
    {
        private readonly IAmazonS3 _s3Client;

        private static readonly long _minByteSizeForMultiPart = RydrEnvironment.GetAppSetting("AWS.S3.MinMbForMultiPartDownload", "4096").ToInt64(4096) * 1024 * 1024;
        private static readonly long _partSizeMultiPart = RydrEnvironment.GetAppSetting("AWS.S3.PartSizeMb", "1024").ToInt64(1024) * 1024 * 1024;
        private static readonly int _multiPartMaxDop = RydrEnvironment.GetAppSetting("AWS.S3.MultiPartMaxDop", "5").ToInt(5);
        private static readonly bool _multiPartDownloadDisabled = RydrEnvironment.GetAppSetting("AWS.S3.MultiPartDownloadDisabled", "false").ToBoolean();

        private static readonly ILog _log = LogManager.GetLogger("S3FileStorageProvider");

        public S3FileStorageProvider(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        public FileStorageProviderType ProviderType => FileStorageProviderType.S3;

        public async Task DownloadAsync(FileMetaData thisProviderMeta, FileMetaData downloadToMeta)
        {
            var bpk = new S3BucketPrefixKey(thisProviderMeta.FullName);

            var s3ObjectMeta = _multiPartDownloadDisabled
                                   ? null
                                   : await GetMetaDataAsync(bpk);

            if (_multiPartDownloadDisabled || (s3ObjectMeta?.ContentLength ?? 0) < _minByteSizeForMultiPart)
            {
                await DownloadSinglePartAsync(bpk, downloadToMeta);
            }
            else
            {
                await DownloadMultiPartAsync(bpk, downloadToMeta, _partSizeMultiPart, s3ObjectMeta);
            }
        }

        public async Task CopyToAsync(FileMetaData thisProviderMeta, IFileStorageProvider targetProvider, FileMetaData targetMeta, FileStorageOptions options = null)
        {
            // Store method with multiple FileMeta objects assumes the source is on the filesystem (or handles that case)
            // so with an S3 source provider, need to copy the given source to the local filesystem first, use that
            // as the source for the given target provider, then cleanup naturally
            var localFile = Path.Combine(Path.GetTempPath(), targetMeta.FileNameAndExtension);
            var localFileMeta = new FileMetaData(localFile);

            try
            {
                await DownloadAsync(thisProviderMeta, localFileMeta);

                var localFileSystemMeta = new FileMetaData(localFile);

                await targetProvider.StoreAsync(localFileSystemMeta, targetMeta, options);
            }
            finally
            {
                FileHelper.Delete(localFile);
            }
        }

        public async Task GetStreamIntoAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var request = new GetObjectRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key
                          };

            var getResponse = await _s3Client.GetObjectAsync(request);

            fileMetaData.Stream = getResponse.ResponseStream;
        }

        public Task StoreAsync(FileMetaData sourceFileSystemMeta, FileMetaData targetMeta, FileStorageOptions options = null)
            => PostToS3Async(targetMeta, options, sourceFileSystemMeta.FullName);

        public Task StoreAsync(FileMetaData fileMetaData, FileStorageOptions options = null)
            => PostToS3Async(fileMetaData, options);

        public async Task CreateFolderAsync(string folderName)
        {
            var bpk = new S3BucketPrefixKey(folderName);

            var request = new PutBucketRequest
                          {
                              BucketName = bpk.BucketName,
                              UseClientRegion = true
                          };

            await _s3Client.PutBucketAsync(request);
        }

        public async Task SetTagsAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData);

            await _s3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
                                                  {
                                                      BucketName = bpk.BucketName,
                                                      Key = bpk.Key,
                                                      Tagging = new Tagging
                                                                {
                                                                    TagSet = fileMetaData.Tags
                                                                                         .Select(t => new Tag
                                                                                                      {
                                                                                                          Key = t.Key,
                                                                                                          Value = t.Value
                                                                                                      })
                                                                                         .AsList()
                                                                }
                                                  });
        }

        public async Task<Dictionary<string, string>> GetTagsAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData);

            var getTagResponse = await _s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                                                                       {
                                                                           BucketName = bpk.BucketName,
                                                                           Key = bpk.Key,
                                                                       });

            return getTagResponse?.Tagging.ToDictionarySafe(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
        }

        public async Task TryDeleteFolderAsync(string folderName, bool recursive)
        {
            try
            {
                await DeleteFolderAsync(folderName, recursive);
            }
            catch(AmazonS3Exception s3X)
            {
                if (!s3X.ErrorCode.EqualsOrdinalCi(AmazonS3ErrorCodes.NoSuchBucket) &&
                    !s3X.ErrorCode.EqualsOrdinalCi(AmazonS3ErrorCodes.NoSuchKey))
                {
                    throw;
                }
            }
        }

        public async Task DeleteFolderAsync(string folderName, bool recursive)
        {
            var bpk = new S3BucketPrefixKey(folderName, true);

            if (recursive)
            {
                while (true)
                {
                    var objects = await ListFolderAsync(bpk);

                    if (!objects.Any())
                    {
                        break;
                    }

                    var keys = objects.Select(o => new KeyVersion
                                                   {
                                                       Key = o.Key
                                                   })
                                      .ToList();

                    var deleteObjectsRequest = new DeleteObjectsRequest
                                               {
                                                   BucketName = bpk.BucketName,
                                                   Quiet = true,
                                                   Objects = keys
                                               };

                    await _s3Client.DeleteObjectsAsync(deleteObjectsRequest);
                }
            }
            else if (!bpk.IsBucketObject)
            {
                var deleteObjectRequest = new DeleteObjectRequest
                                          {
                                              BucketName = bpk.BucketName,
                                              Key = bpk.Key
                                          };

                await _s3Client.DeleteObjectAsync(deleteObjectRequest);
            }

            if (bpk.IsBucketObject)
            {
                var request = new DeleteBucketRequest
                              {
                                  BucketName = bpk.BucketName,
                                  UseClientRegion = true
                              };

                await _s3Client.DeleteBucketAsync(request);
            }
        }

        public async Task MoveAsync(string source, string target, FileStorageOptions options = null)
        {
            var sourceBpk = new S3BucketPrefixKey(source);
            var targetBpk = new S3BucketPrefixKey(target);

            if (sourceBpk.Equals(targetBpk))
            {
                return;
            }

            var stored = false;

            try
            {
                var copyRequest = new CopyObjectRequest
                                  {
                                      SourceBucket = sourceBpk.BucketName,
                                      SourceKey = sourceBpk.Key,
                                      DestinationBucket = targetBpk.BucketName,
                                      DestinationKey = targetBpk.Key,
                                      ServerSideEncryptionMethod = options?.Encrypt ?? false
                                                                       ? ServerSideEncryptionMethod.AES256
                                                                       : ServerSideEncryptionMethod.None
                                  };

                await _s3Client.CopyObjectAsync(copyRequest);

                stored = true;

                await DeleteAsync(sourceBpk);
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

        public async IAsyncEnumerable<string> ListFolderAsync(string folderName, bool includePath = true, bool recursive = false)
        {
            await foreach (var bpk in ListFolderInternalAsync(folderName, recursive))
            {
                yield return includePath
                                 ? bpk.FullName
                                 : bpk.FileName;
            }
        }

        private async IAsyncEnumerable<S3BucketPrefixKey> ListFolderInternalAsync(string folderName, bool recursive)
        {
            string nextMarker = null;

            var bpk = new S3BucketPrefixKey(folderName, true);

            do
            {
                var listResponse = await ListFolderResponseAsync(bpk, nextMarker);

                if (listResponse?.S3Objects == null)
                {
                    break;
                }

                var files = listResponse.S3Objects
                                        .Select(o => new S3BucketPrefixKey(bpk.BucketName, o))
                                        .Where(b => !string.IsNullOrEmpty(b.FileName))
                                        .Where(b => recursive
                                                        ? b.Prefix.StartsWithOrdinalCi(bpk.Prefix)
                                                        : b.Prefix.EqualsOrdinalCi(bpk.Prefix));

                foreach (var file in files)
                {
                    yield return file;
                }

                if (listResponse.IsTruncated)
                {
                    nextMarker = listResponse.NextMarker;
                }
                else
                {
                    yield break;
                }
            } while (true);
        }

        public async Task DeleteAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            await DeleteAsync(bpk);
        }

        public async Task<long> GetSizeInBytesAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var omd = await GetMetaDataAsync(bpk);

            return omd.ContentLength;
        }

        public async Task<bool> ExistsAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            try
            {
                await GetMetaDataAsync(bpk);

                return true;
            }
            catch(AmazonS3Exception x) when(x.ErrorCode.EqualsOrdinalCi(AmazonS3ErrorCodes.NoSuchBucket) ||
                                            x.ErrorCode.EqualsOrdinalCi(AmazonS3ErrorCodes.NoSuchKey) ||
                                            x.ErrorCode.EqualsOrdinalCi(AmazonS3ErrorCodes.NotFound))

            {
                return false;
            }
        }

        public async Task<byte[]> GetOrDefaultAsync(FileMetaData fileMetaData, byte[] defaultValue = null)
        {
            var exists = await ExistsAsync(fileMetaData);

            if (!exists)
            {
                return defaultValue;
            }

            var result = await GetAsync(fileMetaData);

            return result;
        }

        public async Task<byte[]> GetAsync(FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var request = new GetObjectRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key
                          };

            try
            {
                using(var iResponse = await _s3Client.GetObjectAsync(request))
                {
                    if (iResponse?.ResponseStream == null)
                    {
                        return null;
                    }

                    await using(var memoryStream = new MemoryStream())
                    {
                        await iResponse.ResponseStream.CopyToAsync(memoryStream);

                        return memoryStream.ToArray();
                    }
                }
            }
            catch(AmazonS3Exception)
            {
                return null;
            }
        }

        public string GetDownloadUrl(int expiresInMinutes, string folder, string filename, bool isPreview = false)
        {
            var fileMetaData = new FileMetaData(folder, filename);

            var response = GetDownloadUrl(expiresInMinutes, fileMetaData, isPreview);

            return response;
        }

        public string GetDownloadUrl(int expiresInMinutes, FileMetaData fileMetaData, bool isPreview = false)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var request = new GetPreSignedUrlRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key,
                              Verb = HttpVerb.GET,
                              Expires = DateTimeHelper.UtcNow.AddMinutes(expiresInMinutes)
                          };

            if (isPreview)
            {
                request.ResponseHeaderOverrides.ContentDisposition = "inline";
            }
            else if (fileMetaData.DisplayName.HasValue())
            {
                request.ResponseHeaderOverrides.ContentDisposition = $"attachment;filename={fileMetaData.DisplayName}";
            }

            return _s3Client.GetPreSignedURL(request);
        }

        public string GetUploadUrl(int expiresInMinutes, FileMetaData fileMetaData)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var request = new GetPreSignedUrlRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key,
                              Expires = DateTimeHelper.UtcNow.AddMinutes(expiresInMinutes),
                              Verb = HttpVerb.PUT
                          };

            return _s3Client.GetPreSignedURL(request);
        }

        private async Task DownloadSinglePartAsync(S3BucketPrefixKey bpk, FileMetaData downloadToMeta)
        {
            var request = new GetObjectRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key
                          };

            PathHelper.Create(downloadToMeta.FolderName);
            FileHelper.Delete(downloadToMeta.FullName);

            using(var iResponse = await _s3Client.GetObjectAsync(request))
            {
                await iResponse.WriteResponseStreamToFileAsync(downloadToMeta.FullName, false, CancellationToken.None);
            }
        }

        public async Task<int> DownloadMultiPartAsync(S3BucketPrefixKey bpk, FileMetaData downloadToMeta,
                                                      long partSizeBytes, S3ObjectMetaData s3ObjectMeta = null)
        {
            PathHelper.Create(downloadToMeta.FolderName);
            FileHelper.Delete(downloadToMeta.FullName);

            if (s3ObjectMeta == null)
            {
                s3ObjectMeta = await GetMetaDataAsync(bpk);
            }

            // Multipart download
            var parts = GetParts(partSizeBytes, s3ObjectMeta.ContentLength, downloadToMeta.FullName).ToList();

            var parallelOptions = new ParallelOptions
                                  {
                                      MaxDegreeOfParallelism = _multiPartMaxDop,
                                      TaskScheduler = TaskScheduler.Default
                                  };

            try
            {
                Exception lastError = null;

                Parallel.ForEach(parts, parallelOptions,
                                 async (part, state) =>
                                 {
                                     try
                                     {
                                         var request = new GetObjectRequest
                                                       {
                                                           BucketName = bpk.BucketName,
                                                           Key = bpk.Key,
                                                           ByteRange = part.ByteRange
                                                       };

                                         FileHelper.Delete(part.PartFileName);

                                         using(var response = await _s3Client.GetObjectAsync(request))
                                         {
                                             await response.WriteResponseStreamToFileAsync(part.PartFileName, false, CancellationToken.None);
                                         }
                                     }
                                     catch(Exception ex)
                                     {
                                         lastError = ex;
                                         _log.Exception(ex, "GetToFile MultiPart download failed");
                                         state.Stop();
                                     }
                                 });

                if (lastError != null)
                {
                    throw lastError;
                }

                // Successfully downloaded all the parts, put them back together
                FileHelper.Delete(downloadToMeta.FullName);

                await using(var output = File.OpenWrite(downloadToMeta.FullName))
                {
                    foreach (var part in parts)
                    {
                        await using(var input = File.OpenRead(part.PartFileName))
                        {
                            await input.CopyToAsync(output);
                        }

                        FileHelper.Delete(part.PartFileName);
                    }
                }
            }
            catch(Exception)
            {
                FileHelper.Delete(downloadToMeta.FullName);

                throw;
            }
            finally
            { // Cleanup all the parts if needed
                parts.ForEach(p => FileHelper.Delete(p.PartFileName));
            }

            return parts.Count;
        }

        private async Task PostToS3Async(FileMetaData targetMetaData, FileStorageOptions options, string sourceFilePathAndName = null)
        {
            var attemptedBucketCreate = false;

            if (options == null)
            {
                options = new FileStorageOptions
                          {
                              Encrypt = true,
                              StorageClass = FileStorageClass.Intelligent
                          };
            }

            do
            {
                try
                {
                    if (sourceFilePathAndName.HasValue())
                    {
                        await PostMultiToS3Async(targetMetaData, options, sourceFilePathAndName);
                    }
                    else if (targetMetaData.Stream != null)
                    {
                        await PostToS3WithStreamAsync(targetMetaData, targetMetaData.Stream, options);
                    }
                    else
                    {
                        await using(var byteStream = new MemoryStream(targetMetaData.Bytes))
                        {
                            await PostToS3WithStreamAsync(targetMetaData, byteStream, options);
                        }
                    }

                    break;
                }
                catch(AmazonS3Exception s3X)
                {
                    if (!attemptedBucketCreate && s3X.ErrorCode == AmazonS3ErrorCodes.NoSuchBucket)
                    {
                        await CreateFolderAsync(targetMetaData.FolderName);
                        attemptedBucketCreate = true;

                        continue;
                    }

                    throw;
                }
            } while (true);
        }

        private void SetRequestMetadata(FileMetaData fileMetaData, MetadataCollection metadata)
        {
            if (fileMetaData.DisplayName.HasValue())
            {
                metadata.Add("file-display-name", fileMetaData.DisplayName);
            }

            if (fileMetaData.User != null)
            {
                foreach (var (key, value) in fileMetaData.User)
                {
                    metadata.Add(key, value);
                }
            }
        }

        private async Task PostMultiToS3Async(FileMetaData fileMetaData, FileStorageOptions options, string sourceFilePathAndName)
        {
            const long minFileSizeBeforeMultipart = 2 * 1000 * 1000 * 1024; // 2gb in bytes before multipart
            const long multiPartSize = 500 * 1000 * 1024; // 500mb part sizes

            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var request = new TransferUtilityUploadRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key,
                              FilePath = sourceFilePathAndName,
                              PartSize = multiPartSize,
                              ServerSideEncryptionMethod = options.Encrypt
                                                               ? ServerSideEncryptionMethod.AES256
                                                               : ServerSideEncryptionMethod.None,
                              StorageClass = options.StorageClass.ToS3StorageClass(),
                              ContentType = options.ContentType,
                              TagSet = GetTagSet(fileMetaData),
                              Headers =
                              {
                                  ContentEncoding = options.ContentEncoding
                              }
                          };

            SetRequestMetadata(fileMetaData, request.Metadata);

            var xferConfig = new TransferUtilityConfig
                             {
                                 ConcurrentServiceRequests = _multiPartMaxDop,
                                 MinSizeBeforePartUpload = minFileSizeBeforeMultipart
                             };

            using(var xferUtil = new TransferUtility(_s3Client, xferConfig))
            {
                await xferUtil.UploadAsync(request);
            }
        }

        private async Task PostToS3WithStreamAsync(FileMetaData fileMetaData, Stream streamToUse, FileStorageOptions options)
        {
            var bpk = new S3BucketPrefixKey(fileMetaData.FullName);

            var request = new PutObjectRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key,
                              InputStream = streamToUse,
                              ServerSideEncryptionMethod = options.Encrypt
                                                               ? ServerSideEncryptionMethod.AES256
                                                               : ServerSideEncryptionMethod.None,
                              StorageClass = options.StorageClass.ToS3StorageClass(),
                              ContentType = options.ContentType,
                              TagSet = GetTagSet(fileMetaData),
                              Headers =
                              {
                                  ContentEncoding = options.ContentEncoding
                              }
                          };

            SetRequestMetadata(fileMetaData, request.Metadata);

            if (options.ContentDisposition == FileStorageContentDisposition.Attachment)
            {
                request.Headers.ContentDisposition = "attachment";
            }

            await _s3Client.PutObjectAsync(request);
        }

        private async Task<List<S3Object>> ListFolderAsync(S3BucketPrefixKey bpk)
        {
            var listResponse = await ListFolderResponseAsync(bpk);

            return listResponse?.S3Objects ?? new List<S3Object>();
        }

        private async Task<ListObjectsResponse> ListFolderResponseAsync(S3BucketPrefixKey bpk, string nextMarker = null)
        {
            try
            {
                var listRequest = new ListObjectsRequest
                                  {
                                      BucketName = bpk.BucketName
                                  };

                if (nextMarker.HasValue())
                {
                    listRequest.Marker = nextMarker;
                }

                if (bpk.HasPrefix)
                {
                    listRequest.Prefix = bpk.Prefix;
                }

                var listResponse = await _s3Client.ListObjectsAsync(listRequest);

                return listResponse;
            }
            catch(XmlException) { }
            catch(AmazonS3Exception s3X)
            {
                if (s3X.ErrorCode != AmazonS3ErrorCodes.NoSuchBucket &&
                    s3X.ErrorCode != AmazonS3ErrorCodes.NoSuchKey)
                {
                    throw;
                }
            }

            return null;
        }

        private Task DeleteAsync(S3BucketPrefixKey file)
            => _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                                           {
                                               BucketName = file.BucketName,
                                               Key = file.Key
                                           });

        private async Task<S3ObjectMetaData> GetMetaDataAsync(S3BucketPrefixKey bpk)
        {
            var request = new GetObjectMetadataRequest
                          {
                              BucketName = bpk.BucketName,
                              Key = bpk.Key
                          };

            var response = await _s3Client.GetObjectMetadataAsync(request);

            var meta = new S3ObjectMetaData
                       {
                           Md5 = response.Headers.ContentMD5,
                           ContentLength = response.ContentLength.Gz(response.Headers.ContentLength)
                       };

            return meta;
        }

        private IEnumerable<FilePartMeta> GetParts(long partSize, long contentSize, string baseFileName)
        {
            var partIndex = 0;

            while (true)
            {
                var rangeStart = partIndex * partSize;
                var rangeEnd = rangeStart + partSize - 1;

                if (rangeEnd > contentSize)
                {
                    rangeEnd = contentSize;
                }

                var partFileName = string.Concat(baseFileName, ".", partIndex);

                yield return new FilePartMeta
                             {
                                 ByteRange = new ByteRange(rangeStart, rangeEnd),
                                 PartIndex = partIndex,
                                 PartFileName = partFileName
                             };

                if (rangeEnd >= contentSize)
                {
                    break;
                }

                partIndex++;
            }
        }

        private static List<Tag> ToTagSet(Dictionary<string, string> dict)
        {
            var tagSet = new List<Tag>(dict.Count);

            foreach (var entry in dict)
            {
                tagSet.Add(new Tag
                           {
                               Key = entry.Key,
                               Value = entry.Value
                           });
            }

            return tagSet;
        }

        private static List<Tag> GetTagSet(FileMetaData meta)
            => meta.Tags.Count > 0
                   ? ToTagSet(meta.Tags)
                   : null;

        private class FilePartMeta
        {
            public ByteRange ByteRange { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public int PartIndex { get; set; }
            public string PartFileName { get; set; }
        }

        public void Dispose()
        {
            _s3Client.TryDispose();
        }
    }
}
