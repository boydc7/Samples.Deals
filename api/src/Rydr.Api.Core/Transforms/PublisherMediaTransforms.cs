using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Transforms
{
    public static class PublisherMediaTransforms
    {
        // Just under 7 days (max allowed for signed S3 urls)
        public const int RydrMediaUrlsExpirationSeconds = 600000;
        public const int RydrMediaUrlsExpiredSeconds = RydrMediaUrlsExpirationSeconds - 1200;

        private static readonly Dictionary<FileType, Func<DynFile, FileConvertTypeArgumentsBase>> _fileTypeThumbnailConvertArgsMap =
            new Dictionary<FileType, Func<DynFile, FileConvertTypeArgumentsBase>>
            {
                {
                    FileType.Video, f => new VideoThumbnailConvertGenericTypeArguments()
                },
                {
                    FileType.Image, f => new ImageConvertTypeArguments
                                         {
                                             Height = 161,
                                             Width = 161
                                         }
                }
            };

        private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();
        private static readonly IRydrDataService _rydrDataService = RydrEnvironment.Container.Resolve<IRydrDataService>();
        private static readonly IFileStorageProvider _fileStorageProvider = RydrEnvironment.Container.Resolve<IFileStorageProvider>();
        private static readonly IFileStorageService _fileStorageService = RydrEnvironment.Container.Resolve<IFileStorageService>();
        private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();
        private static readonly string _s3Region = RydrEnvironment.GetAppSetting("AWS.S3.Region", "us-west-2");

        public static PublisherAccountMediaAnalysisResult ToPublisherAccountMediaAnalysisResult(this DynPublisherAccountMediaAnalysis source, PublisherContentType contentType = PublisherContentType.Unknown)
        {
            if (source == null)
            {
                return null;
            }

            var publisherAccountMediaAnalysis = source.ConvertTo<PublisherAccountMediaAnalysisResult>();

            publisherAccountMediaAnalysis.PostsAnalyzed = source.CountPostsAnalyzed;
            publisherAccountMediaAnalysis.StoriesAnalyzed = source.CountStoriesAnalyzed;
            publisherAccountMediaAnalysis.ImageCount = source.CountImageAnalyzed;
            publisherAccountMediaAnalysis.ImageFacesCount = source.CountFacesAnalyzed;
            publisherAccountMediaAnalysis.TextCount = source.CountTextAnalyzed;
            publisherAccountMediaAnalysis.ImagesQueued = source.CountImageQueued;
            publisherAccountMediaAnalysis.TextsQueued = source.CountTextQueued;

            publisherAccountMediaAnalysis.ImageFacesAvgAge = source.CountFacesAnalyzed > 0
                                                                 ? Math.Round(source.ImageFacesAgeSum / (double)source.CountFacesAnalyzed / 2d, 2)
                                                                 : 0;

            publisherAccountMediaAnalysis.ImageLabels = source.ImageLabels?
                                                              .Select(l => new ValueWithConfidence
                                                                           {
                                                                               Value = l.EntityText,
                                                                               ParentValue = l.EntityType,
                                                                               Occurrences = l.Occurrences
                                                                           })
                                                              .AsList();

            publisherAccountMediaAnalysis.ImageModerations = source.Moderations?
                                                                   .Select(l => new ValueWithConfidence
                                                                                {
                                                                                    Value = l.EntityText,
                                                                                    ParentValue = l.EntityType,
                                                                                    Occurrences = l.Occurrences
                                                                                })
                                                                   .AsList();

            publisherAccountMediaAnalysis.TextEntities = source.PopularEntities?
                                                               .Select(l => new ValueWithConfidence
                                                                            {
                                                                                Value = l.EntityText,
                                                                                ParentValue = l.EntityType,
                                                                                Occurrences = l.Occurrences
                                                                            })
                                                               .AsList();

            publisherAccountMediaAnalysis.TotalSentimentOccurrences = source.PositiveSentimentOccurrences + source.NegativeSentimentOccurrences +
                                                                      source.NeutralSentimenOccurrences + source.MixedSentimentOccurrences;

            if (publisherAccountMediaAnalysis.TotalSentimentOccurrences > 0)
            {
                publisherAccountMediaAnalysis.TextPositiveSentimentPercentage = Math.Round((source.PositiveSentimentOccurrences / (double)publisherAccountMediaAnalysis.TotalSentimentOccurrences) * 100.0d, 2);
                publisherAccountMediaAnalysis.TextNegativeSentimentPercentage = Math.Round((source.NegativeSentimentOccurrences / (double)publisherAccountMediaAnalysis.TotalSentimentOccurrences) * 100.0d, 2);
                publisherAccountMediaAnalysis.TextNeutralSentimentPercentage = Math.Round((source.NeutralSentimenOccurrences / (double)publisherAccountMediaAnalysis.TotalSentimentOccurrences) * 100.0d, 2);
                publisherAccountMediaAnalysis.TextMixedSentimentPercentage = Math.Round((source.MixedSentimentOccurrences / (double)publisherAccountMediaAnalysis.TotalSentimentOccurrences) * 100.0d, 2);
            }

            publisherAccountMediaAnalysis.ContentType = contentType == PublisherContentType.Unknown
                                                            ? (PublisherContentType?)null
                                                            : contentType;

            return publisherAccountMediaAnalysis;
        }

        public static IEnumerable<RydrPublisherMediaStat> ToRydrPublisherMediaStats(this DynPublisherMediaStat source)
        {
            var endTime = source.EndTime.ToDateTime();

            yield return new RydrPublisherMediaStat
                         {
                             MediaId = source.PublisherMediaId,
                             PublisherAccountId = source.PublisherAccountId,
                             PeriodEnumId = _rydrDataService.GetOrCreateRydrEnumId(source.Period),
                             EndTime = endTime,
                             StatEnumId = _rydrDataService.GetOrCreateRydrEnumId("engagementrating"),
                             Value = source.EngagementRating
                         };

            yield return new RydrPublisherMediaStat
                         {
                             MediaId = source.PublisherMediaId,
                             PublisherAccountId = source.PublisherAccountId,
                             PeriodEnumId = _rydrDataService.GetOrCreateRydrEnumId(source.Period),
                             EndTime = endTime,
                             StatEnumId = _rydrDataService.GetOrCreateRydrEnumId("trueengagementrating"),
                             Value = source.TrueEngagementRating
                         };

            if (source.Stats.IsNullOrEmpty())
            {
                yield break;
            }

            foreach (var publisherStatValue in source.Stats)
            {
                yield return new RydrPublisherMediaStat
                             {
                                 MediaId = source.PublisherMediaId,
                                 PublisherAccountId = source.PublisherAccountId,
                                 PeriodEnumId = _rydrDataService.GetOrCreateRydrEnumId(source.Period),
                                 EndTime = endTime,
                                 StatEnumId = _rydrDataService.GetOrCreateRydrEnumId(publisherStatValue.Name),
                                 Value = publisherStatValue.Value
                             };
            }
        }

        public static PublisherMedia ToPublisherMedia(this IgMedia source, long publisherAccountId)
        {
            var to = source.ConvertTo<PublisherMedia>();

            to.Id = 0;
            to.PublisherAccountId = publisherAccountId;
            to.PublisherType = PublisherType.Facebook; // Correctly Facebook...media remains Facebook media for now
            to.MediaId = source.Id;
            to.PublisherUrl = source.Permalink;
            to.CreatedAt = source.Timestamp.ToDateTime(DateTime.UtcNow, true);
            to.LifetimeStats = null;

            to.ContentType = PublisherContentType.Post;

            return to;
        }

        public static PublisherMedia ToPublisherMedia(this FbIgMedia source, PublisherContentType contentType, long publisherAccountId)
        {
            var to = source.ConvertTo<PublisherMedia>();

            to.Id = 0;
            to.PublisherAccountId = publisherAccountId;
            to.PublisherType = PublisherType.Facebook;
            to.MediaId = source.Id;
            to.ActionCount = source.LikeCount;
            to.CommentCount = source.CommentsCount;
            to.PublisherUrl = source.Permalink;
            to.CreatedAt = source.Timestamp.ToDateTime(DateTime.UtcNow, true);
            to.LifetimeStats = null;

            to.ContentType = contentType;

            return to;
        }

        public static ValueTask<PublisherMedia> ToPublisherMediaAsync(this DynPublisherMedia from)
            => ToPublisherMediaAsyncValue(from);

        public static async ValueTask<PublisherMedia> ToPublisherMediaAsyncValue(this DynPublisherMedia from)
        {
            if (from == null)
            {
                return null;
            }

            var to = from.ConvertTo<PublisherMedia>();

            to.PublisherAccountId = from.PublisherAccountId;
            to.Id = from.PublisherMediaId;
            to.PublisherType = from.PublisherType;
            to.MediaId = from.MediaId;
            to.CreatedAt = from.MediaCreatedAt.ToDateTime();
            to.IsPreBizAccountConversionMedia = from.PreBizAccountConversionMediaErrorCount > 0;

            await ValidatePublisherMediaUrlsAsync(from);
            to.IsMediaRydrHosted = from.IsRydrHosted;

            to.MediaUrl = from.MediaUrl;
            to.ThumbnailUrl = from.ThumbnailUrl;

            return to;
        }

        public static async ValueTask<PublisherApprovedMedia> ToPublisherApprovedMediaAsync(this DynPublisherApprovedMedia from)
        {
            var to = from.ConvertTo<PublisherApprovedMedia>();

            to.Id = from.PublisherApprovedMediaId;
            to.PublisherAccountId = from.PublisherAccountId;

            // Check for need to re-generate signed urls
            await PopulateAndStoreFileMediaUrlsAsync(from);

            to.MediaUrl = from.MediaUrl;
            to.ThumbnailUrl = from.ThumbnailUrl;

            return to;
        }

        public static async ValueTask<DynPublisherApprovedMedia> ToDynPublisherApprovedMediaAsync(this PublisherApprovedMedia from, DynPublisherApprovedMedia existingPublisherApprovedMedia = null)
        {
            var to = from.ConvertTo<DynPublisherApprovedMedia>();

            if (existingPublisherApprovedMedia == null)
            {
                to.PublisherApprovedMediaId = Sequences.Provider.Next();
                to.MediaFileId = from.MediaFileId;

                // NOTE: Do not set ThumbnailFileId here purposely - if this is a file that gets a thumbnail, it'll be generated and stored async/later
                to.Id = from.PublisherAccountId;
                to.UrlsGeneratedOn = 0;
            }
            else
            {
                to.PublisherApprovedMediaId = existingPublisherApprovedMedia.PublisherApprovedMediaId;
                to.MediaFileId = from.MediaFileId.Gz(existingPublisherApprovedMedia.MediaFileId);
                to.ThumbnailUrl = existingPublisherApprovedMedia.ThumbnailUrl; // Thumbnails aren't specified by user...
                to.Id = existingPublisherApprovedMedia.PublisherAccountId; // Cannot change the publisher account...
                to.UrlsGeneratedOn = existingPublisherApprovedMedia.UrlsGeneratedOn;
            }

            to.DynItemType = DynItemType.ApprovedMedia;
            to.UpdateDateTimeTrackedValues(existingPublisherApprovedMedia);

            await PopulateFileMediaUrlsAsync(to);

            return to;
        }

        public static async ValueTask<DynPublisherMedia> ToDynPublisherMediaAsync(this PublisherMedia from, DynPublisherMedia existingMedia = null, bool forceNoPut = false)
        {
            var to = from.ConvertTo<DynPublisherMedia>();

            to.PublisherAccountId = existingMedia?.PublisherAccountId ?? from.PublisherAccountId;

            if (existingMedia == null)
            {
                to.DynItemType = DynItemType.PublisherMedia;
                to.UpdateDateTimeTrackedValues();
                to.EdgeId = Sequences.Provider.Next().ToEdgeId();
                to.LastSyncedOn = DateTimeHelper.UtcNowTs;
            }
            else
            {
                to.TypeId = existingMedia.TypeId;
                to.Id = existingMedia.Id;
                to.EdgeId = existingMedia.EdgeId;
                to.UpdateDateTimeTrackedValues(existingMedia);
                to.LastSyncedOn = existingMedia.LastSyncedOn;
            }

            to.PublisherType = existingMedia?.PublisherType ?? from.PublisherType;
            to.MediaId = existingMedia?.MediaId ?? from.MediaId;
            to.IsCompletionMedia = existingMedia?.IsCompletionMedia ?? false;
            to.IsAnalyzed = existingMedia?.IsAnalyzed ?? false;
            to.AnalyzePriority = existingMedia?.AnalyzePriority ?? 0;

            to.MediaCreatedAt = existingMedia?.MediaCreatedAt ?? (from.CreatedAt > DateTimeHelper.MinApplicationDate
                                                                      ? from.CreatedAt.ToUnixTimestamp()
                                                                      : DateTimeHelper.UtcNowTs);

            to.ReferenceId = DynPublisherMedia.BuildRefId(to.PublisherType, to.MediaId);
            to.ExpiresAt = GetMediaExpiresAt(to.MediaCreatedAt);

            await ValidatePublisherMediaUrlsAsync(to);

            to.IsRydrHosted = to.IsRydrHosted || (existingMedia?.IsRydrHosted ?? false);

            return to;
        }

        public static PublisherMediaStat ToPublisherMediaStat(this DynPublisherMediaStat from)
        {
            var to = from.ConvertTo<PublisherMediaStat>();

            to.PublisherMediaId = from.PublisherMediaId;
            to.Stats = from.Stats.AsList();
            to.LastSyncedOn = from.ModifiedOn;

            return to;
        }

        // An IgMedia is still a Facebook media, just less to it...
        public static DynPublisherMedia ToDynPublisherMedia(this IgMedia source, long forPublisherAccountId)
            => ToDynPublisherMedia(source.ConvertTo<FbIgMedia>(), forPublisherAccountId, PublisherContentType.Post);

        public static DynPublisherMedia ToDynPublisherMedia(this FbIgMedia source, long forPublisherAccountId, PublisherContentType contentType)
        {
            var to = source.ConvertTo<DynPublisherMedia>();

            to.PublisherAccountId = forPublisherAccountId;
            to.PublisherType = PublisherType.Facebook;
            to.MediaId = source.Id;

            to.ReferenceId = DynPublisherMedia.BuildRefId(to.PublisherType, to.MediaId);
            to.EdgeId = Sequences.Provider.Next().ToEdgeId();
            to.DynItemType = DynItemType.PublisherMedia;

            to.PublisherUrl = source.Permalink;
            to.ActionCount = source.LikeCount;
            to.CommentCount = source.CommentsCount;
            to.LastSyncedOn = DateTimeHelper.UtcNowTs;

            if (contentType == PublisherContentType.Unknown)
            {
                contentType = to.PublisherUrl.IndexOf("/stories", StringComparison.OrdinalIgnoreCase) > 10
                                  ? PublisherContentType.Story
                                  : PublisherContentType.Post;
            }

            to.ContentType = contentType;

            var mediaCreatedAt = source.Timestamp.ToDateTime(DateTimeHelper.MinApplicationDate, true);

            to.ExpiresAt = GetMediaExpiresAt(mediaCreatedAt);

            to.MediaCreatedAt = mediaCreatedAt.ToUnixTimestamp();

            to.UpdateDateTimeTrackedValues();

            to.Dirty();

            return to;
        }

        public static DynPublisherMediaStat ToDynPublisherMediaStat(this DynPublisherMedia forPublisherMedia, IEnumerable<PublisherStatValue> withStats)
        {
            var to = new DynPublisherMediaStat
                     {
                         PublisherMediaId = forPublisherMedia.PublisherMediaId,
                         EdgeId = DynPublisherMediaStat.BuildEdgeId(FbIgInsights.LifetimePeriod, FbIgInsights.LifetimeEndTime),
                         Period = FbIgInsights.LifetimePeriod,
                         EndTime = FbIgInsights.LifetimeEndTime,
                         DynItemType = DynItemType.PublisherMediaStat,
                         Stats = new HashSet<PublisherStatValue>(),
                         IsCompletionMediaStat = forPublisherMedia.IsCompletionMedia,
                         ContentType = forPublisherMedia.ContentType,
                         PublisherAccountId = forPublisherMedia.PublisherAccountId
                     };

            to.UpdateDateTimeTrackedValues(forPublisherMedia);
            to.ExpiresAt = forPublisherMedia.ExpiresAt ?? GetMediaExpiresAt(forPublisherMedia.MediaCreatedAt);

            // Take the new stats, add any missing, set...
            var newStats = withStats.AsHashSet();

            if (!to.Stats.IsNullOrEmpty())
            {
                newStats.UnionWith(to.Stats);
            }

            to.Stats = newStats;

            return to;
        }

        public static ICollection<DynPublisherMediaStat> ToDynPublisherMediaStats(this IEnumerable<FbIgMediaInsight> source,
                                                                                  DynPublisherMedia forPublisherMedia, long minStatEndTime = 0)
        {
            if (source == null)
            {
                return null;
            }

            var statMap = new Dictionary<long, DynPublisherMediaStat>();

            foreach (var mediaInsight in source)
            {
                foreach (var insightStat in mediaInsight.Values.Where(v => v.Value > 0))
                {
                    var statEndTime = insightStat.EndTime.IsNullOrEmpty() || mediaInsight.Period.EqualsOrdinalCi(FbIgInsights.LifetimePeriod)
                                          ? FbIgInsights.LifetimeEndTime
                                          : insightStat.EndTime.ToDateTimeNullable(convertToUtcVsGuarding: true)?.ToUnixTimestamp() ?? 0;

                    if (statEndTime < minStatEndTime)
                    {
                        continue;
                    }

                    var dynStat = statMap.ContainsKey(statEndTime)
                                      ? statMap[statEndTime]
                                      : new DynPublisherMediaStat
                                        {
                                            PublisherMediaId = forPublisherMedia.PublisherMediaId,
                                            EdgeId = DynPublisherMediaStat.BuildEdgeId(mediaInsight.Period, statEndTime),
                                            Period = mediaInsight.Period,
                                            EndTime = statEndTime,
                                            DynItemType = DynItemType.PublisherMediaStat,
                                            Stats = new HashSet<PublisherStatValue>(),
                                            ContentType = forPublisherMedia.ContentType,
                                            PublisherAccountId = forPublisherMedia.PublisherAccountId,
                                            IsCompletionMediaStat = forPublisherMedia.IsCompletionMedia
                                        };

                    if (!dynStat.ExpiresAt.HasValue)
                    {
                        dynStat.UpdateDateTimeTrackedValues(forPublisherMedia);
                        dynStat.ExpiresAt = forPublisherMedia.ExpiresAt ?? GetMediaExpiresAt(forPublisherMedia.MediaCreatedAt);
                    }

                    dynStat.Stats.Add(new PublisherStatValue
                                      {
                                          Name = mediaInsight.Name,
                                          Value = insightStat.Value
                                      });

                    statMap[statEndTime] = dynStat;
                }
            }

            if (statMap.ContainsKey(FbIgInsights.LifetimeEndTime))
            {
                statMap[FbIgInsights.LifetimeEndTime].Stats.Add(new PublisherStatValue
                                                                {
                                                                    Name = FbIgInsights.CommentsName,
                                                                    Value = forPublisherMedia.CommentCount
                                                                });

                statMap[FbIgInsights.LifetimeEndTime].Stats.Add(new PublisherStatValue
                                                                {
                                                                    Name = FbIgInsights.ActionsName,
                                                                    Value = forPublisherMedia.ActionCount
                                                                });
            }

            return statMap.Values;
        }

        public static DynPublisherMediaComment ToDynPublisherMediaComment(this FbIgMediaComment source, DynPublisherMedia forPublisherMedia)
        {
            var to = source.ConvertTo<DynPublisherMediaComment>();

            to.PublisherMediaId = forPublisherMedia.PublisherMediaId;
            to.EdgeId = DynPublisherMediaComment.BuildEdgeId(PublisherType.Facebook, source.Id);
            to.ActionCount = source.LikeCount;
            to.CommentCreatedAt = source.Timestamp.ToDateTime(forPublisherMedia.MediaCreatedAt.ToDateTime(), true).ToUnixTimestamp();
            to.DynItemType = DynItemType.PublisherMediaComment;

            to.UpdateDateTimeTrackedValues();
            to.ExpiresAt = to.CommentCreatedAt + (3600 * 24 * 35); // Add 35 days...

            return to;
        }

        public static void PopulatePublisherMediaUrls(this IGenerateMediaUrls source, FileMetaData withFileMetaData, FileMetaData withThumbnailMetaData = null)
        {
            // If it is a private piece of media that is not publicly available we have to roll forward a signed link
            var rydrMediaFileMetaIsPrivate = withFileMetaData.Tags != null && withFileMetaData.Tags.ContainsKey(FileStorageTag.Privacy.ToString()) &&
                                             withFileMetaData.Tags[FileStorageTag.Privacy.ToString()].EqualsOrdinalCi(FileStorageTags.PrivacyPrivate);

            if (rydrMediaFileMetaIsPrivate)
            {
                source.UrlsGeneratedOn = DateTimeHelper.UtcNowTs;

                // Signed url that expires in 10k minutes (just under 7 days)
                source.MediaUrl = _fileStorageProvider.GetDownloadUrl(RydrMediaUrlsExpirationSeconds / 60,
                                                                      withFileMetaData,
                                                                      true);

                if (withThumbnailMetaData != null)
                {
                    source.ThumbnailUrl = _fileStorageProvider.GetDownloadUrl(RydrMediaUrlsExpirationSeconds / 60,
                                                                              withThumbnailMetaData,
                                                                              true);
                }
            }
            else
            { // Publicly available, use the s3 direct https link
                source.UrlsGeneratedOn = 0;

                var bpk = new S3BucketPrefixKey(withFileMetaData);

                source.MediaUrl = string.Concat("https://", bpk.BucketName, ".s3-", _s3Region, ".amazonaws.com/", bpk.Key);

                if (withThumbnailMetaData != null)
                {
                    var tbpk = new S3BucketPrefixKey(withThumbnailMetaData);

                    source.ThumbnailUrl = string.Concat("https://", tbpk.BucketName, ".s3-", _s3Region, ".amazonaws.com/", tbpk.Key);
                }
            }
        }

        public static async ValueTask<bool> PopulateFileMediaUrlsAsync<T>(this T source)
            where T : IGenerateFileMediaUrls
        {
            var mediaFileId = source.MediaFileId;

            if (mediaFileId <= 0)
            {
                return false;
            }

            var utcNow = DateTimeHelper.UtcNowTs;

            var rydrUrlAgeSeconds = utcNow - source.UrlsGeneratedOn;

            // If we need to re-generate signed urls, do so now
            if (rydrUrlAgeSeconds < RydrMediaUrlsExpiredSeconds)
            {
                return false;
            }

            source.UrlsGeneratedOn = utcNow;

            var dynMediaFile = await _fileStorageService.TryGetFileAsync(mediaFileId);

            var mediaFileTransferInfo = await _fileStorageService.GetDownloadInfoAsync<FileConvertTypeArgumentsBase>(dynMediaFile.Id, dynMediaFile, true,
                                                                                                                     dynMediaFile.FileType == FileType.Video
                                                                                                                         ? VideoConvertGenericTypeArguments.Instance
                                                                                                                         : null,
                                                                                                                     (RydrMediaUrlsExpirationSeconds / 60));

            source.MediaUrl = mediaFileTransferInfo.Url;

            if (source.ThumbnailUrl.HasValue())
            {
                var convertArgs = _fileTypeThumbnailConvertArgsMap.ContainsKey(dynMediaFile.FileType)
                                      ? _fileTypeThumbnailConvertArgsMap[dynMediaFile.FileType](dynMediaFile)
                                      : null;

                var thumbnailTransferInfo = await _fileStorageService.GetDownloadInfoAsync(dynMediaFile.Id, dynMediaFile, true, convertArgs,
                                                                                           (RydrMediaUrlsExpirationSeconds / 60));

                source.ThumbnailUrl = thumbnailTransferInfo.Url;
            }

            return true;
        }

        private static long GetMediaExpiresAt(long mediaCreatedAt)
            => GetMediaExpiresAt(mediaCreatedAt.ToDateTime());

        private static long GetMediaExpiresAt(DateTime mediaCreatedAt)
            => mediaCreatedAt > DateTimeHelper.MinApplicationDate
                   ? mediaCreatedAt.Date.AddDays(Math.Abs(PublisherMediaValues.DaysBackToKeepMedia)).ToUnixTimestamp()
                   : DateTimeHelper.UtcNow.AddDays(2).ToUnixTimestamp();

        private static async ValueTask ValidatePublisherMediaUrlsAsync(this DynPublisherMedia source, bool forceNoPut = false)
        {
            if (source.PublisherType != PublisherType.Rydr && source.UrlsGeneratedOn <= 0)
            {
                return;
            }

            if (source.MediaFileId > 0)
            {
                source.IsRydrHosted = true;

                if (forceNoPut)
                {
                    await PopulateFileMediaUrlsAsync(source);
                }
                else
                {
                    await PopulateAndStoreFileMediaUrlsAsync(source);
                }

                return;
            }

            if (source.PublisherType == PublisherType.Rydr)
            {
                source.IsRydrHosted = true;

                return;
            }

            var urlAgeSeconds = DateTimeHelper.UtcNowTs - source.UrlsGeneratedOn;

            if (urlAgeSeconds < RydrMediaUrlsExpiredSeconds)
            {
                return;
            }

            // Expired, get an updated signed url synchronously
            var mediaFileMetaIsImage = false;
            FileMetaData mediaFileMeta = null;
            FileMetaData thumbnailFileMeta = null;

            source.GetRawMediaAnalysisPathAndFileMetas(true, true)
                  .Select(f => (FileMeta: f, FileType: f.FileExtension.ToFileType()))
                  .Where(t => t.FileType == FileType.Image || t.FileType == FileType.Video)
                  .Each(m =>
                        {
                            if (mediaFileMeta == null)
                            {
                                mediaFileMeta = m.FileMeta;
                                mediaFileMetaIsImage = m.FileType == FileType.Image;
                            }
                            else if (m.FileType == FileType.Image)
                            {
                                thumbnailFileMeta = m.FileMeta;
                            }
                            else if (m.FileType == FileType.Video)
                            {
                                if (mediaFileMetaIsImage)
                                {
                                    thumbnailFileMeta = mediaFileMeta;
                                }

                                mediaFileMeta = m.FileMeta;
                                mediaFileMetaIsImage = false;
                            }
                        });

            if (mediaFileMeta == null)
            {
                source.IsRydrHosted = false;

                return;
            }

            source.IsRydrHosted = true;

            PopulatePublisherMediaUrls(source, mediaFileMeta, thumbnailFileMeta);

            _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                               {
                                                   PublisherAccountId = source.PublisherAccountId,
                                                   PublisherMediaIds = new List<long>
                                                                       {
                                                                           source.RydrMediaId
                                                                       }
                                               }.WithAdminRequestInfo());
        }

        private static async ValueTask PopulateAndStoreFileMediaUrlsAsync<T>(T source)
            where T : IGenerateFileMediaUrls, IDynItem
        {
            if (source.MediaFileId <= 0)
            {
                return;
            }

            var repopulated = await PopulateFileMediaUrlsAsync(source);

            if (repopulated)
            {
                await _dynamoDb.PutItemAsync(source);
            }
        }
    }
}
