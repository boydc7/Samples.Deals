using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Services.Services;

public class FileServiceInternal : BaseInternalOnlyApiService
{
    private static readonly string _publisherAccountProfileImageRoot = RydrEnvironment.GetAppSetting("PublisherAccount.ProfileImageRoot", "rydr-cdn-dev");

    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IFileStorageService _fileStorageService;
    private readonly IPublisherAccountService _publisherAccountService;

    public FileServiceInternal(IFileStorageProvider fileStorageProvider,
                               IFileStorageService fileStorageService,
                               IPublisherAccountService publisherAccountService)
    {
        _fileStorageProvider = fileStorageProvider;
        _fileStorageService = fileStorageService;
        _publisherAccountService = publisherAccountService;
    }

    public async Task Post(ConvertFile request)
    {
        var existingFile = await _dynamoDb.GetItemAsync<DynFile>(request.FileId.ToItemDynamoId());

        var convertArgs = request.ToConvertArguments();

        if (!existingFile.StatusCanConvert())
        {
            return;
        }

        await _fileStorageService.ConvertAndStoreAsync(existingFile, convertArgs);
    }

    public async Task Post(ProcessPublisherAccountProfilePic request)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (publisherAccount == null || publisherAccount.IsDeleted())
        {
            return;
        }

        var profileFileMeta = new FileMetaData(_publisherAccountProfileImageRoot, Path.Combine(request.ProfilePicKey, "profile.jpg"));

        if (publisherAccount.ProfilePicture.IsNullOrEmpty() ||
            (publisherAccount.ProfilePicture.StartsWithOrdinalCi("https://cdn.getrydr.com") &&
             await _fileStorageProvider.ExistsAsync(profileFileMeta)))
        {
            _log.InfoFormat("Skipping profile picture processing for PublisherAccount [{0}], it is missing or already a CDN link/exists - [{1}]", publisherAccount.DisplayName(), publisherAccount.ProfilePicture);

            return;
        }

        profileFileMeta.Bytes = publisherAccount.ProfilePicture.GetImage().ToBytes();

        if (profileFileMeta.Bytes.IsNullOrEmpty())
        {
            _log.InfoFormat("Skipping profile picture processing for PublisherAccount [{0}], could not get image from profile url [{1}]", publisherAccount.DisplayName(), publisherAccount.ProfilePicture);

            return;
        }

        profileFileMeta.Tags[FileStorageTag.Lifecycle.ToString()] = FileStorageTags.LifecycleKeep;
        profileFileMeta.Tags[FileStorageTag.Privacy.ToString()] = FileStorageTags.PrivacyPublic;

        await _fileStorageProvider.StoreAsync(profileFileMeta);

        await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount, p => p.ProfilePicture = $"https://cdn.getrydr.com/{request.ProfilePicKey.TrimStart('/').TrimEnd('/')}/profile.jpg");
    }

    public async Task Post(ProcessRelatedMediaFiles request)
    {
        await foreach (var dynPublisherMedia in _dynamoDb.QueryItemsAsync<DynPublisherMedia>(request.PublisherMediaIds
                                                                                                    .Select(mid => new DynamoId(request.PublisherAccountId,
                                                                                                                                mid.ToEdgeId())))
                                                         .Where(p => p != null && !p.IsDeleted()))
        {
            // Do not have to do anything with getting/moving files if this is a RYDR media file...
            if (dynPublisherMedia.PublisherType == PublisherType.Rydr && dynPublisherMedia.MediaFileId > 0)
            {
                await dynPublisherMedia.PopulateFileMediaUrlsAsync();
            }
            else
            {
                var mediaIsVideo = false;
                FileMetaData rydrMediaFileMeta = null;
                FileMetaData rydrThumbnailFileMeta = null;

                foreach (var fileMetaData in dynPublisherMedia.GetRawMediaAnalysisPathAndFileMetas(includeVideo: true,
                                                                                                   isPermanentMedia: request.StoreAsPermanentMedia,
                                                                                                   skipBytes: dynPublisherMedia.IsRydrHosted))
                {
                    var fileType = fileMetaData.FileExtension.ToFileType();

                    fileMetaData.TagFromMedia(dynPublisherMedia, dynPublisherMedia.IsPermanentMedia || request.StoreAsPermanentMedia);

                    if (fileType == FileType.Image)
                    { // A piece of media that we need to keep. If the media is a video, the image is the thumbnail. Otherwise, it's the media.
                        if (mediaIsVideo)
                        {
                            rydrThumbnailFileMeta = fileMetaData;
                        }
                        else if (rydrMediaFileMeta == null)
                        {
                            rydrMediaFileMeta = fileMetaData;
                        }
                    }
                    else if (fileType == FileType.Video)
                    { // If this is a video, we still have to populate the urls. If a mediaFile already exists, it becomes the thumbnail
                        if (rydrMediaFileMeta != null && rydrMediaFileMeta.FileExtension.ToFileType() == FileType.Image)
                        {
                            rydrThumbnailFileMeta = rydrMediaFileMeta;
                        }

                        rydrMediaFileMeta = fileMetaData;

                        // The media file to store/update is the video. If an image is already stored there, it's the thumbnail
                        mediaIsVideo = true;
                    }
                    else
                    { // All other types we do nothing with
                        continue;
                    }

                    if (await _fileStorageProvider.ExistsAsync(fileMetaData))
                    { // File exists, tag only, no need to upload
                        if (!fileMetaData.Tags.IsNullOrEmptyRydr())
                        {
                            var existingTags = await _fileStorageProvider.GetTagsAsync(fileMetaData);

                            if (existingTags.IsNullOrEmptyRydr() || existingTags.Count != fileMetaData.Tags.Count ||
                                !existingTags.Match(fileMetaData.Tags, StringComparer.OrdinalIgnoreCase))
                            {
                                await _fileStorageProvider.SetTagsAsync(fileMetaData);
                            }
                        }
                    }
                    else if (!fileMetaData.Bytes.IsNullOrEmpty())
                    { // Upload and tag
                        await _fileStorageProvider.StoreAsync(fileMetaData);
                    }
                    else if (fileType == FileType.Video)
                    {
                        // Download locally, then upload to file store
                        var localFileMeta = new FileMetaData(Path.Combine(Path.GetTempPath(), request.PublisherAccountId.ToStringInvariant()),
                                                             fileMetaData.FileNameAndExtension);

                        try
                        {
                            await dynPublisherMedia.MediaUrl.DownloadFromUrl(localFileMeta);

                            await _fileStorageProvider.StoreAsync(localFileMeta, fileMetaData);
                        }
                        catch(Exception x)
                        { // Ignore errors on attempting download/storage here, just log
                            _log.Exception(x, wasHandled: true);
                        }
                        finally
                        {
                            FileHelper.Delete(localFileMeta.FullName);
                        }
                    }
                }

                if (rydrMediaFileMeta == null)
                {
                    continue;
                }

                dynPublisherMedia.PopulatePublisherMediaUrls(rydrMediaFileMeta, rydrThumbnailFileMeta);
            }

            dynPublisherMedia.IsRydrHosted = true;
            dynPublisherMedia.IsCompletionMedia = dynPublisherMedia.IsCompletionMedia || request.IsCompletionMedia;

            await _dynamoDb.PutItemAsync(dynPublisherMedia);

            if (!dynPublisherMedia.MediaUrl.Contains("dl.getrydr.com", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Cleanup any temp rydr-hosted dl files
            var tempKey = dynPublisherMedia.MediaUrl.RightPart("temp/");

            if (tempKey.HasValue())
            {
                await Try.ExecAsync(() => _fileStorageProvider.DeleteAsync(new FileMetaData("dl.getrydr.com", tempKey)));
            }
        }
    }
}
