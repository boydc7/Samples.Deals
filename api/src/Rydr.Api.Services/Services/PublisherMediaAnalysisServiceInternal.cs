using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Comprehend;
using Amazon.Rekognition.Model;
using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;
using GenderType = Amazon.Rekognition.GenderType;

namespace Rydr.Api.Services.Services
{
    public class PublisherMediaAnalysisInternalService : BaseInternalOnlyApiService
    {
        private static readonly HashSet<string> _textEntitiesToIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                                                        {
                                                                            EntityType.DATE.Value,
                                                                            EntityType.QUANTITY.Value
                                                                        };

        private static readonly int _minFacesConfidence = RydrEnvironment.GetAppSetting("PublisherAnalysis.MinFacesConfidence", 95);
        private static readonly int _minImageConfidence = RydrEnvironment.GetAppSetting("PublisherAnalysis.MinImageConfidence", 95);
        private static readonly int _minTextConfidence = RydrEnvironment.GetAppSetting("PublisherAnalysis.MinTextConfidence", 90);
        private static readonly int _minModerationConfidence = RydrEnvironment.GetAppSetting("PublisherAnalysis.MinModerationConfidence", 95);
        private static readonly int _minModerationViolenceConfidence = RydrEnvironment.GetAppSetting("PublisherAnalysis.MinModerationViolenceConfidence", 95);

        private readonly IFileStorageProvider _fileStorageProvider;
        private readonly IImageAnalysisService _imageAnalysisService;
        private readonly ITextAnalysisService _textAnalysisService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IElasticClient _elasticClient;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly ILabelTaxonomyProcessingFilter _labelTaxonomyProcessingFilter;
        private readonly IDistributedLockService _distributedLockService;

        public PublisherMediaAnalysisInternalService(IFileStorageProvider fileStorageProvider,
                                                     IImageAnalysisService imageAnalysisService,
                                                     ITextAnalysisService textAnalysisService,
                                                     IPublisherAccountService publisherAccountService,
                                                     IElasticClient elasticClient,
                                                     IDeferRequestsService deferRequestsService,
                                                     ILabelTaxonomyProcessingFilter labelTaxonomyProcessingFilter,
                                                     IDistributedLockService distributedLockService)
        {
            _fileStorageProvider = fileStorageProvider;
            _imageAnalysisService = imageAnalysisService;
            _textAnalysisService = textAnalysisService;
            _publisherAccountService = publisherAccountService;
            _elasticClient = elasticClient;
            _deferRequestsService = deferRequestsService;
            _labelTaxonomyProcessingFilter = labelTaxonomyProcessingFilter;
            _distributedLockService = distributedLockService;
        }

        public async Task Post(PostAnalyzePublisherAggStats request)
        {
            const string lockCategory = nameof(DynPublisherAccountMediaAnalysis);

            var attempts = 0;
            var lockId = string.Concat(request.PublisherAccountId, "|", request.EdgeId);

            do
            {
                using(var lockItem = _distributedLockService.TryGetKeyLock(lockId, lockCategory, 7))
                {
                    if (lockItem == null)
                    {
                        await Task.Delay(RandomProvider.GetRandomIntBeween(150, 550));

                        attempts++;

                        continue;
                    }

                    // Update the queued counts on the aggregate object
                    _dynamoDb.UpdateItem(_dynamoDb.UpdateExpression<DynPublisherAccountMediaAnalysis>(request.PublisherAccountId, request.EdgeId)
                                                  .Add(() => new DynPublisherAccountMediaAnalysis
                                                             {
                                                                 CountTextQueued = request.CountTextQueued,
                                                                 CountImageQueued = request.CountImageQueued,
                                                                 CountPostsAnalyzed = request.CountPostsAnalyzed,
                                                                 CountStoriesAnalyzed = request.CountStoriesAnalyzed
                                                             }));

                    return;
                }
            } while (attempts <= 50);

            throw new TimeoutException($"Could not get interlock for UpdateItem operation on [{lockId}-{lockCategory}], in PostAnalyzePublisherAggStats");
        }

        public async Task Post(PostAnalyzePublisherImage request)
        {
            var imageFileMeta = new FileMetaData(request.Path);

            if (!(await _fileStorageProvider.ExistsAsync(imageFileMeta)))
            {
                _log.WarnFormat("Image file [{0}] does not eixst, exiting", imageFileMeta.FullName);

                return;
            }

            if (_fileStorageProvider.ProviderType != FileStorageProviderType.S3)
            {
                imageFileMeta.Bytes = await FileHelper.ReadAllBytesAsync(imageFileMeta.FullName);
            }

            var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

            var dynPublisherMedia = await _dynamoDb.GetItemAsync<DynPublisherMedia>(request.PublisherAccountId, request.PublisherMediaId.ToEdgeId());

            var moderations = await ProcessFileExtra(imageFileMeta, HumanLoopService.ImageModerationAnalysisSuffix,
                                                     () => _imageAnalysisService.GetImageModerationsAsync(imageFileMeta, dynPublisherMedia.PublisherMediaId.ToStringInvariant()),
                                                     request.Reanalyze);

            // Store the individual media value and aggregate
            await ProcessMediaAnalysisAggregatesAsync(request.PublisherAccountId,
                                                      moderations,
                                                      e => Task.FromResult(e.Name.HasValue() &&
                                                                           e.Confidence > _minModerationConfidence &&
                                                                           (e.ParentName.IsNullOrEmpty() ||
                                                                            e.Confidence > _minModerationViolenceConfidence ||
                                                                            !e.ParentName.ContainsAny(PublisherMediaAnalysisService.ViolenceCategories, StringComparison.OrdinalIgnoreCase))),
                                                      e => e.Name.Left(50),
                                                      m => m.Moderations,
                                                      typeSelector: f => f.ParentName.ToNullIfEmpty(),
                                                      dynPutItemAction: (a, l) =>
                                                                        {
                                                                            a.CountImageAnalyzed++;
                                                                            a.Moderations = l;
                                                                        },
                                                      mediaContentType: dynPublisherMedia.ContentType);

            var rawLabels = await ProcessFileExtra(imageFileMeta, "labels", () => _imageAnalysisService.GetImageLabelsAsync(imageFileMeta), request.Reanalyze);
            var labels = new List<Label>(rawLabels.Count);

            foreach (var rawLabel in rawLabels)
            {
                var label = await _labelTaxonomyProcessingFilter.LookupLabelAsync(rawLabel);

                if (label != null)
                {
                    labels.Add(label);
                }
            }

            var faces = await ProcessFileExtra(imageFileMeta, "faces", () => _imageAnalysisService.GetFacesAsync(imageFileMeta), request.Reanalyze);

            // Faces are a bit more complex, pre-calc as much as we can before storing
            var ageSum = 0L;
            var beardCount = 0;
            var mustacheCount = 0;
            var eyeglassCount = 0;
            var smileCount = 0;
            var sunglassCount = 0;
            var maleCount = 0;
            var femaleCount = 0;
            var facesCount = 0;

            var emotions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var face in faces.Where(f => f.Confidence >= _minFacesConfidence))
            {
                facesCount++;

                ageSum += face.AgeRange?.Low ?? 0;
                ageSum += face.AgeRange?.High ?? 0;

                beardCount += face?.Beard != null && face.Beard.Value && face.Beard.Confidence >= _minFacesConfidence
                                  ? 1
                                  : 0;

                eyeglassCount += face?.Eyeglasses != null && face.Eyeglasses.Value && face.Eyeglasses.Confidence >= _minFacesConfidence
                                     ? 1
                                     : 0;

                mustacheCount += face?.Mustache != null && face.Mustache.Value && face.Mustache.Confidence >= _minFacesConfidence
                                     ? 1
                                     : 0;

                smileCount += face?.Smile != null && face.Smile.Value && face.Smile.Confidence >= _minFacesConfidence
                                  ? 1
                                  : 0;

                sunglassCount += face?.Sunglasses != null && face.Sunglasses.Value && face.Sunglasses.Confidence >= _minFacesConfidence
                                     ? 1
                                     : 0;

                if (face?.Gender != null && face.Gender.Confidence >= _minFacesConfidence)
                {
                    maleCount += face.Gender.Value == GenderType.Male
                                     ? 1
                                     : 0;

                    femaleCount += face.Gender.Value == GenderType.Female
                                       ? 1
                                       : 0;
                }

                face.Emotions?.Where(e => e.Confidence >= _minFacesConfidence)
                    .Each(e =>
                          {
                              if (emotions.ContainsKey(e.Type.Value))
                              {
                                  emotions[e.Type.Value] += 1;
                              }
                              else
                              {
                                  emotions[e.Type.Value] = 1;
                              }
                          });
            }

            // Update the aggregate analysis for this publisher account
            await ProcessMediaAnalysisAggregatesAsync(request.PublisherAccountId,
                                                      labels,
                                                      async l => l.Confidence >= _minImageConfidence && !(await _labelTaxonomyProcessingFilter.ProcessIgnoreAsync(l)
                                                                                                         ),
                                                      l => l.Name,
                                                      a => a.ImageLabels,
                                                      typeSelector: f => f.Parents?.FirstOrDefault()?.Name.ToNullIfEmpty(),
                                                      dynPutItemAction: (a, l) =>
                                                                        {
                                                                            a.CountFacesAnalyzed += facesCount;

                                                                            a.ImageFacesAgeSum += ageSum;
                                                                            a.ImageFacesBeards += beardCount;
                                                                            a.ImageFacesEyeglasses += eyeglassCount;
                                                                            a.ImageFacesMustaches += mustacheCount;
                                                                            a.ImageFacesSmiles += smileCount;
                                                                            a.ImageFacesSunglasses += sunglassCount;
                                                                            a.ImageFacesMales += maleCount;
                                                                            a.ImageFacesFemales += femaleCount;

                                                                            if (a.ImageFacesEmotions.IsNullOrEmptyRydr())
                                                                            {
                                                                                a.ImageFacesEmotions = emotions;
                                                                            }
                                                                            else
                                                                            {
                                                                                emotions.Each(e => a.ImageFacesEmotions[e.Key] = a.ImageFacesEmotions.ContainsKey(e.Key)
                                                                                                                                     ? a.ImageFacesEmotions[e.Key] + e.Value
                                                                                                                                     : e.Value);
                                                                            }

                                                                            a.ImageLabels = l;
                                                                        },
                                                      mediaContentType: dynPublisherMedia.ContentType);

            // Update the analysis for this specific piece of media
            var publisherMediaAnalysisEdgeId = DynPublisherMediaAnalysis.BuildEdgeId(dynPublisherAccount.PublisherType, dynPublisherMedia.MediaId);

            var dynPublisherMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherMediaAnalysis>(request.PublisherMediaId, publisherMediaAnalysisEdgeId)
                                            ??
                                            new DynPublisherMediaAnalysis
                                            {
                                                Id = request.PublisherMediaId,
                                                EdgeId = publisherMediaAnalysisEdgeId,
                                                ReferenceId = (dynPublisherAccount?.PublisherAccountId).Gz(request.PublisherAccountId).ToStringInvariant(),
                                                TypeId = (int)DynItemType.PublisherMediaAnalysis
                                            };

            await _dynamoDb.PutItemTrackedInterlockedAsync(dynPublisherMediaAnalysis, dma =>
                                                                                      {
                                                                                          var publisherAccountId = (dynPublisherAccount?.PublisherAccountId).Gz(request.PublisherAccountId).Gz(dma.PublisherAccountId);

                                                                                          dma.ReferenceId = publisherAccountId > 0
                                                                                                                ? publisherAccountId.ToStringInvariant()
                                                                                                                : null;

                                                                                          dma.Moderations = moderations?.Where(m => m.Name.HasValue() &&
                                                                                                                                    m.Confidence > _minModerationConfidence &&
                                                                                                                                    (m.ParentName.IsNullOrEmpty() ||
                                                                                                                                     m.Confidence > _minModerationViolenceConfidence) ||
                                                                                                                                    !m.ParentName.ContainsAny(PublisherMediaAnalysisService.ViolenceCategories, StringComparison.OrdinalIgnoreCase))
                                                                                                                       .Select(m => new ValueWithConfidence
                                                                                                                                    {
                                                                                                                                        Value = m.Name,
                                                                                                                                        ParentValue = m.ParentName.ToNullIfEmpty(),
                                                                                                                                        Confidence = m.Confidence,
                                                                                                                                        Occurrences = 1
                                                                                                                                    })
                                                                                                                       .AsList();

                                                                                          dma.ImageLabels = labels?.Where(m => m.Confidence > _minImageConfidence && m.Name.HasValue())
                                                                                                                  .Select(m => new ValueWithConfidence
                                                                                                                               {
                                                                                                                                   Value = m.Name,
                                                                                                                                   ParentValue = (m.Parents?.FirstOrDefault()?.Name).ToNullIfEmpty(),
                                                                                                                                   Confidence = m.Confidence,
                                                                                                                                   Occurrences = m.Instances?.Where(i => i.Confidence > _minImageConfidence).Count() ?? 0
                                                                                                                               })
                                                                                                                  .AsList();

                                                                                          dma.ImageFacesEmotions = emotions;

                                                                                          dma.ImageFacesAgeSum = ageSum;
                                                                                          dma.ImageFacesCount = facesCount;
                                                                                          dma.ImageFacesBeards = beardCount;
                                                                                          dma.ImageFacesMustaches = mustacheCount;
                                                                                          dma.ImageFacesEyeglasses = eyeglassCount;
                                                                                          dma.ImageFacesSmiles = smileCount;
                                                                                          dma.ImageFacesSunglasses = sunglassCount;
                                                                                          dma.ImageFacesFemales = femaleCount;
                                                                                          dma.ImageFacesMales = maleCount;
                                                                                      });

            _deferRequestsService.DeferLowPriRequest(new PublisherMediaAnalysisUpdated
                                                     {
                                                         PublisherMediaId = dynPublisherMediaAnalysis.PublisherMediaId,
                                                         PublisherMediaAnalysisEdgeId = dynPublisherMediaAnalysis.EdgeId
                                                     });
        }

        public async Task Post(PostAnalyzePublisherText request)
        {
            var textFileMeta = new FileMetaData(request.Path);

            var fileData = await _fileStorageProvider.GetAsync(textFileMeta);
            var text = Encoding.UTF8.GetString(fileData);

            if (!text.HasValue())
            {
                _log.WarnFormat("Text analysis for PublisherAccountId [{0}], path [{1}] resulted in empty text, exiting attempt.", request.PublisherAccountId, textFileMeta.FullName);

                return;
            }

            //            if (!(await _textAnalysisService.GetDominantLanguageCodeAsync(text)).EqualsOrdinalCi("en"))
            //            {
            //                _log.WarnFormat("Text analysis for PublisherAccountId [{0}], path [{1}] resulted in non-english dominant text, exiting attempt.", request.PublisherAccountId, textFileMeta.FullName);
            //
            //                return;
            //            }

            var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

            var dynPublisherMedia = await _dynamoDb.GetItemAsync<DynPublisherMedia>(request.PublisherAccountId, request.PublisherMediaId.ToEdgeId());
            var entities = await ProcessFileExtra(textFileMeta, "entities", () => _textAnalysisService.GetEntitiesAsync(text), request.Reanalyze);
            var sentiment = await ProcessFileExtra(textFileMeta, "sentiment", () => _textAnalysisService.GetSentimentAsync(text), request.Reanalyze);

            // Set all the values to add/increment within the analysis object - do this ahead of time simply to decrease the amount of time the
            // dynamo object is left in potential race condition. We don't really care much about losing some here and there, but we just want
            // to do as much as we can to avoid it without overdoing it
            var positiveOccurrence = (sentiment?.Sentiment).EqualsOrdinalCi(SentimentType.POSITIVE.Value)
                                         ? 1
                                         : 0;

            var positiveSentiment = sentiment?.PositiveSentiment ?? 0;

            var mixedOccurrence = (sentiment?.Sentiment).EqualsOrdinalCi(SentimentType.MIXED.Value)
                                      ? 1
                                      : 0;

            var mixedSentiment = sentiment?.MixedSentiment ?? 0;

            var neutralOccurrence = (sentiment?.Sentiment).EqualsOrdinalCi(SentimentType.NEUTRAL.Value)
                                        ? 1
                                        : 0;

            var neutralSentiment = sentiment?.NeutralSentiment ?? 0;

            var negativeOccurrence = (sentiment?.Sentiment).EqualsOrdinalCi(SentimentType.NEGATIVE.Value)
                                         ? 1
                                         : 0;

            var negativeSentiment = sentiment?.NegativeSentiment ?? 0;

            // Update aggregate info for this publisher account
            await ProcessMediaAnalysisAggregatesAsync(request.PublisherAccountId,
                                                      entities,
                                                      e => Task.FromResult((e.Score * 100) > _minTextConfidence &&
                                                                           !_textEntitiesToIgnore.Contains(e.Type.Value)),
                                                      e => e.Text.Left(50),
                                                      m => m.PopularEntities,
                                                      (a, l) =>
                                                      {
                                                          a.CountTextAnalyzed++;

                                                          a.PositiveSentimentOccurrences += positiveOccurrence;
                                                          a.PositiveSentimentTotal += positiveSentiment;

                                                          a.MixedSentimentOccurrences += mixedOccurrence;
                                                          a.MixedSentimentTotal += mixedSentiment;

                                                          a.NeutralSentimenOccurrences += neutralOccurrence;
                                                          a.NeutralSentimentTotal += neutralSentiment;

                                                          a.NegativeSentimentOccurrences += negativeOccurrence;
                                                          a.NegativeSentimentTotal += negativeSentiment;

                                                          a.PopularEntities = l;
                                                      },
                                                      dynPublisherMedia.ContentType,
                                                      f => f.Type?.Value.Left(25));

            // Update the analysis for this specific piece of media
            var publisherMediaAnalysisEdgeId = DynPublisherMediaAnalysis.BuildEdgeId(dynPublisherAccount.PublisherType, dynPublisherMedia.MediaId);

            var dynPublisherMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherMediaAnalysis>(request.PublisherMediaId, publisherMediaAnalysisEdgeId)
                                            ??
                                            new DynPublisherMediaAnalysis
                                            {
                                                Id = request.PublisherMediaId,
                                                EdgeId = publisherMediaAnalysisEdgeId,
                                                ReferenceId = (dynPublisherAccount?.PublisherAccountId).Gz(request.PublisherAccountId).ToStringInvariant(),
                                                TypeId = (int)DynItemType.PublisherMediaAnalysis
                                            };

            await _dynamoDb.PutItemTrackedInterlockedAsync(dynPublisherMediaAnalysis, dma =>
                                                                                      {
                                                                                          var publisherAccountId = (dynPublisherAccount?.PublisherAccountId).Gz(request.PublisherAccountId).Gz(dma.PublisherAccountId);

                                                                                          dma.ReferenceId = publisherAccountId > 0
                                                                                                                ? publisherAccountId.ToStringInvariant()
                                                                                                                : null;

                                                                                          dma.PopularEntities = entities?.Where(e => (e.Score * 100) > _minTextConfidence &&
                                                                                                                                     !_textEntitiesToIgnore.Contains(e.Type.Value))
                                                                                                                        .GroupBy(e => e.Text)
                                                                                                                        .Select(g => new ValueWithConfidence
                                                                                                                                     {
                                                                                                                                         Value = g.Key,
                                                                                                                                         ParentValue = g.FirstOrDefault(t => t?.Type?.Value != null)?.Type?.Value,
                                                                                                                                         Confidence = g.Max(e => e.Score) * 100,
                                                                                                                                         Occurrences = g.Count()
                                                                                                                                     })
                                                                                                                        .AsList();

                                                                                          dma.Sentiment = sentiment.Sentiment;
                                                                                          dma.MixedSentiment = sentiment.MixedSentiment;
                                                                                          dma.PositiveSentiment = sentiment.PositiveSentiment;
                                                                                          dma.NegativeSentiment = sentiment.NegativeSentiment;
                                                                                          dma.NeutralSentiment = sentiment.NeutralSentiment;
                                                                                      });

            _deferRequestsService.DeferLowPriRequest(new PublisherMediaAnalysisUpdated
                                                     {
                                                         PublisherMediaId = dynPublisherMediaAnalysis.PublisherMediaId,
                                                         PublisherMediaAnalysisEdgeId = dynPublisherMediaAnalysis.EdgeId
                                                     });
        }

        public async Task Post(PublisherMediaAnalysisUpdated request)
        {
            var dynPublisherMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherMediaAnalysis>(request.PublisherMediaId, request.PublisherMediaAnalysisEdgeId);

            if (dynPublisherMediaAnalysis == null)
            {
                return;
            }

            var dynPublisherMedia = await _dynamoDb.GetItemAsync<DynPublisherMedia>(dynPublisherMediaAnalysis.PublisherAccountId, dynPublisherMediaAnalysis.PublisherMediaId.ToEdgeId());

            if (dynPublisherMedia == null || dynPublisherMedia.IsDeleted() ||
                dynPublisherMediaAnalysis == null || dynPublisherMediaAnalysis.IsDeleted())
            {
                await _elasticClient.DeleteAsync(EsMedia.GetDocumentPath(request.PublisherMediaId),
                                                 d => d.Index(ElasticIndexes.MediaAlias));

                return;
            }

            var esMedia = dynPublisherMedia.ToEsMedia(dynPublisherMediaAnalysis);

            var response = await _elasticClient.IndexAsync(esMedia, i => i.Index(ElasticIndexes.MediaAlias)
                                                                          .Id(esMedia.PublisherMediaId));

            response.Successful();

            if (!dynPublisherMedia.IsAnalyzed)
            {
                dynPublisherMedia.IsAnalyzed = true;
                dynPublisherMedia.IsRydrHosted = true;

                await _dynamoDb.PutItemTrackedAsync(dynPublisherMedia);
            }
        }

        private async Task<T> ProcessFileExtra<T>(FileMetaData sourceFileMeta, string suffix, Func<Task<T>> getter, bool reanalyze)
            where T : class
        {
            var extraHumanMeta = new FileMetaData(sourceFileMeta.FolderName, string.Concat(sourceFileMeta.FileName, "_", suffix, "_human", ".json"));

            var humanExtras = (await _fileStorageProvider.ExistsAsync(extraHumanMeta))
                                  ? Encoding.UTF8.GetString(await _fileStorageProvider.GetAsync(extraHumanMeta)).FromJson<T>()
                                  : null;

            if (humanExtras != null)
            {
                return humanExtras;
            }

            var extraMeta = new FileMetaData(sourceFileMeta.FolderName, string.Concat(sourceFileMeta.FileName, "_", suffix, ".json"));
            var haveExtraMeta = await _fileStorageProvider.ExistsAsync(extraMeta);

            var extras = haveExtraMeta && !reanalyze
                             ? Encoding.UTF8.GetString(await _fileStorageProvider.GetAsync(extraMeta)).FromJson<T>()
                             : await getter();

            if ((reanalyze || !haveExtraMeta) && extras != null)
            { // Save them to storage
                var json = extras.ToJson();

                if (json.IsNullOrEmpty() || json.Length <= 5)
                {
                    return extras;
                }

                extraMeta.Bytes = Encoding.UTF8.GetBytes(json);

                extraMeta.Tags.Add(FileStorageTag.Lifecycle.ToString(), FileStorageTags.LifecyclePurge);
                extraMeta.Tags.Add(FileStorageTag.Privacy.ToString(), FileStorageTags.PrivacyPrivate);

                await _fileStorageProvider.StoreAsync(extraMeta, new FileStorageOptions
                                                                 {
                                                                     ContentType = "application/json",
                                                                     Encrypt = true,
                                                                     StorageClass = FileStorageClass.Intelligent
                                                                 });
            }

            return extras;
        }

        private async Task ProcessMediaAnalysisAggregatesAsync<T>(long publisherAccountId, IReadOnlyList<T> values, Func<T, Task<bool>> predicate, Func<T, string> grouping,
                                                                  Func<DynPublisherAccountMediaAnalysis, List<MediaAnalysisEntity>> existingListSelector,
                                                                  Action<DynPublisherAccountMediaAnalysis, List<MediaAnalysisEntity>> dynPutItemAction,
                                                                  PublisherContentType mediaContentType, Func<T, string> typeSelector = null)
        {
            var mediaAnalysisMap = new Dictionary<string, MediaAnalysisEntity>(StringComparer.OrdinalIgnoreCase);

            foreach (var value in values)
            {
                if (!(await predicate(value)))
                {
                    continue;
                }

                var groupKey = grouping == null
                                   ? Guid.NewGuid().ToStringId()
                                   : grouping(value);

                if (mediaAnalysisMap.ContainsKey(groupKey))
                {
                    mediaAnalysisMap[groupKey].Occurrences += 1;
                }
                else
                {
                    mediaAnalysisMap[groupKey] = new MediaAnalysisEntity
                                                 {
                                                     EntityText = groupKey,
                                                     EntityType = value == null || typeSelector == null
                                                                      ? null
                                                                      : typeSelector(value).ToNullIfEmpty(),
                                                     Occurrences = 1
                                                 };
                }
            }

            var accountEdgeId = string.Concat(DynItemType.PublisherMediaAnalysis.ToString(), "|agganalysis");
            var mediaTypeEdgeId = string.Concat(accountEdgeId, "|", mediaContentType.ToString());

            // Aggregate analysis for the account and the type levels...
            await ProcessMediaAnalysisAggregateEdgeAsync(mediaAnalysisMap.Values, publisherAccountId, accountEdgeId, existingListSelector, dynPutItemAction);
            await ProcessMediaAnalysisAggregateEdgeAsync(mediaAnalysisMap.Values, publisherAccountId, mediaTypeEdgeId, existingListSelector, dynPutItemAction);
        }

        private async Task ProcessMediaAnalysisAggregateEdgeAsync(ICollection<MediaAnalysisEntity> values, long publisherAccountId, string analysisEdgeId,
                                                                  Func<DynPublisherAccountMediaAnalysis, List<MediaAnalysisEntity>> existingListSelector,
                                                                  Action<DynPublisherAccountMediaAnalysis, List<MediaAnalysisEntity>> dynPutItemAction)
        {
            List<MediaAnalysisEntity> newValues = null;

            var dynMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherAccountMediaAnalysis>(publisherAccountId, analysisEdgeId)
                                   ??
                                   new DynPublisherAccountMediaAnalysis
                                   {
                                       Id = publisherAccountId,
                                       EdgeId = analysisEdgeId,
                                       TypeId = (int)DynItemType.PublisherMediaAnalysis,
                                       PopularEntities = new List<MediaAnalysisEntity>(),
                                       Moderations = new List<MediaAnalysisEntity>()
                                   };

            var existingValues = existingListSelector?.Invoke(dynMediaAnalysis);

            if (existingValues.IsNullOrEmpty())
            {
                newValues = values?.OrderByDescending(e => e.Occurrences)
                                  .Take(50)
                                  .AsList();
            }
            else
            {
                newValues = (values ?? Enumerable.Empty<MediaAnalysisEntity>()).Concat(existingValues ?? Enumerable.Empty<MediaAnalysisEntity>())
                                                                               .GroupBy(e => e.EntityText.Left(50))
                                                                               .Select(g => new MediaAnalysisEntity
                                                                                            {
                                                                                                EntityText = g.Key,
                                                                                                EntityType = g.First().EntityType.Left(50).ToNullIfEmpty(),
                                                                                                Occurrences = g.Sum(gi => gi.Occurrences)
                                                                                            })
                                                                               .OrderByDescending(e => e.Occurrences)
                                                                               .Take(50)
                                                                               .AsList();
            }

            await _dynamoDb.PutItemTrackedInterlockedAsync(dynMediaAnalysis, a => dynPutItemAction(a, newValues));
        }
    }
}
