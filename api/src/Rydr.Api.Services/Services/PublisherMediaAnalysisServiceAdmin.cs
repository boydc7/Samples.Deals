using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

[RequiredRole("Admin")]
public class PublisherMediaAnalysisAdminService : BaseAdminApiService
{
    private static readonly int _imageTextMinConfidence = RydrEnvironment.GetAppSetting("PublisherAnalysis.MinImageTextConfidence", 85);
    private static readonly int _maxDailyStories = RydrEnvironment.GetAppSetting("PublisherAnalysis.MaxStoriesDaily", 5);
    private static readonly int _maxDailyPosts = RydrEnvironment.GetAppSetting("PublisherAnalysis.MaxPostsDaily", 1);

    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IImageAnalysisService _imageAnalysisService;
    private readonly ITextAnalysisService _textAnalysisService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly ICacheClient _cacheClient;
    private readonly IRydrDataService _rydrDataService;
    private readonly ILabelTaxonomyProcessingFilter _labelTaxonomyProcessingFilter;

    public PublisherMediaAnalysisAdminService(IFileStorageProvider fileStorageProvider,
                                              IDeferRequestsService deferRequestsService,
                                              IImageAnalysisService imageAnalysisService,
                                              ITextAnalysisService textAnalysisService,
                                              IPublisherAccountService publisherAccountService,
                                              ICacheClient cacheClient,
                                              IRydrDataService rydrDataService,
                                              ILabelTaxonomyProcessingFilter labelTaxonomyProcessingFilter)
    {
        _fileStorageProvider = fileStorageProvider;
        _deferRequestsService = deferRequestsService;
        _imageAnalysisService = imageAnalysisService;
        _textAnalysisService = textAnalysisService;
        _publisherAccountService = publisherAccountService;
        _cacheClient = cacheClient;
        _rydrDataService = rydrDataService;
        _labelTaxonomyProcessingFilter = labelTaxonomyProcessingFilter;
    }

    public async Task<OnlyResultResponse<ValueWithConfidence>> Put(PutUpdateMediaLabel request)
    {
        await _labelTaxonomyProcessingFilter.UpdateLabelAsync(request.Name, request.ParentName, request.Ignore, request.RewriteName, request.RewriteParent);

        var label = await _labelTaxonomyProcessingFilter.LookupLabelAsync(new Label
                                                                          {
                                                                              Name = request.Name,
                                                                              Parents = request.ParentName.HasValue()
                                                                                            ? new List<Parent>
                                                                                              {
                                                                                                  new()
                                                                                                  {
                                                                                                      Name = request.ParentName
                                                                                                  }
                                                                                              }
                                                                                            : null
                                                                          });

        var valWithConf = label == null
                              ? null
                              : new ValueWithConfidence
                                {
                                    Value = label.Name,
                                    ParentValue = (label.Parents?.FirstOrDefault()?.Name).ToNullIfEmpty()
                                };

        return valWithConf.AsOnlyResultResponse();
    }

    public async Task Post(PostAnalyzePublisherTopics request)
    {
        if (_fileStorageProvider.ProviderType != FileStorageProviderType.S3)
        {
            _log.Warn("Cannot perform topic analysis unless using S3 file storage provider, exiting");

            return;
        }

        var parseFileMeta = new FileMetaData(string.Concat(request.Path, ".dummy"));

        var topicResult = await _textAnalysisService.StartTopicModelingAsync(request.Path, parseFileMeta.FolderName);

        _log.DebugInfoFormat("  Started topic modeling job [{0}] for path [{1}]", topicResult, request.Path);
    }

    public async Task<StatusSimpleResponse> Post(PostAnalyzePublisherMedias request)
    {
        var publisherAccountIds = request.PublisherAccountIds
                                  ??
                                  await _rydrDataService.QueryAdHocAsync(db => db.ColumnAsync<long>(db.From<RydrPublisherAccount>()
                                                                                                      .Where(p => p.DeletedOn == null &&
                                                                                                                  p.PublisherType == PublisherType.Facebook &&
                                                                                                                  p.RydrAccountType >= RydrAccountType.Influencer &&
                                                                                                                  !p.IsSyncDisabled)
                                                                                                      .Select(p => p.Id)));

        var countQueued = 0;

        foreach (var publisherAccountId in publisherAccountIds)
        {
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null || publisherAccount.IsDeleted() ||
                !publisherAccount.OptInToAi || !publisherAccount.RydrAccountType.IsInfluencer())
            {
                continue;
            }

            _deferRequestsService.DeferLowPriRequest(new PostAnalyzePublisherMedia
                                                     {
                                                         PublisherAccountId = publisherAccountId
                                                     });

            countQueued++;
        }

        return new StatusSimpleResponse($"Enqueud [{countQueued}] accounts from potential list of [{publisherAccountIds.Count}] IDs for analysis.");
    }

    public async Task Post(PostAnalyzePublisherMedia request)
    {
        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

        if (publisherAccount == null || publisherAccount.IsDeleted())
        {
            _deferRequestsService.RemoveRecurringJob(PostAnalyzePublisherMedia.GetRecurringJobId(request.PublisherAccountId));

            return;
        }

        var (canAnalyze, analyzeInfo) = await CanAnalyzeAccountAsync(request, publisherAccount);

        if (!canAnalyze)
        {
            return;
        }

        _log.DebugInfoFormat("Starting AnalyzePublisherMedia for PublisherAccount [{0}]", publisherAccount.DisplayName());

        // Get all the medias we have synced since the last time we did this (if we've done it at all)
        var take = ((analyzeInfo.MaxStories + analyzeInfo.MaxPosts) * 3).ToDynamoBatchCeilingTake();

        var mediaToProcess = await _dynamoDb.FromQuery<DynPublisherMedia>(pm => pm.Id == publisherAccount.PublisherAccountId &&
                                                                                Dynamo.Between(pm.EdgeId,
                                                                                               analyzeInfo.AfterMediaId.ToEdgeId() ?? RecordTypeExtensions.MinLongEdgeId,
                                                                                               RecordTypeExtensions.MaxLongEdgeId))
                                            .Filter(pm => pm.DeletedOnUtc == null &&
                                                          pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                          pm.PublisherType == publisherAccount.PublisherType &&
                                                          pm.MediaCreatedAt >= analyzeInfo.StartTimestamp &&
                                                          pm.MediaCreatedAt <= analyzeInfo.EndTimestamp)
                                            .QueryAsync(_dynamoDb)
                                            .Where(dpm => dpm.PublisherMediaId > analyzeInfo.AfterMediaId)
                                            .Take(take)
                                            .ToList(take);

        if (mediaToProcess.IsNullOrEmpty())
        {
            _log.DebugInfoFormat("  No media to analyze for PublisherAccount [{0}]", publisherAccount.DisplayName());

            await UpdateAccountAnalysisMapAsync(analyzeInfo, 0, 0, 0);

            return;
        }

        _log.DebugInfoFormat("  [{0}] pieces of media to analyze for PublisherAccount [{1}]", mediaToProcess.Count, publisherAccount.DisplayName());

        // Push the raw media to S3
        // Process all the medias and create a topic file as needed
        var tempLocalTopicFileMeta = new FileMetaData(Path.GetTempPath(), string.Concat(Guid.NewGuid().ToStringId(), ".txt"));
        var maxProcessedMediaId = 0L;
        var publisherAccountMediaAnalysisAggEdgeId = string.Concat(DynItemType.PublisherMediaAnalysis.ToString(), "|agganalysis");

        if (request.Rebuild || analyzeInfo.AfterMediaId <= 0)
        { // Rebuilding aggregates, so get rid of any existing aggregate object
            await _dynamoDb.PutItemAsync(new DynPublisherAccountMediaAnalysis
                                         {
                                             Id = request.PublisherAccountId,
                                             EdgeId = publisherAccountMediaAnalysisAggEdgeId,
                                             TypeId = (int)DynItemType.PublisherMediaAnalysis
                                         });

            await _dynamoDb.PutItemAsync(new DynPublisherAccountMediaAnalysis
                                         {
                                             Id = request.PublisherAccountId,
                                             EdgeId = string.Concat(publisherAccountMediaAnalysisAggEdgeId, "|", PublisherContentType.Post),
                                             TypeId = (int)DynItemType.PublisherMediaAnalysis
                                         });

            await _dynamoDb.PutItemAsync(new DynPublisherAccountMediaAnalysis
                                         {
                                             Id = request.PublisherAccountId,
                                             EdgeId = string.Concat(publisherAccountMediaAnalysisAggEdgeId, "|", PublisherContentType.Story),
                                             TypeId = (int)DynItemType.PublisherMediaAnalysis
                                         });
        }

        var textsQueued = 0;
        var imagesQueued = 0;
        var postsProcessed = 0;
        var storiesProcessed = 0;
        var imageMediaIdsStored = new HashSet<long>();

        // If not rebuilding and/or first-time build, order by processing order for enumeration...
        var mediaToProcessEnumerable = request.Rebuild || analyzeInfo.AfterMediaId <= 0
                                           ? mediaToProcess.AsEnumerable()
                                           : mediaToProcess.OrderByDescending(m => m.AnalyzePriority)
                                                           .ThenByDescending(m => m.Id);

        try
        {
            await using(var fileStream = new StreamWriter(tempLocalTopicFileMeta.FullName))
            {
                foreach (var dynPublisherMedia in mediaToProcessEnumerable)
                {
                    if (dynPublisherMedia.PublisherMediaId > maxProcessedMediaId)
                    {
                        maxProcessedMediaId = dynPublisherMedia.PublisherMediaId;
                    }

                    if ((postsProcessed >= analyzeInfo.MaxPosts && dynPublisherMedia.ContentType == PublisherContentType.Post) ||
                        (storiesProcessed >= analyzeInfo.MaxStories && dynPublisherMedia.ContentType == PublisherContentType.Story))
                    {
                        continue;
                    }

                    // We do not analyze videos ever, and if we analyze an image/media, we keep it...
                    foreach (var fmd in dynPublisherMedia.GetRawMediaAnalysisPathAndFileMetas(includeVideo: false, isPermanentMedia: true))
                    {
                        if (await _fileStorageProvider.ExistsAsync(fmd))
                        { // File exists, tag only, no need to upload
                            if (!fmd.Tags.IsNullOrEmptyRydr())
                            {
                                var existingTags = await _fileStorageProvider.GetTagsAsync(fmd);

                                if (existingTags.IsNullOrEmptyRydr() || existingTags.Count != fmd.Tags.Count ||
                                    !existingTags.Match(fmd.Tags, StringComparer.OrdinalIgnoreCase))
                                {
                                    await _fileStorageProvider.SetTagsAsync(fmd);
                                }
                            }
                        }
                        else
                        {
                            await _fileStorageProvider.StoreAsync(fmd);
                        }

                        // If this media is text or image, analyze it appropriately
                        if (fmd.FileName.StartsWithOrdinalCi(RydrFileStoragePaths.AnalysisContentPrefix))
                        {
                            textsQueued++;

                            _deferRequestsService.DeferRequest(new PostAnalyzePublisherText
                                                               {
                                                                   PublisherAccountId = request.PublisherAccountId,
                                                                   PublisherMediaId = dynPublisherMedia.PublisherMediaId,
                                                                   Path = fmd.FullName,
                                                                   Reanalyze = request.Reanalyze
                                                               });
                        }
                        else if (fmd.FileName.StartsWithOrdinalCi(RydrFileStoragePaths.AnalysisImagePrefix) ||
                                 fmd.FileName.StartsWithOrdinalCi(RydrFileStoragePaths.AnalysisThumbnailPrefix))
                        {
                            imagesQueued++;

                            imageMediaIdsStored.Add(dynPublisherMedia.PublisherMediaId);

                            _deferRequestsService.DeferRequest(new PostAnalyzePublisherImage
                                                               {
                                                                   PublisherAccountId = request.PublisherAccountId,
                                                                   PublisherMediaId = dynPublisherMedia.PublisherMediaId,
                                                                   Path = fmd.FullName,
                                                                   Reanalyze = request.Reanalyze
                                                               });

                            // Extract any text from the image and include that in the topic analysis as well
                            var imageText = await _imageAnalysisService.GetTextAsync(fmd);

                            if (!imageText.IsNullOrEmpty())
                            {
                                var imageTextLine = string.Join(" ", imageText.Where(t => t.Confidence >= _imageTextMinConfidence &&
                                                                                          t.Type.Value.EqualsOrdinalCi(TextTypes.WORD.Value))
                                                                              .Select(t => t.DetectedText))
                                                          .ReplaceNewLines()
                                                          .ReplaceRepeatingWhitespace();

                                if (imageTextLine.HasValue())
                                {
                                    await fileStream.WriteLineAsync(imageTextLine);
                                }
                            }
                        }
                    }

                    // Store the caption of the media in the topic processing file
                    var captionLine = dynPublisherMedia.Caption.ReplaceNewLines().ReplaceRepeatingWhitespace();

                    if (captionLine.HasValue())
                    {
                        await fileStream.WriteLineAsync(captionLine);
                    }

                    switch (dynPublisherMedia.ContentType)
                    {
                        case PublisherContentType.Post:
                            postsProcessed++;

                            break;
                        case PublisherContentType.Story:
                            storiesProcessed++;

                            break;
                    }
                }
            }

            _deferRequestsService.DeferRequest(new PostAnalyzePublisherAggStats
                                               {
                                                   PublisherAccountId = request.PublisherAccountId,
                                                   EdgeId = publisherAccountMediaAnalysisAggEdgeId,
                                                   CountTextQueued = textsQueued,
                                                   CountImageQueued = imagesQueued,
                                                   CountPostsAnalyzed = postsProcessed,
                                                   CountStoriesAnalyzed = storiesProcessed
                                               }.WithAdminRequestInfo());

            // Store the topics file and kick off a topic analysis
            var topicTargetMeta = new FileMetaData(Path.Combine(RydrFileStoragePaths.GetPublisherAccountPath(RydrFileStoragePaths.AnalysisRootPath, request.PublisherAccountId), "topics"),
                                                   string.Concat("topics_", analyzeInfo.AfterMediaId, ".txt"));

            await _fileStorageProvider.StoreAsync(tempLocalTopicFileMeta, topicTargetMeta);

            if (request.ProcessTopics)
            {
                _deferRequestsService.DeferRequest(new PostAnalyzePublisherTopics
                                                   {
                                                       PublisherAccountId = request.PublisherAccountId,
                                                       Path = topicTargetMeta.Combine(topicTargetMeta.FolderName, "topics_")
                                                   }.WithAdminRequestInfo());
            }

            await UpdateAccountAnalysisMapAsync(analyzeInfo, maxProcessedMediaId, postsProcessed, storiesProcessed);

            _log.DebugInfoFormat("Completed AnalyzePublisherMedia for PublisherAccount [{0}], queued [{1}] texts, [{2}] images", publisherAccount.DisplayName(), textsQueued, imagesQueued);
        }
        finally
        {
            FileHelper.Delete(tempLocalTopicFileMeta.FullName);

            if (!imageMediaIdsStored.IsNullOrEmpty())
            {
                imageMediaIdsStored.ToBatchesOf(25)
                                   .Each(b => _deferRequestsService.DeferRequest(new ProcessRelatedMediaFiles
                                                                                 {
                                                                                     PublisherAccountId = publisherAccount.PublisherAccountId,
                                                                                     PublisherMediaIds = b.AsList()
                                                                                 }.WithAdminRequestInfo()));
            }
        }
    }

    private async Task UpdateAccountAnalysisMapAsync(PublisherMediaAnalysisInfo analysisInfo, long maxMediaIdAnalyzed, int postsAnalyzed, int storiesAnalyzed)
    {
        if (analysisInfo.MapItem.Items == null)
        {
            analysisInfo.MapItem.Items = new Dictionary<string, string>();
        }

        // Set the last media analyzed, update the rollover counts
        var currentMaxMediaIdAnalyzed = analysisInfo.MapItem.Items.GetValueOrDefault("lastMediaId").ToLong(0);

        if (maxMediaIdAnalyzed > 0 && maxMediaIdAnalyzed > currentMaxMediaIdAnalyzed)
        {
            analysisInfo.MapItem.Items["lastMediaId"] = maxMediaIdAnalyzed.ToStringInvariant();
        }

        if (analysisInfo.UpdateRollovers)
        {
            var currentStoriesRollover = analysisInfo.StoryRolloverCount;
            var currentPostsRollover = analysisInfo.PostRolloverCount;
            var excessStoriesAnalyzed = storiesAnalyzed - _maxDailyStories;
            var excessPostsAnalyzed = postsAnalyzed - _maxDailyPosts;

            // Rollover at most 10x the daily
            analysisInfo.MapItem.Items["storyRollovers"] = Math.Min((currentStoriesRollover - excessStoriesAnalyzed).Gz(0), _maxDailyStories * 10)
                                                               .ToStringInvariant();

            analysisInfo.MapItem.Items["postRollovers"] = Math.Min((currentPostsRollover - excessPostsAnalyzed).Gz(0), _maxDailyPosts * 10)
                                                              .ToStringInvariant();
        }

        // Update the last analyzed time
        analysisInfo.MapItem.ReferenceNumber = _dateTimeProvider.UtcNowTs;

        // Store the map
        await _dynamoDb.PutItemAsync(analysisInfo.MapItem);
    }

    private async Task<(bool CanAnalyze, PublisherMediaAnalysisInfo AnalysisInfo)> CanAnalyzeAccountAsync(PostAnalyzePublisherMedia request, DynPublisherAccount publisherAccount)
    {
        var nowUtc = _dateTimeProvider.UtcNow;
        var todayTimestamp = nowUtc.Date.ToUnixTimestamp();
        var yesterdayTimestamp = nowUtc.AddDays(-1).Date.ToUnixTimestamp();

        var syncInfoEdge = string.Concat("mediaanalysis_syncinfo_", publisherAccount.PublisherAccountId);

        var analysisInfo = new PublisherMediaAnalysisInfo
                           {
                               UpdateRollovers = !request.Rebuild && !request.Force,
                               MapItem = (request.Rebuild
                                              ? null
                                              : await MapItemService.DefaultMapItemService
                                                                    .TryGetMapAsync(publisherAccount.PublisherAccountId, syncInfoEdge))
                                         ??
                                         new DynItemMap
                                         {
                                             Id = publisherAccount.PublisherAccountId,
                                             EdgeId = syncInfoEdge,
                                             ReferenceNumber = 0,
                                             Items = new Dictionary<string, string>()
                                         }
                           };

        var lastAnalyzedTimestamp = analysisInfo.MapItem.ReferenceNumber.GetValueOrDefault();

        if (request.Rebuild || lastAnalyzedTimestamp <= 0)
        {
            analysisInfo.StartTimestamp = 0;
            analysisInfo.EndTimestamp = DateTimeHelper.MaxApplicationDateTs;
            analysisInfo.MaxPosts = 300;
            analysisInfo.MaxStories = 25;
        }
        else
        {
            analysisInfo.StartTimestamp = yesterdayTimestamp;
            analysisInfo.EndTimestamp = todayTimestamp;

            var currentStoriesRollover = analysisInfo.StoryRolloverCount;
            var currentPostsRollover = analysisInfo.PostRolloverCount;

            analysisInfo.MaxPosts = _maxDailyPosts + currentPostsRollover;
            analysisInfo.MaxStories = _maxDailyStories + currentStoriesRollover;
        }

        if (request.Force)
        {
            return (true, analysisInfo);
        }

        // Ensure this account can sync
        if (RydrEnvironment.IsLocalEnvironment)
        {
            _log.DebugInfo("Local environment and FORCE flag not set, skipping media analysis");

            return (false, analysisInfo);
        }

        if (!publisherAccount.OptInToAi)
        {
            _log.DebugInfoFormat("Cannot perform AnalyzePublisherMedia for PublisherAccount [{0}] - has not opted in", publisherAccount.DisplayName());

            return (false, analysisInfo);
        }

        if (!publisherAccount.RydrAccountType.IsInfluencer())
        {
            _log.DebugInfoFormat("Will not perform AnalyzePublisherMedia for PublisherAccount [{0}] - not an influencer account and FORCE flag not set", publisherAccount.DisplayName());

            return (false, analysisInfo);
        }

        // If the last time we analyzed was today or later, don't analyze - we wait for a full day of stuff to roll in then analyze
        // that day's worth of stuff based on account limits...
        if (lastAnalyzedTimestamp > todayTimestamp)
        {
            _log.DebugInfoFormat("Already performed AnalyzePublisherMedia today for PublisherAccount [{0}], skipping media analysis", publisherAccount.DisplayName());

            return (false, analysisInfo);
        }

        var nowTimestamp = _dateTimeProvider.UtcNowTs;

        // And one final check to ensure this isn't firing too often, a loose lock/protect check basically
        var lastSyncCheck = _cacheClient.TryGet<Int64Id>(syncInfoEdge, null, CacheConfig.LongConfig);

        // Update the last check time...yes, this is correctly here vs. outside the if block, update this regardless
        await _cacheClient.TrySetAsync(Int64Id.FromValue(nowTimestamp), syncInfoEdge, CacheConfig.LongConfig);

        if (lastSyncCheck != null && (nowTimestamp - lastSyncCheck.Id) <= 100)
        {
            _log.DebugInfoFormat("AnalyzePublisherMedia firing too often for PublisherAccount [{0}], skipping media analysis", publisherAccount.DisplayName());

            return (false, analysisInfo);
        }

        // Good to go
        return (true, analysisInfo);
    }

    private class PublisherMediaAnalysisInfo
    {
        public long StartTimestamp { get; set; }
        public long EndTimestamp { get; set; }
        public DynItemMap MapItem { get; set; }
        public int MaxStories { get; set; }
        public int MaxPosts { get; set; }
        public bool UpdateRollovers { get; set; }

        public long AfterMediaId => MapItem?.Items?.GetValueOrDefault("lastMediaId").ToLong(0) ?? 0;
        public int StoryRolloverCount => MapItem?.Items?.GetValueOrDefault("storyRollovers").ToInteger() ?? 0;
        public int PostRolloverCount => MapItem?.Items?.GetValueOrDefault("postRollovers").ToInteger() ?? 0;
    }
}
