using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Rekognition.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Dto.Users;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services
{
    public class HumanInternalService : BaseInternalOnlyApiService
    {
        private static readonly Dictionary<string, HumanAnswerCategoryToRydrCategoryItem> _humanAnswerCategoryMap;

        private readonly IFileStorageProvider _fileStorageProvider;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IHumanLoopService _humanLoopService;
        private readonly IMapItemService _mapItemService;

        static HumanInternalService()
        {
            _humanAnswerCategoryMap = CreateHumanAnswerCategoryMap();
        }

        public HumanInternalService(IFileStorageProvider fileStorageProvider, IDeferRequestsService deferRequestsService,
                                    IPublisherAccountService publisherAccountService, IHumanLoopService humanLoopService,
                                    IMapItemService mapItemService)
        {
            _fileStorageProvider = fileStorageProvider;
            _deferRequestsService = deferRequestsService;
            _publisherAccountService = publisherAccountService;
            _humanLoopService = humanLoopService;
            _mapItemService = mapItemService;
        }

        public Task Post(PostProcessHumanBusinessCategoryResponse request)
            => DoProcessMultiCategoryResponseAsync(request.PublisherAccountId, HumanLoopService.PublisherAccountBusinessCategoryPrefix, request);

        public Task Post(PostProcessHumanCreatorCategoryResponse request)
            => DoProcessMultiCategoryResponseAsync(request.PublisherAccountId, HumanLoopService.PublisherAccountCreatorCategoryPrefix, request);

        public Task Post(PostHumanCategorizeCreator request)
            => DoHumanProfileCategorizationAsync(request.PublisherAccountId, HumanLoopService.HumanCreatorCategoryFlowArn,
                                                 HumanLoopService.PublisherAccountCreatorCategoryPrefix, p => p.IsInfluencer());

        public Task Post(PostHumanCategorizeBusiness request)
            => DoHumanProfileCategorizationAsync(request.PublisherAccountId, HumanLoopService.HumanBusinessCategoryFlowArn,
                                                 HumanLoopService.PublisherAccountBusinessCategoryPrefix, p => p.IsBusiness());

        public async Task Post(PostProcessHumanImageModerationResponse request)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherMedia>(DynItemType.PublisherMedia, request.PublisherMediaId.ToEdgeId(),
                                                                                              true);

            if (dynPublisherMedia == null || dynPublisherMedia.IsDeleted())
            {
                return;
            }

            var dynPublisherMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherMediaAnalysis>(dynPublisherMedia.PublisherMediaId,
                                                                                                    DynPublisherMediaAnalysis.BuildEdgeId(dynPublisherMedia.PublisherType,
                                                                                                                                          dynPublisherMedia.MediaId));

            if (dynPublisherMediaAnalysis == null || dynPublisherMediaAnalysis.IsDeleted())
            {
                return;
            }

            dynPublisherMediaAnalysis.HumanResponseLocations ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dynPublisherMediaAnalysis.HumanResponseLocations[HumanLoopService.ImageModerationPrefix] = request.HumanS3Uri;

            // Get the analysis path to the image from this media
            var imageFileMeta = dynPublisherMedia.GetRawMediaAnalysisPathAndFileMetas(true)
                                                 .FirstOrDefault(fmd => fmd.FileName.StartsWithOrdinalCi(RydrFileStoragePaths.AnalysisImagePrefix) ||
                                                                        fmd.FileName.StartsWithOrdinalCi(RydrFileStoragePaths.AnalysisThumbnailPrefix));

            if (imageFileMeta == null)
            {
                return;
            }

            // Convert the answers into ModerationLabels, serialize, store alongside the original
            var imageFileHumanMeta = new FileMetaData(imageFileMeta.FolderName,
                                                      string.Concat(imageFileMeta.FileName, "_", HumanLoopService.ImageModerationAnalysisSuffix, "_human", ".json"));

            var moderations = request.Answers.IsNullOrEmpty()
                                  ? Enumerable.Empty<ModerationLabel>()
                                  : request.Answers.Select(a => new ModerationLabel
                                                                {
                                                                    Confidence = 100, // Human response...
                                                                    Name = a.Value,
                                                                    ParentName = a.ParentValue
                                                                });

            var json = moderations.ToJson();

            imageFileHumanMeta.Bytes = Encoding.UTF8.GetBytes(json);

            imageFileHumanMeta.Tags.Add(FileStorageTag.Lifecycle.ToString(), FileStorageTags.LifecycleKeep);
            imageFileHumanMeta.Tags.Add(FileStorageTag.Privacy.ToString(), FileStorageTags.PrivacyPrivate);

            await _fileStorageProvider.StoreAsync(imageFileHumanMeta, new FileStorageOptions
                                                                      {
                                                                          ContentType = "application/json",
                                                                          Encrypt = true,
                                                                          StorageClass = FileStorageClass.Intelligent
                                                                      });

            // Now have to deal with the aggregates - back out the calculated ones, add in the human resolved ones
            async Task updateAccountAggregateModerationsAsync(string aggregateEdgeId)
            {
                var dynMediaAggregateAnalysis = await _dynamoDb.GetItemAsync<DynPublisherAccountMediaAnalysis>(dynPublisherMedia.PublisherAccountId, aggregateEdgeId);

                if (dynMediaAggregateAnalysis != null && !dynMediaAggregateAnalysis.IsDeleted())
                {
                    dynMediaAggregateAnalysis.Moderations ??= new List<MediaAnalysisEntity>();

                    // Back out the automated calculated moderations from the aggregate
                    if (dynMediaAggregateAnalysis.Moderations.Count > 0 && !dynPublisherMediaAnalysis.Moderations.IsNullOrEmpty())
                    {
                        foreach (var originalModeration in dynPublisherMediaAnalysis.Moderations)
                        {
                            var aggregateModeration = dynMediaAggregateAnalysis.Moderations.FirstOrDefault(am => am.EntityText.EqualsOrdinalCi(originalModeration.Value) &&
                                                                                                                 am.EntityType.EqualsOrdinalCi(originalModeration.ParentValue));

                            if (aggregateModeration != null)
                            {
                                aggregateModeration.Occurrences -= originalModeration.Occurrences.Gz(1);
                            }
                        }
                    }

                    // Push in the human determined metrics
                    if (!request.Answers.IsNullOrEmpty())
                    {
                        foreach (var humanAnswer in request.Answers)
                        {
                            var aggregateModeration = dynMediaAggregateAnalysis.Moderations.FirstOrDefault(am => am.EntityText.EqualsOrdinalCi(humanAnswer.Value) &&
                                                                                                                 am.EntityType.EqualsOrdinalCi(humanAnswer.ParentValue));

                            if (aggregateModeration == null)
                            {
                                dynMediaAggregateAnalysis.Moderations.Add(new MediaAnalysisEntity
                                                                          {
                                                                              EntityText = humanAnswer.Value,
                                                                              EntityType = humanAnswer.ParentValue,
                                                                              Occurrences = humanAnswer.Occurrences.Gz(1)
                                                                          });
                            }
                            else
                            {
                                aggregateModeration.Occurrences += humanAnswer.Occurrences.Gz(1);
                            }
                        }
                    }

                    // Update in dynamo
                    await _dynamoDb.PutItemTrackedInterlockedAsync(dynMediaAggregateAnalysis, dma =>
                                                                                              {
                                                                                                  dma.Moderations = dynMediaAggregateAnalysis.Moderations
                                                                                                                                             .Where(m => m.Occurrences > 0)
                                                                                                                                             .OrderByDescending(m => m.Occurrences)
                                                                                                                                             .Take(50)
                                                                                                                                             .AsList();
                                                                                              });
                }
            }

            // Update the account and type-specific aggreates each
            var accountEdgeId = string.Concat(DynItemType.PublisherMediaAnalysis.ToString(), "|agganalysis");
            var mediaTypeEdgeId = string.Concat(accountEdgeId, "|", dynPublisherMedia.ContentType.ToString());

            await updateAccountAggregateModerationsAsync(accountEdgeId);
            await updateAccountAggregateModerationsAsync(mediaTypeEdgeId);

            // Now update the individual media analysis
            dynPublisherMediaAnalysis.HumanResponseLocations[HumanLoopService.ImageModerationAnalysisSuffix] = imageFileHumanMeta.FullName;
            dynPublisherMediaAnalysis.Moderations = request.Answers.NullIfEmpty();

            await _dynamoDb.PutItemTrackedInterlockedAsync(dynPublisherMediaAnalysis, dma =>
                                                                                      {
                                                                                          dma.HumanResponseLocations = dma.HumanResponseLocations.Merge(dynPublisherMediaAnalysis.HumanResponseLocations);
                                                                                          dma.Moderations = dynPublisherMediaAnalysis.Moderations;
                                                                                      });

            // All done
            _deferRequestsService.DeferLowPriRequest(new PublisherMediaAnalysisUpdated
                                                     {
                                                         PublisherMediaId = dynPublisherMediaAnalysis.PublisherMediaId,
                                                         PublisherMediaAnalysisEdgeId = dynPublisherMediaAnalysis.EdgeId
                                                     });
        }

        private async Task DoProcessMultiCategoryResponseAsync<T>(long publisherAccountId, string categoryPrefix, T request)
            where T : ProcessHumanResponseBase
        {
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null || publisherAccount.IsDeleted())
            {
                return;
            }

            publisherAccount.Tags ??= new HashSet<Tag>();

            if (request.Answers.IsNullOrEmpty())
            { // No categories specified, remove any existing
                publisherAccount.Tags.RemoveWhere(t => t.Key.EqualsOrdinalCi(categoryPrefix));
            }
            else
            { // Add those specified as needed
                foreach (var answer in request.Answers.Where(a => a.Value.HasValue()))
                {
                    var tagValue = answer.Value.LeftPart("/");

                    var mappedTags = _humanAnswerCategoryMap.ContainsKey(tagValue)
                                         ? _humanAnswerCategoryMap[tagValue]
                                         : null;

                    // If we have mapped rydr categories, these become proper categories...otherwise, they are just internal tags
                    if ((mappedTags?.RydrCategories).IsNullOrEmpty())
                    {
                        publisherAccount.Tags.Add(new Tag(categoryPrefix, tagValue));
                    }
                    else
                    {
                        foreach (var rydrCategoryName in mappedTags.RydrCategories)
                        { // These become actual categories we use...
                            publisherAccount.Tags.Add(new Tag(Tag.TagRydrCategory, rydrCategoryName));
                        }
                    }
                }
            }

            await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount, dp => dp.Tags = publisherAccount.Tags);

            var publisherHumanMapEdgeId = DynItemMap.BuildEdgeId(DynItemType.PublisherAccount, HumanLoopService.PublisherAccountHumanResponseMapKey);

            var publisherHumanMap = await _mapItemService.TryGetMapAsync(publisherAccount.PublisherAccountId, publisherHumanMapEdgeId)
                                    ??
                                    new DynItemMap
                                    {
                                        Id = publisherAccount.PublisherAccountId,
                                        EdgeId = publisherHumanMapEdgeId,
                                        MappedItemEdgeId = publisherAccount.EdgeId
                                    };

            publisherHumanMap.Items ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            publisherHumanMap.Items[categoryPrefix] = request.HumanS3Uri;

            await _mapItemService.PutMapAsync(publisherHumanMap);
        }

        private async Task DoHumanProfileCategorizationAsync(long publisherAccountId, string flowArn, string categoryPrefix, Func<DynPublisherAccount, bool> predicate)
        {
            if (flowArn.IsNullOrEmpty())
            {
                _log.DebugInfo("Ignoring human profile categorize request, no flow ARN defined.");

                return;
            }

            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null || publisherAccount.IsDeleted())
            {
                return;
            }

            if (!predicate(publisherAccount))
            {
                _log.WarnFormat("Ignoring human profile categorize request for PublisherAccount [{0}], not a proper account type.", publisherAccount.DisplayName());

                return;
            }

            if (!publisherAccount.UserName.HasValue())
            {
                _log.WarnFormat("Ignoring human profile categorize request for PublisherAccount [{0}], no IG UserName found.", publisherAccount.DisplayName());

                return;
            }

            await _humanLoopService.StartHumanLoopAsync(flowArn, categoryPrefix, publisherAccount.PublisherAccountId.ToStringInvariant(),
                                                        new
                                                        {
                                                            TaskObject = $"https://www.instagram.com/{publisherAccount.UserName}"
                                                        });
        }

        private static Dictionary<string, HumanAnswerCategoryToRydrCategoryItem> CreateHumanAnswerCategoryMap()
        {
            var mapFile = RydrEnvironment.GetAppSetting("HumanFlow.HumanCategoryToRydrMapFile");
            var map = new Dictionary<string, HumanAnswerCategoryToRydrCategoryItem>(StringComparer.OrdinalIgnoreCase);

            if (mapFile.IsNullOrEmpty())
            {
                return map;
            }

            var s3 = RydrEnvironment.Container.ResolveNamed<IFileStorageProvider>(FileStorageProviderType.S3.ToString());
            var mapFileMeta = new FileMetaData(mapFile);

            if (!s3.ExistsAsync(mapFileMeta).GetAwaiter().GetResult())
            {
                return map;
            }

            var fileContents = s3.GetAsync(mapFileMeta).GetAwaiter().GetResult();
            var fileMap = Encoding.UTF8.GetString(fileContents).FromJson<HumanAnswerCategoryToRydrCategoryMap>();

            if ((fileMap?.Categories).IsNullOrEmpty())
            {
                return map;
            }

            foreach (var categoryItem in fileMap.Categories)
            {
                map.Add(categoryItem.HumanCategory, categoryItem);
            }

            return map;
        }

        private class HumanAnswerCategoryToRydrCategoryMap
        {
            public List<HumanAnswerCategoryToRydrCategoryItem> Categories { get; set; }
        }

        private class HumanAnswerCategoryToRydrCategoryItem
        {
            public string HumanCategory { get; set; }
            public List<string> RydrCategories { get; set; }
        }
    }
}
