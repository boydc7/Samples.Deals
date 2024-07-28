using Amazon.Comprehend;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Services.Services;

public class PublisherMediaAnalysisService : BaseApiService
{
    public static readonly string[] ViolenceCategories =
    {
        "violence", "disturbing"
    };

    private static readonly int _maxDailyStories = RydrEnvironment.GetAppSetting("PublisherAnalysis.MaxStoriesDaily", 5);
    private static readonly int _maxDailyPosts = RydrEnvironment.GetAppSetting("PublisherAnalysis.MaxPostsDaily", 1);

    private static readonly string[] _suggestiveCategories =
    {
        "nudity", "suggestive"
    };

    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IElasticSearchService _elasticSearchService;

    public PublisherMediaAnalysisService(IPublisherAccountService publisherAccountService, IElasticSearchService elasticSearchService)
    {
        _publisherAccountService = publisherAccountService;
        _elasticSearchService = elasticSearchService;
    }

    [RydrForcedSimpleCacheResponse(7000)] // Just under 2 hours
    public async Task<OnlyResultResponse<PublisherAccountMediaVision>> Get(GetPublisherAccountMediaVision request)
    {
        const int recentMediasToReturn = 20;

        var publisherAccountId = request.GetPublisherIdFromIdentifier();

        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

        var response = new PublisherAccountMediaVision
                       {
                           PostDailyLimit = _maxDailyPosts,
                           StoryDailyLimit = _maxDailyStories,
                           RecentPosts = new List<PublisherMedia>(recentMediasToReturn),
                           RecentStories = new List<PublisherMedia>(recentMediasToReturn)
                       };

        var inspectedMediaCount = 0;
        var todayTimestamp = _dateTimeProvider.UtcNow.Date.ToUnixTimestamp();

        foreach (var dynPublisherMedia in _dynamoDb.FromQuery<DynPublisherMedia>(pm => pm.Id == publisherAccountId &&
                                                                                       Dynamo.BeginsWith(pm.EdgeId, "00"))
                                                   .Filter(pm => pm.DeletedOnUtc == null &&
                                                                 pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                 pm.PublisherType == publisherAccount.PublisherType &&
                                                                 Dynamo.In(pm.ContentType, PublisherMediaService.AllPublisherContentTypeEnums))
                                                   .Exec())
        {
            inspectedMediaCount++;

            var isTodayMedia = dynPublisherMedia.MediaCreatedAt >= todayTimestamp;

            switch (dynPublisherMedia.ContentType)
            {
                case PublisherContentType.Story:
                    if (isTodayMedia)
                    {
                        response.TodayStoriesAnalyzed++;
                    }

                    if (dynPublisherMedia.IsAnalyzed && response.RecentStories.Count < recentMediasToReturn)
                    {
                        var story = await dynPublisherMedia.ToPublisherMediaAsync();

                        response.RecentStories.Add(story);
                    }

                    break;

                case PublisherContentType.Post:
                    if (isTodayMedia)
                    {
                        response.TodayPostsAnalyzed++;
                    }

                    if (dynPublisherMedia.IsAnalyzed && response.RecentPosts.Count < recentMediasToReturn)
                    {
                        var post = await dynPublisherMedia.ToPublisherMediaAsync();

                        response.RecentPosts.Add(post);
                    }

                    break;
            }

            // Stop once we know we've exceeded the limits
            if (inspectedMediaCount >= 300 ||
                (response.TodayPostsAnalyzed > _maxDailyPosts &&
                 response.TodayStoriesAnalyzed > _maxDailyStories &&
                 response.RecentPosts.Count >= recentMediasToReturn &&
                 response.RecentStories.Count >= recentMediasToReturn))
            {
                break;
            }
        }

        var mediaAnalysis = await DoGetPublisherAccountMediaAnalysisAsync(publisherAccount.PublisherAccountId);

        if (mediaAnalysis?.AccountAnalysis == null)
        {
            return response.AsOnlyResultResponse();
        }

        response.TotalPostsAnalyzed = mediaAnalysis.AccountAnalysis.PostsAnalyzed;
        response.TotalStoriesAnalyzed = mediaAnalysis.AccountAnalysis.StoriesAnalyzed;

        response.Notable = new PublisherAccountMediaVisionSection
                           {
                               Title = "Notable",
                               Items = new List<PublisherAccountMediaVisionSectionItem>
                                       {
                                           await GetVisionSectionItemAsync("Revealing", "Swimwear or nudity", mediaAnalysis,
                                                                           a => a.AccountAnalysis
                                                                                 .ImageModerations?
                                                                                 .Sum(m => m.ParentValue.HasValue() && m.ParentValue.ContainsAny(_suggestiveCategories, StringComparison.OrdinalIgnoreCase)
                                                                                               ? m.Occurrences
                                                                                               : 0) ?? 0,
                                                                           a => a.AccountAnalysis
                                                                                 .ImageModerations?
                                                                                 .Where(m => m.Value.ContainsAny(_suggestiveCategories, StringComparison.OrdinalIgnoreCase) ||
                                                                                             (m.ParentValue.HasValue() && m.ParentValue.ContainsAny(_suggestiveCategories, StringComparison.OrdinalIgnoreCase)))
                                                                                 .Select(m => m.Value)
                                                                                 .Distinct(StringComparer.OrdinalIgnoreCase),
                                                                           new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                           {
                                                                               PublisherAccountId = publisherAccountId,
                                                                               Query = "nudity|suggestive|swimwear|sexual",
                                                                               Take = 4
                                                                           }),
                                           await GetVisionSectionItemAsync("Violence", "Weapons or violence", mediaAnalysis,
                                                                           a => a.AccountAnalysis
                                                                                 .ImageModerations?
                                                                                 .Sum(m => m.ParentValue.HasValue() && m.ParentValue.ContainsAny(ViolenceCategories, StringComparison.OrdinalIgnoreCase)
                                                                                               ? m.Occurrences
                                                                                               : 0) ?? 0,
                                                                           a => a.AccountAnalysis
                                                                                 .ImageModerations?
                                                                                 .Where(m => m.Value.ContainsAny(ViolenceCategories, StringComparison.OrdinalIgnoreCase) ||
                                                                                             (m.ParentValue.HasValue() && m.ParentValue.ContainsAny(ViolenceCategories, StringComparison.OrdinalIgnoreCase)))
                                                                                 .Select(m => m.Value)
                                                                                 .Distinct(StringComparer.OrdinalIgnoreCase),
                                                                           new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                           {
                                                                               PublisherAccountId = publisherAccountId,
                                                                               Query = "violence|disturbing|weapon|weapons",
                                                                               Take = 4
                                                                           }),
                                           await GetVisionSectionItemAsync("Places", "Travel or locations", mediaAnalysis,
                                                                           a => a.AccountAnalysis
                                                                                 .TextEntities?
                                                                                 .Sum(m => m.ParentValue.EqualsOrdinalCi(EntityType.LOCATION)
                                                                                               ? m.Occurrences
                                                                                               : 0) ?? 0,
                                                                           a => a.AccountAnalysis
                                                                                 .TextEntities?
                                                                                 .Where(e => e.ParentValue.EqualsOrdinalCi(EntityType.LOCATION))
                                                                                 .Select(e => e.Value)
                                                                                 .Distinct(StringComparer.OrdinalIgnoreCase),
                                                                           new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                           {
                                                                               PublisherAccountId = publisherAccountId,
                                                                               Query = EntityType.LOCATION.Value.ToLowerInvariant(),
                                                                               Take = 4
                                                                           })
                                       }
                           };

        response.Notable.TotalCount = response.Notable.Items.Sum(i => i.Count);

        if (mediaAnalysis.StoryAnalysis != null)
        {
            response.Stories = new PublisherAccountMediaVisionSection
                               {
                                   Title = "Instagram Stories",
                                   TotalCount = mediaAnalysis.StoryAnalysis.StoriesAnalyzed,
                                   Items = new List<PublisherAccountMediaVisionSectionItem>
                                           {
                                               await GetVisionSectionItemAsync("People", "People or organizations", mediaAnalysis,
                                                                               a => a.StoryAnalysis
                                                                                     .TextEntities?
                                                                                     .Sum(m => m.ParentValue.EqualsOrdinalCi(EntityType.PERSON) || m.ParentValue.EqualsOrdinalCi(EntityType.ORGANIZATION)
                                                                                                   ? m.Occurrences
                                                                                                   : 0) ?? 0,
                                                                               a => a.StoryAnalysis
                                                                                     .TextEntities?
                                                                                     .Where(e => e.ParentValue.EqualsOrdinalCi(EntityType.PERSON) || e.ParentValue.EqualsOrdinalCi(EntityType.ORGANIZATION))
                                                                                     .Select(e => e.Value)
                                                                                     .Distinct(StringComparer.OrdinalIgnoreCase),
                                                                               new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                               {
                                                                                   PublisherAccountId = publisherAccountId,
                                                                                   Query = string.Concat(EntityType.PERSON.Value.ToLowerInvariant(), "|", EntityType.ORGANIZATION.Value.ToLowerInvariant()),
                                                                                   Take = 4
                                                                               })
                                           }
                               };

            if (!mediaAnalysis.StoryAnalysis.ImageLabels.IsNullOrEmpty())
            {
                foreach (var storyLabel in mediaAnalysis.StoryAnalysis
                                                        .ImageLabels
                                                        .Where(l => l.Occurrences > 2))
                {
                    var title = storyLabel.ParentValue.Coalesce(storyLabel.Value);

                    if (response.Stories.Items.Any(i => i.Title.EqualsOrdinalCi(title)))
                    {
                        continue;
                    }

                    var subCategories = mediaAnalysis.StoryAnalysis
                                                     .ImageLabels
                                                     .Where(l => l.ParentValue.EqualsOrdinalCi(title))
                                                     .AsListReadOnly();

                    var subTitle = string.Join(',', subCategories.Select(l => l.Value)).ToNullIfEmpty();

                    var sectionItem = await GetVisionSectionItemAsync(title, subTitle, mediaAnalysis,
                                                                      a => storyLabel.Occurrences,
                                                                      a => subCategories.Select(c => c.Value),
                                                                      new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                      {
                                                                          PublisherAccountId = publisherAccountId,
                                                                          Query = title.ToLowerInvariant(),
                                                                          Take = 4
                                                                      });

                    response.Stories.Items.Add(sectionItem);

                    if (response.Stories.Items.Count >= 10)
                    {
                        break;
                    }
                }
            }

            if (response.Stories.TotalCount <= 0)
            {
                response.Stories.TotalCount = response.Stories.Items.Sum(i => i.Count);
            }
        }

        if (mediaAnalysis.PostAnalysis != null)
        {
            response.Posts = new PublisherAccountMediaVisionSection
                             {
                                 Title = "Instagram Posts",
                                 TotalCount = mediaAnalysis.PostAnalysis.PostsAnalyzed,
                                 Items = new List<PublisherAccountMediaVisionSectionItem>
                                         {
                                             await GetVisionSectionItemAsync("People", "People or organizations", mediaAnalysis,
                                                                             a => a.PostAnalysis
                                                                                   .TextEntities?
                                                                                   .Sum(m => m.ParentValue.EqualsOrdinalCi(EntityType.PERSON) || m.ParentValue.EqualsOrdinalCi(EntityType.ORGANIZATION)
                                                                                                 ? m.Occurrences
                                                                                                 : 0) ?? 0,
                                                                             a => a.PostAnalysis
                                                                                   .TextEntities?
                                                                                   .Where(e => e.ParentValue.EqualsOrdinalCi(EntityType.PERSON) || e.ParentValue.EqualsOrdinalCi(EntityType.ORGANIZATION))
                                                                                   .Select(e => e.Value)
                                                                                   .Distinct(StringComparer.OrdinalIgnoreCase),
                                                                             new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                             {
                                                                                 PublisherAccountId = publisherAccountId,
                                                                                 Query = string.Concat(EntityType.PERSON.Value.ToLowerInvariant(), "|", EntityType.ORGANIZATION.Value.ToLowerInvariant()),
                                                                                 Take = 4
                                                                             })
                                         }
                             };

            if (!mediaAnalysis.PostAnalysis.ImageLabels.IsNullOrEmpty())
            {
                foreach (var postLabel in mediaAnalysis.PostAnalysis
                                                       .ImageLabels
                                                       .Where(l => l.Occurrences > 2))
                {
                    var title = postLabel.ParentValue.Coalesce(postLabel.Value);

                    if (response.Posts.Items.Any(i => i.Title.EqualsOrdinalCi(title)))
                    {
                        continue;
                    }

                    var subCategories = mediaAnalysis.PostAnalysis
                                                     .ImageLabels
                                                     .Where(l => l.ParentValue.EqualsOrdinalCi(title))
                                                     .AsListReadOnly();

                    var subTitle = string.Join(',', subCategories.Select(l => l.Value)).ToNullIfEmpty();

                    var sectionItem = await GetVisionSectionItemAsync(title, subTitle, mediaAnalysis,
                                                                      a => postLabel.Occurrences,
                                                                      a => subCategories.Select(c => c.Value),
                                                                      new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                      {
                                                                          PublisherAccountId = publisherAccountId,
                                                                          Query = title.ToLowerInvariant(),
                                                                          Take = 4
                                                                      });

                    response.Posts.Items.Add(sectionItem);

                    if (response.Posts.Items.Count >= 10)
                    {
                        break;
                    }
                }
            }

            if (response.Posts.TotalCount <= 0)
            {
                response.Posts.TotalCount = response.Posts.Items.Sum(i => i.Count);
            }
        }

        response.Captions = new PublisherAccountMediaVisionSection
                            {
                                Title = "Captions",
                                TotalCount = mediaAnalysis.AccountAnalysis.TextCount,
                                Items = new List<PublisherAccountMediaVisionSectionItem>
                                        {
                                            await GetVisionSectionItemAsync("Positive", "Positive sentiment or happiness", mediaAnalysis,
                                                                            a => a.AccountAnalysis
                                                                                  .ImageFacesSmiles
                                                                                  .Greatest(a.AccountAnalysis.ImageFacesEmotions?.GetValueOrDefault("HAPPY") ?? 0)
                                                                                  .Greatest((long)(a.AccountAnalysis.TotalSentimentOccurrences * (a.AccountAnalysis.TextPositiveSentimentPercentage / 100))),
                                                                            a => new[]
                                                                                 {
                                                                                     "happy", "smile", "positive"
                                                                                 },
                                                                            new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                            {
                                                                                PublisherAccountId = publisherAccountId,
                                                                                Query = "happy|smile|positive",
                                                                                Take = 4
                                                                            }),
                                            await GetVisionSectionItemAsync("Negative", "Negative sentiment or angry/sad", mediaAnalysis,
                                                                            a => ((a.AccountAnalysis.ImageFacesEmotions?.GetValueOrDefault("SAD") ?? 0)
                                                                                  +
                                                                                  (a.AccountAnalysis.ImageFacesEmotions?.GetValueOrDefault("ANGRY") ?? 0)).Greatest((long)(a.AccountAnalysis.TotalSentimentOccurrences * (a.AccountAnalysis.TextNegativeSentimentPercentage / 100))),
                                                                            a => new[]
                                                                                 {
                                                                                     "sad", "angry", "negative", "disgusted"
                                                                                 },
                                                                            new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                            {
                                                                                PublisherAccountId = publisherAccountId,
                                                                                Query = "negative|sad|angry|disgusted",
                                                                                Take = 4
                                                                            }),
                                            await GetVisionSectionItemAsync("Neutral", "Neutral sentiment or calm", mediaAnalysis,
                                                                            a => (a.AccountAnalysis.ImageFacesEmotions?
                                                                                      .GetValueOrDefault("CALM") ?? 0).Greatest((long)(a.AccountAnalysis.TotalSentimentOccurrences * (a.AccountAnalysis.TextNeutralSentimentPercentage / 100))),
                                                                            a => new[]
                                                                                 {
                                                                                     "neutral", "calm"
                                                                                 },
                                                                            new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                            {
                                                                                PublisherAccountId = publisherAccountId,
                                                                                Query = "neutral|calm",
                                                                                Take = 4
                                                                            }),
                                            await GetVisionSectionItemAsync("Mixed", "Mixed sentiment or confusion", mediaAnalysis,
                                                                            a => (a.AccountAnalysis.ImageFacesEmotions?
                                                                                      .GetValueOrDefault("CONFUSED") ?? 0).Greatest((long)(a.AccountAnalysis.TotalSentimentOccurrences * (a.AccountAnalysis.TextMixedSentimentPercentage / 100))),
                                                                            a => new[]
                                                                                 {
                                                                                     "mixed", "confused"
                                                                                 },
                                                                            new PublisherAccountMediaVisionSectionSearchDescriptor
                                                                            {
                                                                                PublisherAccountId = publisherAccountId,
                                                                                Query = "mixed|confused",
                                                                                Take = 4
                                                                            })
                                        }
                            };

        if (response.TotalPostsAnalyzed <= 0 && (response.RecentPosts.Count > 0 || (response.Posts?.TotalCount ?? 0) > 0))
        {
            response.TotalPostsAnalyzed = response.RecentPosts.Count.Gz((int)(response.Posts?.TotalCount ?? 0));
        }

        if (response.TotalStoriesAnalyzed <= 0 && (response.RecentStories.Count > 0 || (response.Stories?.TotalCount ?? 0) > 0))
        {
            response.TotalStoriesAnalyzed = response.RecentStories.Count.Gz((int)(response.Stories?.TotalCount ?? 0));
        }

        return response.AsOnlyResultResponse();
    }

    public async Task<OnlyResultsResponse<PublisherMedia>> Get(GetPublisherAccountMediaSearch request)
    {
        var searchDescriptor = request.ConvertTo<PublisherAccountMediaVisionSectionSearchDescriptor>();

        searchDescriptor.PublisherAccountId = request.GetPublisherIdFromIdentifier();

        var publisherMedias = await DoGetPublisherAccountMediaSearch(searchDescriptor);

        return publisherMedias.AsOnlyResultsResponse();
    }

    [RydrForcedSimpleCacheResponse(7000)]
    public async Task<OnlyResultResponse<PublisherAccountMediaAnalysis>> Get(GetPublisherAccountMediaAnalysis request)
    {
        var response = await DoGetPublisherAccountMediaAnalysisAsync(request.GetPublisherIdFromIdentifier());

        return (response ?? new PublisherAccountMediaAnalysis()).AsOnlyResultResponse();
    }

    private async Task<PublisherAccountMediaVisionSectionItem> GetVisionSectionItemAsync(string title, string subTitle,
                                                                                         PublisherAccountMediaAnalysis mediaAnalysis,
                                                                                         Func<PublisherAccountMediaAnalysis, long> countGetter,
                                                                                         Func<PublisherAccountMediaAnalysis, IEnumerable<string>> searchTagGetter,
                                                                                         PublisherAccountMediaVisionSectionSearchDescriptor searchDescriptor)
    {
        var newSectionItem = new PublisherAccountMediaVisionSectionItem
                             {
                                 Title = title,
                                 SubTitle = subTitle,
                                 Count = countGetter(mediaAnalysis),
                                 SearchDescriptor = searchDescriptor,
                                 SearchTags = searchTagGetter(mediaAnalysis).AsListReadOnly()
                             };

        if (newSectionItem.Count <= 0)
        {
            return newSectionItem;
        }

        newSectionItem.Medias = (await DoGetPublisherAccountMediaSearch(newSectionItem.SearchDescriptor))?.Select(m => m.CreateCopy<PublisherMediaInfo>()).AsListReadOnly();

        if (!newSectionItem.Medias.IsNullOrEmptyReadOnly())
        {
            return newSectionItem;
        }

        _log.WarnFormat("PublisherAccountMediaVision Item {0}.{1} had count > 0, but search returned no matching medias - count was [{2}].",
                        newSectionItem.Title.Coalesce("NoTitle"), newSectionItem.SubTitle.Coalesce("NoSubtitle"), newSectionItem.Count);

        newSectionItem.Count = 0;
        newSectionItem.Medias = null;
        newSectionItem.SearchTags = null;

        return newSectionItem;
    }

    private async Task<IReadOnlyList<PublisherMedia>> DoGetPublisherAccountMediaSearch(PublisherAccountMediaVisionSectionSearchDescriptor searchDescriptor)
    {
        var take = searchDescriptor.Take;

        // In small search cases, take more search results so we get some spares to lookup to dynamo against
        searchDescriptor.Take = (int)(take * 5).MinGz(250);

        var searchedMediaIds = await _elasticSearchService.SearchMediaAsync(searchDescriptor);

        searchDescriptor.Take = take;

        if (searchedMediaIds == null || searchedMediaIds.TotalHits <= 0)
        {
            return new List<PublisherMedia>();
        }

        var searchResults = searchedMediaIds.Results.Select(m => m.PublisherMediaId);

        // Optimization for different orderings...if ordering by recency, we can just get stuff and return
        // If not, we need to keep the relevance order of things returned by search
        if (searchDescriptor.SortRecent)
        {
            var publisherMedias = await _dynamoDb.QueryItemsAsync<DynPublisherMedia>(searchResults.Select(t => new DynamoId(searchDescriptor.PublisherAccountId, t.ToEdgeId())))
                                                 .SelectAwait(m => m.ToPublisherMediaAsyncValue())
                                                 .Take(take)
                                                 .OrderByDescending(m => m.CreatedAt)
                                                 .ThenByDescending(m => m.Id)
                                                 .ToList(take);

            return publisherMedias;
        }

        var searchMaterialized = searchResults.AsListReadOnly();

        var randomPublisherMedias = await _dynamoDb.QueryItemsAsync<DynPublisherMedia>(searchMaterialized.Select(t => new DynamoId(searchDescriptor.PublisherAccountId, t.ToEdgeId())))
                                                   .ToDictionarySafe(m => m.PublisherMediaId);

        var results = new List<PublisherMedia>(take);

        foreach (var searchedId in searchMaterialized.Where(sm => randomPublisherMedias.ContainsKey(sm))
                                                     .Take(take))
        {
            var publisherMedia = await randomPublisherMedias[searchedId].ToPublisherMediaAsync();

            results.Add(publisherMedia);
        }

        return results.AsListReadOnly();
    }

    private async Task<PublisherAccountMediaAnalysis> DoGetPublisherAccountMediaAnalysisAsync(long publisherAccountId)
    {
        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId);

        var dynPublisherAccountMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherAccountMediaAnalysis>(publisherAccount.PublisherAccountId,
                                                                                                              string.Concat(DynItemType.PublisherMediaAnalysis, "|agganalysis"));

        if (dynPublisherAccountMediaAnalysis == null)
        {
            return null;
        }

        var dynPublisherAccountMediaAnalysisPosts = await _dynamoDb.GetItemAsync<DynPublisherAccountMediaAnalysis>(publisherAccount.PublisherAccountId,
                                                                                                                   string.Concat(DynItemType.PublisherMediaAnalysis, "|agganalysis|", PublisherContentType.Post));

        var dynPublisherAccountMediaAnalysisStories = await _dynamoDb.GetItemAsync<DynPublisherAccountMediaAnalysis>(publisherAccount.PublisherAccountId,
                                                                                                                     string.Concat(DynItemType.PublisherMediaAnalysis, "|agganalysis|", PublisherContentType.Story));

        var analysisResponse = new PublisherAccountMediaAnalysis
                               {
                                   AccountAnalysis = dynPublisherAccountMediaAnalysis.ToPublisherAccountMediaAnalysisResult(),
                                   StoryAnalysis = dynPublisherAccountMediaAnalysisStories.ToPublisherAccountMediaAnalysisResult(PublisherContentType.Story),
                                   PostAnalysis = dynPublisherAccountMediaAnalysisPosts.ToPublisherAccountMediaAnalysisResult(PublisherContentType.Post)
                               };

        if (analysisResponse.AccountAnalysis != null)
        {
            if (analysisResponse.PostAnalysis != null)
            {
                analysisResponse.PostAnalysis.PostsAnalyzed = analysisResponse.AccountAnalysis.PostsAnalyzed;
            }

            if (analysisResponse.StoryAnalysis != null)
            {
                analysisResponse.StoryAnalysis.StoriesAnalyzed = analysisResponse.AccountAnalysis.StoriesAnalyzed;
            }
        }

        return analysisResponse;
    }
}
