using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Enums
{
    public static class RydrFileStoragePaths
    {
        public const string AnalysisContentPrefix = "content_";
        public const string AnalysisImagePrefix = "image_";
        public const string AnalysisThumbnailPrefix = "thumb_";
        public const string AnalysisVideoPrefix = "video_";

        public const string AnalysisRawSegment = "raw";

        private static readonly List<string> _extensionValues = FileTransforms.FileExtensionFileTypeMap
                                                                              .Where(m => m.Value == FileType.Image ||
                                                                                          m.Value == FileType.Video)
                                                                              .Select(x => string.Concat(".", x.Key, "?"))
                                                                              .Concat(FileTransforms.FileExtensionFileTypeMap
                                                                                                    .Where(m => m.Value == FileType.Image ||
                                                                                                                m.Value == FileType.Video)
                                                                                                    .Select(x => string.Concat(".", x.Key)))
                                                                              .AsList();

        public static readonly string AnalysisRootPath = RydrEnvironment.GetAppSetting("PublisherAnalysis.RootPath");
        public static readonly string FilesRoot = RydrEnvironment.GetAppSetting("Files.RootPath", "rydr-files-dev");
        public static readonly string LocalFileStorageRoot = RydrEnvironment.GetAppSetting("FileStorage.Local.RootPath", "~/RydrLocalData");
        public static readonly string VideosRoot = RydrEnvironment.GetAppSetting("Files.Videos.RootPath", "rydr-files-videos-dev");
        public static readonly string DlqDropPath = RydrEnvironment.GetAppSetting("Messaging.DlqDropPath", "rydr-workflows/dev/dlq");

        public static string GetPublisherAccountPath(string rootPath, long publisherAccountId)
            => Path.Combine(rootPath, publisherAccountId.ToStringInvariant());

        public static IEnumerable<FileMetaData> GetRawMediaAnalysisPathAndFileMetas<T>(this T source, bool skipBytes = false,
                                                                                       bool includeVideo = false, bool isPermanentMedia = false)
            where T : IGenerateMediaUrls
            => GetRawMediaAnalysisPathAndFileMetas(source.RydrMediaId, source.Caption, source.MediaUrl, source.ThumbnailUrl,
                                                   source.PublisherAccountId, source.ContentType,
                                                   source.IsPermanentMedia || isPermanentMedia, skipBytes, includeVideo);

        private static IEnumerable<FileMetaData> GetRawMediaAnalysisPathAndFileMetas(long mediaId, string caption, string mediaUrl, string thumbnailUrl,
                                                                                     long publisherAccountId, PublisherContentType contentType,
                                                                                     bool isPermanentMedia, bool skipBytes, bool includeVideo)
        {
            var rawPath = Path.Combine(GetPublisherAccountPath(AnalysisRootPath, publisherAccountId), AnalysisRawSegment);

            if (caption.HasValue())
            {
                var contentRawMeta = new FileMetaData(rawPath, string.Concat(AnalysisContentPrefix, mediaId, ".txt"))
                                     {
                                         Bytes = skipBytes
                                                     ? null
                                                     : Encoding.UTF8.GetBytes(caption)
                                     };

                if (skipBytes || (contentRawMeta.Bytes != null && contentRawMeta.Bytes.Length > 0))
                {
                    contentRawMeta.TagMediaValues(contentType, isPermanentMedia);

                    yield return contentRawMeta;
                }
            }

            if (mediaUrl.HasValue())
            {
                var mediaExtension = _extensionValues.FirstOrDefault(x => mediaUrl.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 15)?.TrimEnd('?');

                if (mediaExtension.HasValue())
                {
                    var mediaFileType = mediaExtension.ToFileType();

                    if (mediaFileType == FileType.Image)
                    {
                        var mediaRawMeta = new FileMetaData(rawPath, string.Concat(AnalysisImagePrefix, mediaId, mediaExtension))
                                           {
                                               Bytes = skipBytes
                                                           ? null
                                                           : mediaUrl.GetImage().ToBytes()
                                           };

                        if (skipBytes || (mediaRawMeta.Bytes != null && mediaRawMeta.Bytes.Length > 0))
                        {
                            mediaRawMeta.TagMediaValues(contentType, isPermanentMedia);

                            yield return mediaRawMeta;
                        }
                    }
                    else if (includeVideo && mediaFileType == FileType.Video && mediaExtension.EndsWithOrdinalCi("mp4"))
                    {
                        var mediaRawMeta = new FileMetaData(rawPath, string.Concat(AnalysisVideoPrefix, mediaId, mediaExtension));

                        mediaRawMeta.TagMediaValues(contentType, isPermanentMedia);

                        yield return mediaRawMeta;
                    }
                }
            }

            if (thumbnailUrl.HasValue())
            {
                var thumbnailExtension = _extensionValues.FirstOrDefault(x => thumbnailUrl.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 15)?.TrimEnd('?');

                if (thumbnailExtension.HasValue() && thumbnailExtension.ToFileType() == FileType.Image)
                {
                    var thumbRawMeta = new FileMetaData(rawPath, string.Concat(AnalysisThumbnailPrefix, mediaId, thumbnailExtension))
                                       {
                                           Bytes = skipBytes
                                                       ? null
                                                       : thumbnailUrl.GetImage().ToBytes()
                                       };

                    if (skipBytes || (thumbRawMeta.Bytes != null && thumbRawMeta.Bytes.Length > 0))
                    {
                        thumbRawMeta.TagMediaValues(contentType, isPermanentMedia);

                        yield return thumbRawMeta;
                    }
                }
            }
        }
    }
}
