using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Dto.Users;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Helpers;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;
using LongRange = Rydr.Api.Dto.Shared.LongRange;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.Api.Services.Services
{
    public class PublisherStatsService : BaseApiService
    {
        private const string _audienceCityKeyPrefix = FbIgInsights.AudienceCity + "|";
        private const string _audienceCountryKeyPrefix = FbIgInsights.AudienceCountry + "|";
        private const string _audienceAgeGenderKeyPrefix = FbIgInsights.AudienceAgeGender + "|";
        private static readonly string _dailySnapshotPrefix = string.Concat(DynItemType.DailyStatSnapshot, "|");

        private static readonly HashSet<string> _suggestiveImageLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                                                         {
                                                                             "suggestive",
                                                                             "revealing",
                                                                             "nudity",
                                                                             "skin",
                                                                             "explicit",
                                                                             "swimwear",
                                                                             "underwear",
                                                                             "bikini",
                                                                             "lingerie",
                                                                             "bra",
                                                                             "thong",
                                                                             "panties",
                                                                             "lace"
                                                                         };

        private readonly ICountryLookupService _countryLookupService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IRydrDataService _rydrDataService;
        private readonly IElasticClient _esClient;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IElasticSearchService _elasticSearchService;

        public PublisherStatsService(ICountryLookupService countryLookupService, IPublisherAccountService publisherAccountService,
                                     IRydrDataService rydrDataService, IElasticClient esClient,
                                     IDeferRequestsService deferRequestsService, IElasticSearchService elasticSearchService)
        {
            _countryLookupService = countryLookupService;
            _publisherAccountService = publisherAccountService;
            _rydrDataService = rydrDataService;
            _esClient = esClient;
            _deferRequestsService = deferRequestsService;
            _elasticSearchService = elasticSearchService;
        }

        [RydrForcedSimpleCacheResponse(1800)]
        public async Task<OnlyResultsResponse<ContentTypeStat>> Get(GetPublisherContentStats request)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.GetPublisherIdFromIdentifier());

            var take = request.Limit.Gz(100);
            var mediaTake = (take * 4).ToDynamoBatchCeilingTake();

            if (mediaTake > 1000)
            {
                mediaTake = take.ToDynamoBatchCeilingTake();
            }

            // Get the lifetime stats for the last take*2 medias, return them for the take...
            var dynMedia = await _dynamoDb.FromQuery<DynPublisherMedia>(pm => pm.Id == publisherAccount.Id &&
                                                                              Dynamo.BeginsWith(pm.EdgeId, "00"))
                                          .Filter(pm => pm.DeletedOnUtc == null &&
                                                        pm.TypeId == (int)DynItemType.PublisherMedia &&
                                                        pm.ContentType == request.ContentType &&
                                                        pm.PublisherType == publisherAccount.PublisherType)
                                          .Select(pm => new
                                                        {
                                                            pm.EdgeId,
                                                            pm.MediaCreatedAt,
                                                            pm.MediaUrl,
                                                            pm.ThumbnailUrl,
                                                            pm.PublisherUrl
                                                        })
                                          .ExecAsync()
                                          .Take(mediaTake)
                                          .ToDictionarySafe(m => m.PublisherMediaId);

            var dynStats = await _dynamoDb.GetItemsAsync<DynPublisherMediaStat>(dynMedia.Select(l => new DynamoId(l.Key, DynPublisherMediaStat.BuildEdgeId(FbIgInsights.LifetimePeriod,
                                                                                                                                                           FbIgInsights.LifetimeEndTime))))
                                          .ToList(dynMedia.Count);

            var results = (dynStats?.Select(ds =>
                                            {
                                                var (engagements, impressions, saves, views, reach, actions, comments, replies) = ds.GetRatingStats();

                                                return new ContentTypeStat
                                                       {
                                                           PublisherMediaId = ds.PublisherMediaId,
                                                           MediaCreatedOn = dynMedia.ContainsKey(ds.PublisherMediaId)
                                                                                ? dynMedia[ds.PublisherMediaId].MediaCreatedAt.ToDateTime()
                                                                                : ds.CreatedOn,
                                                           MediaUrl = dynMedia.ContainsKey(ds.PublisherMediaId)
                                                                          ? dynMedia[ds.PublisherMediaId].MediaUrl.Coalesce(dynMedia[ds.PublisherMediaId].ThumbnailUrl)
                                                                          : null,
                                                           PublisherUrl = dynMedia.ContainsKey(ds.PublisherMediaId)
                                                                              ? dynMedia[ds.PublisherMediaId].PublisherUrl
                                                                              : null,
                                                           Period = ds.Period,
                                                           EndTime = ds.EndTime,
                                                           Engagements = engagements.NullIf(v => v <= 0),
                                                           Impressions = impressions.NullIf(v => v <= 0),
                                                           Saves = saves.NullIf(v => v <= 0),
                                                           Replies = replies.NullIf(v => v <= 0),
                                                           Views = views.NullIf(v => v <= 0),
                                                           Reach = reach.NullIf(v => v <= 0),
                                                           Actions = actions.NullIf(v => v <= 0),
                                                           Comments = comments.NullIf(v => v <= 0),
                                                           EngagementRating = ds.EngagementRating.NullIf(v => v <= 0),
                                                           TrueEngagementRating = ds.TrueEngagementRating.NullIf(v => v <= 0)
                                                       };
                                            })
                                   .OrderByDescending(s => s.MediaCreatedOn)
                                   .Take(take)).AsOnlyResultsResponse();

            _log.DebugInfoFormat("  GetPublisherContentStats for PublisherAccount [{0}] returned [{1}] media stats for the [{2}] content type",
                                 publisherAccount.DisplayName(), results.ResultCount, request.ContentType);

            return results;
        }

        [RydrForcedSimpleCacheResponse(1800)]
        public async Task<OnlyResultsResponse<AudienceLocationResult>> Get(GetPublisherAudienceLocations request)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherIdentifier.EqualsOrdinalCi("me")
                                                                                               ? request.RequestPublisherAccountId
                                                                                               : request.PublisherIdentifier.ToLong());

            var take = request.Limit.Gz(50);

            var latestDailyStatSnapshot = await GetLatestStatSnapshotAsync(publisherAccount.Id);

            if ((latestDailyStatSnapshot?.Stats).IsNullOrEmptyRydr())
            {
                return new OnlyResultsResponse<AudienceLocationResult>();
            }

            var cities = latestDailyStatSnapshot.Stats
                                                .Where(s => s.Key.StartsWithOrdinalCi(_audienceCityKeyPrefix))
                                                .Take(take)
                                                .Select(k => new AudienceLocationResult
                                                             {
                                                                 LocationType = AudienceLocationType.City,
                                                                 Name = k.Key.Replace(_audienceCityKeyPrefix, string.Empty).Replace("_", " "),
                                                                 Value = k.Value.Value
                                                             });

            var countries = latestDailyStatSnapshot.Stats
                                                   .Where(s => s.Key.StartsWithOrdinalCi(_audienceCountryKeyPrefix))
                                                   .Take(take)
                                                   .Select(k =>
                                                           {
                                                               var countryCode = k.Key.Replace(_audienceCountryKeyPrefix, string.Empty);

                                                               return new AudienceLocationResult
                                                                      {
                                                                          LocationType = AudienceLocationType.Country,
                                                                          Name = _countryLookupService.GetCountryNameFromTwoLetterIso(countryCode).Coalesce(countryCode),
                                                                          Value = k.Value.Value
                                                                      };
                                                           });

            var results = (cities ?? Enumerable.Empty<AudienceLocationResult>()).Concat(countries ?? Enumerable.Empty<AudienceLocationResult>())
                                                                                .AsOnlyResultsResponse();

            _log.DebugInfoFormat("  GetPublisherAudienceLocations for PublisherAccount [{0}] returned [{1}] audience location stats",
                                 publisherAccount.DisplayName(), results.ResultCount);

            return results;
        }

        [RydrForcedSimpleCacheResponse(1800)]
        public async Task<OnlyResultsResponse<AudienceAgeGenderResult>> Get(GetPublisherAudienceAgeGenders request)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherIdentifier.EqualsOrdinalCi("me")
                                                                                               ? request.RequestPublisherAccountId
                                                                                               : request.PublisherIdentifier.ToLong());

            var take = request.Limit.Gz(50);

            var latestDailyStatSnapshot = await GetLatestStatSnapshotAsync(publisherAccount.Id);

            if ((latestDailyStatSnapshot?.Stats).IsNullOrEmptyRydr())
            {
                return new OnlyResultsResponse<AudienceAgeGenderResult>();
            }

            var ageGenders = latestDailyStatSnapshot.Stats
                                                    .Where(s => s.Key.StartsWithOrdinalCi(_audienceAgeGenderKeyPrefix))
                                                    .Select(k =>
                                                            {
                                                                var dotIndex = k.Key.IndexOf(".", _audienceAgeGenderKeyPrefix.Length - 1, StringComparison.OrdinalIgnoreCase);

                                                                if (dotIndex < 0)
                                                                {
                                                                    return null;
                                                                }

                                                                return new AudienceAgeGenderResult
                                                                       {
                                                                           Gender = k.Key.Substring(dotIndex - 1, 1),
                                                                           AgeRange = k.Key.Substring(dotIndex + 1),
                                                                           Value = k.Value.Value
                                                                       };
                                                            })
                                                    .Where(t => t != null)
                                                    .Take(take);

            var results = ageGenders.AsOnlyResultsResponse();

            _log.DebugInfoFormat("  GetPublisherAudienceAgeGenders for PublisherAccount [{0}] returned [{1}] stats",
                                 publisherAccount.DisplayName(), results.ResultCount);

            return results;
        }

        [RydrForcedSimpleCacheResponse(1800)]
        public async Task<OnlyResultsResponse<AudienceGrowthResult>> Get(GetPublisherAudienceGrowth request)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherIdentifier.EqualsOrdinalCi("me")
                                                                                               ? request.RequestPublisherAccountId
                                                                                               : request.PublisherIdentifier.ToLong());

            var today = _dateTimeProvider.UtcNow.Date;
            var take = request.Limit.Gz(100);

            var startDateTime = request.StartDate.GetValueOrDefault(today.AddDays(-35)).Date;
            var endDateTime = request.EndDate.GetValueOrDefault(today).Date;
            var startDate = startDateTime.ToUnixTimestamp();
            var endDate = endDateTime.ToUnixTimestamp();
            var daysToReturn = (int)(endDateTime - startDateTime).TotalDays;

            var growthResults = GetDailyStatsWithFollowerCounts(publisherAccount.PublisherAccountId, startDate, endDate).Select(d => new AudienceGrowthResult
                                                                                                                                     {
                                                                                                                                         DayTimestamp = d.DayTimestamp,
                                                                                                                                         Date = d.DayTimestamp.ToDateTime(),
                                                                                                                                         Followers = (long)d.Stats[PublisherMetricName.FollowedBy].Value,
                                                                                                                                         Following = (long)d.Stats[PublisherMetricName.Follows].Value
                                                                                                                                     })
                                                                                                                        .Take(take)
                                                                                                                        .AsList();

            if (growthResults.IsNullOrEmpty())
            {
                _log.DebugInfoFormat("  GetPublisherAudienceAgeGenders for PublisherAccount [{0}] does not have any lifetime snapshots yet, returning empty.", publisherAccount.DisplayName());

                return new OnlyResultsResponse<AudienceGrowthResult>();
            }

            // And get daily stat values for profile daily tracked stats (reach, impressions, etc.)
            var dailyKeys = (daysToReturn + 1).Times(i => new DynamoId(publisherAccount.Id, DynDailyStat.BuildEdgeId(startDateTime.AddDays(i).Date.ToUnixTimestamp())));
            var earliestSnapshot = growthResults.OrderBy(d => d.DayTimestamp).First();
            var followedBySum = 0L;
            AudienceGrowthResult nextGrowthResult = null;

            var dailyStats = await _dynamoDb.GetItemsAsync<DynDailyStat>(dailyKeys)
                                            .ToList(dailyKeys.Count);

            foreach (var dailyStat in dailyStats.Where(s => !s.Stats.IsNullOrEmptyRydr())
                                                .OrderByDescending(s => s.DayTimestamp))
            {
                var matchingGrowthResult = dailyStat.DayTimestamp < earliestSnapshot.DayTimestamp
                                               ? null
                                               : growthResults.FirstOrDefault(g => g.DayTimestamp.Equals(dailyStat.DayTimestamp));

                if (matchingGrowthResult == null)
                {
                    if (dailyStat.DayTimestamp >= earliestSnapshot.DayTimestamp ||
                        !dailyStat.Stats.ContainsKey(FbIgInsights.DailyFollowerCountName))
                    {
                        continue;
                    }

                    // Have a daily stat earlier than a snapshot (which we can only get from the time they signup onward)
                    followedBySum += (long)dailyStat.Stats[FbIgInsights.DailyFollowerCountName].Value;

                    matchingGrowthResult = new AudienceGrowthResult
                                           {
                                               DayTimestamp = dailyStat.DayTimestamp,
                                               Date = dailyStat.DayTimestamp.ToDateTime(),
                                               Followers = earliestSnapshot.Followers - followedBySum,
                                               Following = earliestSnapshot.Following
                                           };

                    growthResults.Add(matchingGrowthResult);
                }

                matchingGrowthResult.OnlineFollowers = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyOnlineFollowersName)?.Value ?? 0);
                matchingGrowthResult.Impressions = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.ImpressionsName)?.Value ?? 0);
                matchingGrowthResult.Reach = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.ReachName)?.Value ?? 0);
                matchingGrowthResult.ProfileViews = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyProfileViewsName)?.Value ?? 0);
                matchingGrowthResult.WebsiteClicks = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyWebsiteClicksName)?.Value ?? 0);
                matchingGrowthResult.EmailContacts = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyEmailContactsName)?.Value ?? 0);
                matchingGrowthResult.GetDirectionClicks = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyGetDirectionsClicksName)?.Value ?? 0);
                matchingGrowthResult.PhoneCallClicks = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyPhoneCallClicksName)?.Value ?? 0);
                matchingGrowthResult.TextMessageClicks = (long)(dailyStat.Stats.GetValueOrDefault(FbIgInsights.DailyTextMessageClicksName)?.Value ?? 0);

                CalcPriorDayGrowthValues(matchingGrowthResult, nextGrowthResult);

                // We work through the stats in reverse day order, so the "next" growth result is the one we previously iterated over
                nextGrowthResult = matchingGrowthResult;
            }

            var results = growthResults.AsOnlyResultsResponse();

            _log.DebugInfoFormat("  GetPublisherAudienceGrowth for PublisherAccount [{0}] returned [{1}] daily stat snapshots",
                                 publisherAccount.DisplayName(), results.ResultCount);

            return results;
        }

        [RydrForcedSimpleCacheResponse(1800)]
        public async Task<OnlyResultResponse<CreatorStatsResponse>> Get(QueryCreatorStats request)
        {
            var esCreatorSearch = request.ToEsCreatorSearch();

            var esSearchResult = await _elasticSearchService.SearchAggregateCreatorsAsync(esCreatorSearch);

            var response = new CreatorStatsResponse();
            var agg = esSearchResult?.Results?.FirstOrDefault();

            if (agg == null)
            {
                return response.AsOnlyResultResponse();
            }

            response.Creators = agg.Creators;
            response.Followers = agg.FollowedBy.ToCreatorStat();
            response.StoryEngagementRating = agg.StoryEngagementRating.ToCreatorStat(EsCreatorScales.EngagementRatingScale);
            response.StoryImpressions = agg.StoryImpressions.ToCreatorStat();
            response.StoryReach = agg.StoryReach.ToCreatorStat();
            response.StoryActions = agg.StoryActions.ToCreatorStat();
            response.Stories = agg.Stories.ToCreatorStat();
            response.MediaEngagementRating = agg.MediaEngagementRating.ToCreatorStat(EsCreatorScales.EngagementRatingScale);
            response.MediaTrueEngagementRating = agg.MediaTrueEngagementRating.ToCreatorStat(EsCreatorScales.EngagementRatingScale);
            response.MediaImpressions = agg.MediaImpressions.ToCreatorStat();
            response.MediaReach = agg.MediaReach.ToCreatorStat();
            response.MediaActions = agg.MediaActions.ToCreatorStat();
            response.Medias = agg.Medias.ToCreatorStat();
            response.AvgStoryImpressions = agg.AvgStoryImpressions.ToCreatorStat();
            response.AvgMediaImpressions = agg.AvgMediaImpressions.ToCreatorStat();
            response.AvgStoryReach = agg.AvgStoryReach.ToCreatorStat();
            response.AvgMediaReach = agg.AvgMediaReach.ToCreatorStat();
            response.AvgStoryActions = agg.AvgStoryActions.ToCreatorStat();
            response.AvgMediaActions = agg.AvgMediaActions.ToCreatorStat();
            response.Follower7DayJitter = agg.Follower7DayJitter.ToCreatorStat();
            response.Follower14DayJitter = agg.Follower14DayJitter.ToCreatorStat();
            response.Follower30DayJitter = agg.Follower30DayJitter.ToCreatorStat();
            response.AudienceUsa = agg.AudienceUsa.ToCreatorStat();
            response.AudienceEnglish = agg.AudienceEnglish.ToCreatorStat();
            response.AudienceSpanish = agg.AudienceSpanish.ToCreatorStat();
            response.AudienceMale = agg.AudienceMale.ToCreatorStat();
            response.AudienceFemale = agg.AudienceFemale.ToCreatorStat();
            response.AudienceAge1317 = agg.AudienceAge1317.ToCreatorStat();
            response.AudienceAge18Up = agg.AudienceAge18Up.ToCreatorStat();
            response.AudienceAge1824 = agg.AudienceAge1824.ToCreatorStat();
            response.AudienceAge25Up = agg.AudienceAge25Up.ToCreatorStat();
            response.AudienceAge2534 = agg.AudienceAge2534.ToCreatorStat();
            response.AudienceAge3544 = agg.AudienceAge3544.ToCreatorStat();
            response.AudienceAge4554 = agg.AudienceAge4554.ToCreatorStat();
            response.AudienceAge5564 = agg.AudienceAge5564.ToCreatorStat();
            response.AudienceAge65Up = agg.AudienceAge65Up.ToCreatorStat();
            response.ImagesAvgAge = agg.ImagesAvgAge.ToCreatorStat();
            response.SuggestiveRating = agg.SuggestiveRating.ToCreatorStat(EsCreatorScales.PercentageScale);
            response.ViolenceRating = agg.ViolenceRating.ToCreatorStat(EsCreatorScales.PercentageScale);
            response.Rydr7DayActivityRating = agg.Rydr7DayActivityRating.ToCreatorStat(EsCreatorScales.PercentageScale);
            response.Rydr14DayActivityRating = agg.Rydr14DayActivityRating.ToCreatorStat(EsCreatorScales.PercentageScale);
            response.Rydr30DayActivityRating = agg.Rydr30DayActivityRating.ToCreatorStat(EsCreatorScales.PercentageScale);
            response.Requests = agg.Requests.ToCreatorStat();
            response.CompletedRequests = agg.CompletedRequests.ToCreatorStat();
            response.AvgCPMr = agg.AvgCPMr.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPMi = agg.AvgCPMi.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPE = agg.AvgCPE.ToCreatorStat(EsCreatorScales.AvgCostPerScale);

            response.Creators1 = agg.Requests1?.Count > 0
                                     ? agg.Requests1.Count
                                     : 0;

            response.Requests1 = agg.Requests1.ToCreatorStat();
            response.CompletedRequests1 = agg.CompletedRequests1.ToCreatorStat();
            response.AvgCPMr1 = agg.AvgCPMr1.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPMi1 = agg.AvgCPMi1.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPE1 = agg.AvgCPE1.ToCreatorStat(EsCreatorScales.AvgCostPerScale);

            response.Creators2 = agg.Requests2?.Count > 0
                                     ? agg.Requests2.Count
                                     : 0;

            response.Requests2 = agg.Requests2.ToCreatorStat();
            response.CompletedRequests2 = agg.CompletedRequests2.ToCreatorStat();
            response.AvgCPMr2 = agg.AvgCPMr2.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPMi2 = agg.AvgCPMi2.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPE2 = agg.AvgCPE2.ToCreatorStat(EsCreatorScales.AvgCostPerScale);

            response.Creators3 = agg.Requests3?.Count > 0
                                     ? agg.Requests3.Count
                                     : 0;

            response.Requests3 = agg.Requests3.ToCreatorStat();
            response.CompletedRequests3 = agg.CompletedRequests3.ToCreatorStat();
            response.AvgCPMr3 = agg.AvgCPMr3.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPMi3 = agg.AvgCPMi3.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPE3 = agg.AvgCPE3.ToCreatorStat(EsCreatorScales.AvgCostPerScale);

            response.Creators4 = agg.Requests4?.Count > 0
                                     ? agg.Requests4.Count
                                     : 0;

            response.Requests4 = agg.Requests4.ToCreatorStat();
            response.CompletedRequests4 = agg.CompletedRequests4.ToCreatorStat();
            response.AvgCPMr4 = agg.AvgCPMr4.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPMi4 = agg.AvgCPMi4.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPE4 = agg.AvgCPE4.ToCreatorStat(EsCreatorScales.AvgCostPerScale);

            response.Creators5 = agg.Requests5?.Count > 0
                                     ? agg.Requests5.Count
                                     : 0;

            response.Requests5 = agg.Requests5.ToCreatorStat();
            response.CompletedRequests5 = agg.CompletedRequests5.ToCreatorStat();
            response.AvgCPMr5 = agg.AvgCPMr5.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPMi5 = agg.AvgCPMi5.ToCreatorStat(EsCreatorScales.AvgCostPerScale);
            response.AvgCPE5 = agg.AvgCPE5.ToCreatorStat(EsCreatorScales.AvgCostPerScale);

            LongRange getEstimatedRangeValue(CreatorStat forStat)
                => request.MaxApprovals > 0 && forStat.IsValidForRange()
                       ? LongRange.From(request.MaxApprovals.MinGz(response.Creators) * (long)(forStat.Avg.Value - forStat.StdDev.Value),
                                        request.MaxApprovals.MinGz(response.Creators) * (long)(forStat.Avg.Value + forStat.StdDev.Value))
                       : null;

            response.EstimatedStoryImpressions = getEstimatedRangeValue(response.AvgStoryImpressions);
            response.EstimatedStoryReach = getEstimatedRangeValue(response.AvgStoryReach);
            response.EstimatedStoryEngagements = getEstimatedRangeValue(response.AvgStoryActions);
            response.EstimatedPostImpressions = getEstimatedRangeValue(response.AvgMediaImpressions);
            response.EstimatedPostReach = getEstimatedRangeValue(response.AvgMediaReach);
            response.EstimatedPostEngagements = getEstimatedRangeValue(response.AvgMediaActions);

            LongRange getTargetRangeValue(CreatorStat forStat, long target)
                => target > 0 && forStat.IsValidForRange()
                       ? LongRange.From((long)Math.Ceiling(target / (forStat.Avg.Value + forStat.StdDev.Value)),
                                        (long)Math.Ceiling(target / (forStat.Avg.Value - forStat.StdDev.Value)))
                       : null;

            response.StoryApprovalsForTargetImpressions = getTargetRangeValue(response.AvgStoryImpressions, request.TargetImpressions);
            response.StoryApprovalsForTargetReach = getTargetRangeValue(response.AvgStoryReach, request.TargetReach);
            response.StoryApprovalsForTargetEngagements = getTargetRangeValue(response.AvgStoryActions, request.TargetEngagements);
            response.PostApprovalsForTargetImpressions = getTargetRangeValue(response.AvgMediaImpressions, request.TargetImpressions);
            response.PostApprovalsForTargetReach = getTargetRangeValue(response.AvgMediaReach, request.TargetReach);
            response.PostApprovalsForTargetEngagements = getTargetRangeValue(response.AvgMediaActions, request.TargetEngagements);

            return response.AsOnlyResultResponse();
        }

        [RequiredRole("Admin")]
        public async Task<StatusSimpleResponse> Post(PostUpdateCreatorsMetrics request)
        {
            var publisherAccountIds = request.PublisherAccountIds
                                      ??
                                      _rydrDataService.QueryAdHoc(db => db.Column<long>(db.From<RydrPublisherAccount>()
                                                                                          .Where(p => p.DeletedOn == null &&
                                                                                                      p.PublisherType == PublisherType.Facebook &&
                                                                                                      p.RydrAccountType >= RydrAccountType.Influencer &&
                                                                                                      !p.IsSyncDisabled)
                                                                                          .Select(p => p.Id)));

            var countQueued = 0;

            foreach (var publisherAccountId in publisherAccountIds)
            {
                var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

                if (publisherAccount == null || publisherAccount.IsDeleted() || !publisherAccount.RydrAccountType.IsInfluencer())
                {
                    continue;
                }

                _deferRequestsService.DeferLowPriRequest(new PostUpdateCreatorMetrics
                                                         {
                                                             PublisherIdentifier = publisherAccountId.ToStringInvariant()
                                                         });

                countQueued++;
            }

            return new StatusSimpleResponse($"Enqueud [{countQueued}] accounts from potential list of [{publisherAccountIds.Count}] IDs for metric update.");
        }

        [RequiredRole("Admin")]
        public async Task Post(PostUpdateCreatorMetrics request)
        {
            var publisherAccountId = request.GetPublisherIdFromIdentifier();
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null || publisherAccount.IsDeleted() || !publisherAccount.RydrAccountType.IsInfluencer())
            {
                _deferRequestsService.RemoveRecurringJob(PostUpdateCreatorMetrics.GetRecurringJobId(publisherAccountId));

                return;
            }

            GeoLocation lastLocation = null;

            var lastLocationMap = await MapItemService.DefaultMapItemService
                                                      .TryGetMapAsync(publisherAccount.PublisherAccountId, DynItemMap.BuildEdgeId(DynItemType.AccountLocation, "LastLocation"));

            if ((lastLocationMap?.MappedItemEdgeId).HasValue())
            {
                var latitude = DynItemMap.GetFirstEdgeSegment(lastLocationMap.MappedItemEdgeId).ToDoubleRydr(double.MinValue);
                var longitude = DynItemMap.GetFinalEdgeSegment(lastLocationMap.MappedItemEdgeId).ToDoubleRydr(double.MaxValue);

                if (GeoExtensions.IsValidLatLon(latitude, longitude))
                {
                    lastLocation = GeoLocation.TryCreate(latitude, longitude);

                    _deferRequestsService.DeferLowPriRequest(new AddLocationMap
                                                             {
                                                                 Latitude = latitude,
                                                                 Longitude = longitude,
                                                                 ForPublisherAccountId = publisherAccount.PublisherAccountId
                                                             }.WithAdminRequestInfo());
                }
            }

            IEnumerable<(string Tag, long Count)> getTagDetailsFromAnalysis(DynPublisherAccountMediaAnalysis from)
            {
                if (from == null)
                {
                    yield break;
                }

                if (!from.ImageLabels.IsNullOrEmpty())
                {
                    foreach (var entity in from.ImageLabels.Where(l => l != null))
                    {
                        if ((entity.EntityText).HasValue())
                        {
                            yield return (entity.EntityText, entity.Occurrences);
                        }

                        if ((entity.EntityType).HasValue())
                        {
                            yield return (entity.EntityType, entity.Occurrences);
                        }
                    }
                }

                if (!from.PopularEntities.IsNullOrEmpty())
                {
                    foreach (var entity in from.PopularEntities.Where(l => l != null))
                    {
                        if ((entity.EntityText).HasValue())
                        {
                            yield return (entity.EntityText, entity.Occurrences);
                        }

                        if ((entity.EntityType).HasValue())
                        {
                            yield return (entity.EntityType, entity.Occurrences);
                        }
                    }
                }
            }

            // Age and genders apply to the facebook user accounts, not necessarily the creator FbIg account directly - in which case we have to go to
            // the rydr user accounts that this FbIg account is linked to in order to collect age/gender data if it isn't assoicated with the publisher directly
            var ageMin = publisherAccount.AgeRangeMin;
            var ageMax = publisherAccount.AgeRangeMax;
            var gender = (int)publisherAccount.Gender;

            if (ageMin <= 0 || ageMax <= 0 || ageMin > ageMax || gender <= 0)
            { // Get the range of ages over all linked user accounts for purposes of querying - get the user accounts this account linked from
                var associatedTokenAccounts = await _publisherAccountService.GetLinkedPublisherAccountsAsync(publisherAccount.PublisherAccountId)
                                                                            .Take(250)
                                                                            .ToList(250);

                if (!associatedTokenAccounts.IsNullOrEmpty())
                {
                    foreach (var associatedTokenAccount in associatedTokenAccounts)
                    {
                        if (associatedTokenAccount.IsDeleted())
                        { // Cleanup the linkage, shouldn't be linked
                            _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                               {
                                                                   FromPublisherAccountId = associatedTokenAccount.PublisherAccountId,
                                                                   ToPublisherAccountId = publisherAccount.PublisherAccountId,
                                                                   FromWorkspaceId = 0
                                                               }.WithAdminRequestInfo());

                            continue;
                        }

                        if (associatedTokenAccount.AgeRangeMin > 0 && (ageMin <= 0 || associatedTokenAccount.AgeRangeMin < ageMin))
                        {
                            ageMin = associatedTokenAccount.AgeRangeMin;
                        }

                        if (associatedTokenAccount.AgeRangeMax > ageMax)
                        {
                            ageMax = associatedTokenAccount.AgeRangeMax;
                        }

                        if (associatedTokenAccount.Gender != GenderType.Unknown && gender <= 0 ||
                            associatedTokenAccount.Gender != GenderType.Other && gender >= 3)
                        {
                            gender = (int)associatedTokenAccount.Gender;
                        }
                    }
                }
            }

            var dynPublisherAccountMediaAnalysis = await _dynamoDb.GetItemAsync<DynPublisherAccountMediaAnalysis>(publisherAccount.PublisherAccountId,
                                                                                                                  string.Concat(DynItemType.PublisherMediaAnalysis, "|agganalysis"));

            var suggestiveRating = 0d;
            var violenceRating = 0d;

            if (dynPublisherAccountMediaAnalysis != null && dynPublisherAccountMediaAnalysis.CountImageAnalyzed > 0)
            {
                var moderationSuggestiveSum = (dynPublisherAccountMediaAnalysis?.Moderations
                                               ??
                                               Enumerable.Empty<MediaAnalysisEntity>()).Where(m => (m?.EntityText).ContainsAny(_suggestiveImageLabels)
                                                                                                   ||
                                                                                                   (m?.EntityType).ContainsAny(_suggestiveImageLabels))
                                                                                       .Sum(m => m.Occurrences);

                var imageLabelsSuggestiveSum = (dynPublisherAccountMediaAnalysis?.ImageLabels
                                                ??
                                                Enumerable.Empty<MediaAnalysisEntity>()).Where(l => (l?.EntityText).ContainsAny(_suggestiveImageLabels)
                                                                                                    ||
                                                                                                    (l?.EntityType).ContainsAny(_suggestiveImageLabels))
                                                                                        .Sum(l => l.Occurrences);

                suggestiveRating = (moderationSuggestiveSum + imageLabelsSuggestiveSum) / (double)dynPublisherAccountMediaAnalysis.CountImageAnalyzed;

                var violenceSum = (dynPublisherAccountMediaAnalysis.Moderations
                                   ??
                                   Enumerable.Empty<MediaAnalysisEntity>()).Where(m => ((m?.EntityText).HasValue() &&
                                                                                        m.EntityText.Contains("violence", StringComparison.OrdinalIgnoreCase))
                                                                                       ||
                                                                                       ((m?.EntityType).HasValue() &&
                                                                                        m.EntityType.Contains("violence", StringComparison.OrdinalIgnoreCase)))
                                                                           .Sum(m => m.Occurrences);

                violenceRating = violenceSum / (double)dynPublisherAccountMediaAnalysis.CountImageAnalyzed;
            }

            // Set the pieces made up from the publisher account profile/model directly
            var esCreator = new EsCreator
                            {
                                SearchValue = string.Concat(publisherAccount.Id, " ", publisherAccount.UserName.ToLowerInvariant(), " ", publisherAccount.AccountId.ToLowerInvariant()),
                                Tags = (publisherAccount.Tags ?? Enumerable.Empty<Tag>()).Select(t => t.ToString())
                                                                                         .Union(getTagDetailsFromAnalysis(dynPublisherAccountMediaAnalysis).GroupBy(t => t.Tag, StringComparer.OrdinalIgnoreCase)
                                                                                                                                                           .OrderByDescending(g => g.Sum(t => t.Count))
                                                                                                                                                           .Take(15)
                                                                                                                                                           .Select(g => g.Key),
                                                                                                StringComparer.OrdinalIgnoreCase)
                                                                                         .AsList()
                                                                                         .ToNullIfEmpty(),
                                ImagesAvgAge = dynPublisherAccountMediaAnalysis == null || dynPublisherAccountMediaAnalysis.CountFacesAnalyzed <= 0
                                                   ? 0
                                                   : (int)(Math.Round(dynPublisherAccountMediaAnalysis.ImageFacesAgeSum / (double)dynPublisherAccountMediaAnalysis.CountFacesAnalyzed / 2d, MidpointRounding.AwayFromZero)),
                                SuggestiveRating = (int)(suggestiveRating * 100 * EsCreatorScales.PercentageScale), // 100 gets the percentage, 100 more for scale
                                ViolenceRating = (int)(violenceRating * 100 * EsCreatorScales.PercentageScale), // 100 gets the percentage, 100 more for scale
                                IsDeleted = publisherAccount.IsDeleted(),
                                PublisherAccountId = publisherAccount.PublisherAccountId,
                                AccountId = publisherAccount.AccountId,
                                PublisherType = (int)publisherAccount.PublisherType,
                                LastLocation = lastLocation,
                                LastLocationModifiedOn = lastLocationMap?.ReferenceNumber ?? 0,
                                AgeRangeMin = ageMin,
                                AgeRangeMax = ageMax,
                                Gender = gender,
                                MediaCount = (int)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaCount) ?? 0),
                                FollowedBy = (long)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.FollowedBy) ?? 0),
                                Following = (long)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.Follows) ?? 0),
                                StoryEngagementRating = (int)((publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.StoryEngagementRating) ?? 0) * EsCreatorScales.EngagementRatingScale),
                                StoryImpressions = (long)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryImpressions) ?? 0),
                                StoryReach = (long)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryReach) ?? 0),
                                StoryActions = (int)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryActions) ?? 0),
                                Stories = (int)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentStoryCount) ?? 0),
                                MediaEngagementRating = (int)((publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentEngagementRating) ?? 0) * EsCreatorScales.EngagementRatingScale),
                                MediaTrueEngagementRating = (int)((publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentTrueEngagementRating) ?? 0) * EsCreatorScales.EngagementRatingScale),
                                MediaImpressions = (long)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaImpressions) ?? 0),
                                MediaReach = (long)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaReach) ?? 0),
                                MediaActions = (int)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaActions) ?? 0),
                                Medias = (int)(publisherAccount.Metrics?.GetValueOrDefault(PublisherMetricName.RecentMediaCount) ?? 0)
                            };

            esCreator.AvgStoryImpressions = esCreator.Stories > 0
                                                ? esCreator.StoryImpressions / esCreator.Stories
                                                : 0;

            esCreator.AvgMediaImpressions = esCreator.Medias > 0
                                                ? esCreator.MediaImpressions / esCreator.Medias
                                                : 0;

            esCreator.AvgStoryReach = esCreator.Stories > 0
                                          ? esCreator.StoryReach / esCreator.Stories
                                          : 0;

            esCreator.AvgMediaReach = esCreator.Medias > 0
                                          ? esCreator.MediaReach / esCreator.Medias
                                          : 0;

            esCreator.AvgStoryActions = esCreator.Stories > 0
                                            ? esCreator.StoryActions / esCreator.Stories
                                            : 0;

            esCreator.AvgMediaActions = esCreator.Medias > 0
                                            ? esCreator.MediaActions / esCreator.Medias
                                            : 0;

            // Populate the pieces that are made up by the stats (current or historical)
            var today = _dateTimeProvider.UtcNow.Date;
            var day7Ago = today.AddDays(-7).ToUnixTimestamp();
            var day14Ago = today.AddDays(-14).ToUnixTimestamp();

            var follower7DayMin = 0L;
            var follower7DayMax = 0L;
            var follower14DayMin = 0L;
            var follower14DayMax = 0L;
            var follower30DayMin = 0L;
            var follower30DayMax = 0L;

            long getCurrentOrValidValue(long currentValue, DynDailyStatSnapshot fromSnapshot, Func<Dictionary<string, DailyStatValue>, double?> getter)
            {
                if (currentValue > 0)
                {
                    return currentValue;
                }

                var thisSnapshotValue = getter(fromSnapshot.Stats);

                return thisSnapshotValue.HasValue && thisSnapshotValue.Value > 0
                           ? (long)thisSnapshotValue.Value
                           : 0;
            }

            foreach (var dailyStat in GetDailyStatsWithFollowerCounts(publisherAccount.PublisherAccountId,
                                                                      today.AddDays(-30).ToUnixTimestamp(),
                                                                      today.ToUnixTimestamp()).OrderByDescending(s => s.DayTimestamp)
                                                                                              .Where(s => !s.Stats.IsNullOrEmptyRydr()))
            {
                // Set the current values that have not been set yet to the most recent valid value we have
                esCreator.AudienceUsa = getCurrentOrValidValue(esCreator.AudienceUsa, dailyStat, m => m.GetValueOrDefault("audience_country|US")?.Value);

                esCreator.AudienceEnglish = getCurrentOrValidValue(esCreator.AudienceEnglish, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_locale|en_"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceSpanish = getCurrentOrValidValue(esCreator.AudienceSpanish, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_locale|es_"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceMale = getCurrentOrValidValue(esCreator.AudienceMale, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|M."))
                                                                                                         .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceFemale = getCurrentOrValidValue(esCreator.AudienceFemale, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|F."))
                                                                                                             .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge1317 = getCurrentOrValidValue(esCreator.AudienceAge1317, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".13-17"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge18Up = getCurrentOrValidValue(esCreator.AudienceAge18Up, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           !s.Key.EndsWithOrdinalCi(".13-17"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge1824 = getCurrentOrValidValue(esCreator.AudienceAge1824, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".18-24"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge25Up = getCurrentOrValidValue(esCreator.AudienceAge25Up, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           !s.Key.EndsWithOrdinalCi(".13-17") &&
                                                                                                                           !s.Key.EndsWithOrdinalCi(".18-24"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge2534 = getCurrentOrValidValue(esCreator.AudienceAge2534, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".25-34"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge3544 = getCurrentOrValidValue(esCreator.AudienceAge3544, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".35-44"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge4554 = getCurrentOrValidValue(esCreator.AudienceAge4554, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".45-54"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge5564 = getCurrentOrValidValue(esCreator.AudienceAge5564, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".55-64"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                esCreator.AudienceAge65Up = getCurrentOrValidValue(esCreator.AudienceAge65Up, dailyStat, m => m.Where(s => s.Key.StartsWithOrdinalCi("audience_gender_age|") &&
                                                                                                                           s.Key.EndsWithOrdinalCi(".65+"))
                                                                                                               .Sum(s => s.Value?.Value ?? 0));

                var followerStat = dailyStat.Stats[PublisherMetricName.FollowedBy];

                // Does this stat apply to the 14 day window?
                if (dailyStat.DayTimestamp >= day14Ago)
                {
                    if (followerStat.MinValue > 0 && (follower14DayMin <= 0 || follower14DayMin > followerStat.MinValue))
                    {
                        follower14DayMin = (long)followerStat.MinValue;
                    }

                    if (followerStat.MaxValue > 0 && followerStat.MaxValue > follower14DayMax)
                    {
                        follower14DayMax = (long)followerStat.MaxValue;
                    }

                    // Apply to the 7 day window?
                    if (dailyStat.DayTimestamp >= day7Ago)
                    {
                        if (followerStat.MinValue > 0 && (follower7DayMin <= 0 || follower7DayMin > followerStat.MinValue))
                        {
                            follower7DayMin = (long)followerStat.MinValue;
                        }

                        if (followerStat.MaxValue > 0 && followerStat.MaxValue > follower7DayMax)
                        {
                            follower7DayMax = (long)followerStat.MaxValue;
                        }
                    }
                }

                // Always applies to 30 day window
                if (followerStat.MinValue > 0 && (follower30DayMin <= 0 || follower30DayMin > followerStat.MinValue))
                {
                    follower30DayMin = (long)followerStat.MinValue;
                }

                if (followerStat.MaxValue > 0 && followerStat.MaxValue > follower30DayMax)
                {
                    follower30DayMax = (long)followerStat.MaxValue;
                }
            }

            esCreator.Follower7DayJitter = Math.Abs(follower7DayMax - follower7DayMin);
            esCreator.Follower14DayJitter = Math.Abs(follower14DayMax - follower14DayMin);
            esCreator.Follower30DayJitter = Math.Abs(follower30DayMax - follower30DayMin);

            var totalPublisherDealStats = await GetPublisherDealStatsAsync(publisherAccount.PublisherAccountId, -1, int.MaxValue);

            if (totalPublisherDealStats?.Requests != null && totalPublisherDealStats.Completions != null)
            {
                esCreator.Requests = totalPublisherDealStats.Requests.Requests;
                esCreator.CompletedRequests = totalPublisherDealStats.Completions.CompletedRequests;

                esCreator.AvgCPMr = totalPublisherDealStats.Requests.Reach <= 0
                                        ? 0
                                        : (long)(((totalPublisherDealStats.Completions.CompletedRequestsCost / totalPublisherDealStats.Requests.Reach) * 1000) * EsCreatorScales.AvgCostPerScale);

                esCreator.AvgCPMi = totalPublisherDealStats.Requests.Impressions <= 0
                                        ? 0
                                        : (long)(((totalPublisherDealStats.Completions.CompletedRequestsCost / totalPublisherDealStats.Requests.Impressions) * 1000) * EsCreatorScales.AvgCostPerScale);

                esCreator.AvgCPE = totalPublisherDealStats.Requests.Engagements <= 0
                                       ? 0
                                       : (long)(((totalPublisherDealStats.Completions.CompletedRequestsCost / totalPublisherDealStats.Requests.Engagements) * 1000) * EsCreatorScales.AvgCostPerScale);
            }

            var bucket1DealStats = await GetPublisherDealStatsAsync(publisherAccount.PublisherAccountId, -1, 5);

            if (bucket1DealStats?.Requests != null && bucket1DealStats.Completions != null)
            {
                esCreator.Requests1 = bucket1DealStats.Requests.Requests.NullIfNotPositive();
                esCreator.CompletedRequests1 = bucket1DealStats.Completions.CompletedRequests.NullIfNotPositive();

                esCreator.AvgCPMr1 = bucket1DealStats.Requests.Reach <= 0
                                         ? null
                                         : ((long?)(((bucket1DealStats.Completions.CompletedRequestsCost / bucket1DealStats.Requests.Reach) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPMi1 = bucket1DealStats.Requests.Impressions <= 0
                                         ? null
                                         : ((long?)(((bucket1DealStats.Completions.CompletedRequestsCost / bucket1DealStats.Requests.Impressions) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPE1 = bucket1DealStats.Requests.Engagements <= 0
                                        ? null
                                        : ((long?)(((bucket1DealStats.Completions.CompletedRequestsCost / bucket1DealStats.Requests.Engagements) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();
            }

            var bucket2DealStats = await GetPublisherDealStatsAsync(publisherAccount.PublisherAccountId, 5, 10);

            if (bucket2DealStats?.Requests != null && bucket2DealStats.Completions != null)
            {
                esCreator.Requests2 = bucket2DealStats.Requests.Requests.NullIfNotPositive();
                esCreator.CompletedRequests2 = bucket2DealStats.Completions.CompletedRequests.NullIfNotPositive();

                esCreator.AvgCPMr2 = bucket2DealStats.Requests.Reach <= 0
                                         ? null
                                         : ((long?)(((bucket2DealStats.Completions.CompletedRequestsCost / bucket2DealStats.Requests.Reach) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPMi2 = bucket2DealStats.Requests.Impressions <= 0
                                         ? null
                                         : ((long?)(((bucket2DealStats.Completions.CompletedRequestsCost / bucket2DealStats.Requests.Impressions) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPE2 = bucket2DealStats.Requests.Engagements <= 0
                                        ? null
                                        : ((long?)(((bucket2DealStats.Completions.CompletedRequestsCost / bucket2DealStats.Requests.Engagements) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();
            }

            var bucket3DealStats = await GetPublisherDealStatsAsync(publisherAccount.PublisherAccountId, 10, 25);

            if (bucket3DealStats?.Requests != null && bucket3DealStats.Completions != null)
            {
                esCreator.Requests3 = bucket3DealStats.Requests.Requests.NullIfNotPositive();
                esCreator.CompletedRequests3 = bucket3DealStats.Completions.CompletedRequests.NullIfNotPositive();

                esCreator.AvgCPMr3 = bucket3DealStats.Requests.Reach <= 0
                                         ? null
                                         : ((long?)(((bucket3DealStats.Completions.CompletedRequestsCost / bucket3DealStats.Requests.Reach) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPMi3 = bucket3DealStats.Requests.Impressions <= 0
                                         ? null
                                         : ((long?)(((bucket3DealStats.Completions.CompletedRequestsCost / bucket3DealStats.Requests.Impressions) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPE3 = bucket3DealStats.Requests.Engagements <= 0
                                        ? null
                                        : ((long?)(((bucket3DealStats.Completions.CompletedRequestsCost / bucket3DealStats.Requests.Engagements) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();
            }

            var bucket4DealStats = await GetPublisherDealStatsAsync(publisherAccount.PublisherAccountId, 25, 100);

            if (bucket4DealStats?.Requests != null && bucket4DealStats.Completions != null)
            {
                esCreator.Requests4 = bucket4DealStats.Requests.Requests.NullIfNotPositive();
                esCreator.CompletedRequests4 = bucket4DealStats.Completions.CompletedRequests.NullIfNotPositive();

                esCreator.AvgCPMr4 = bucket4DealStats.Requests.Reach <= 0
                                         ? null
                                         : ((long?)(((bucket4DealStats.Completions.CompletedRequestsCost / bucket4DealStats.Requests.Reach) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPMi4 = bucket4DealStats.Requests.Impressions <= 0
                                         ? null
                                         : ((long?)(((bucket4DealStats.Completions.CompletedRequestsCost / bucket4DealStats.Requests.Impressions) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPE4 = bucket4DealStats.Requests.Engagements <= 0
                                        ? null
                                        : ((long?)(((bucket4DealStats.Completions.CompletedRequestsCost / bucket4DealStats.Requests.Engagements) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();
            }

            var bucket5DealStats = await GetPublisherDealStatsAsync(publisherAccount.PublisherAccountId, 100, int.MaxValue);

            if (bucket5DealStats?.Requests != null && bucket5DealStats.Completions != null)
            {
                esCreator.Requests5 = bucket5DealStats.Requests.Requests.NullIfNotPositive();
                esCreator.CompletedRequests5 = bucket5DealStats.Completions.CompletedRequests.NullIfNotPositive();

                esCreator.AvgCPMr5 = bucket5DealStats.Requests.Reach <= 0
                                         ? null
                                         : ((long?)(((bucket5DealStats.Completions.CompletedRequestsCost / bucket5DealStats.Requests.Reach) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPMi5 = bucket5DealStats.Requests.Impressions <= 0
                                         ? null
                                         : ((long?)(((bucket5DealStats.Completions.CompletedRequestsCost / bucket5DealStats.Requests.Impressions) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();

                esCreator.AvgCPE5 = bucket5DealStats.Requests.Engagements <= 0
                                        ? null
                                        : ((long?)(((bucket5DealStats.Completions.CompletedRequestsCost / bucket5DealStats.Requests.Engagements) * 1000) * EsCreatorScales.AvgCostPerScale)).NullIfNotPositive();
            }

            // Store in es...
            var response = await _esClient.IndexAsync(esCreator, idx => idx.Index(ElasticIndexes.CreatorsAlias)
                                                                           .Id(esCreator.PublisherAccountId));

            if (!response.SuccessfulOnly())
            {
                throw response.ToException();
            }

            _log.InfoFormat("Finished PostUpdateCreatorMetrics for PublisherAccount [{0}]", publisherAccount.DisplayName());
        }

        private IEnumerable<DynDailyStatSnapshot> GetDailyStatsWithFollowerCounts(long publisherAccountId, long startDate, long endDate)
        {
            var startDateKey = string.Concat(_dailySnapshotPrefix, startDate);
            var endDateKey = string.Concat(_dailySnapshotPrefix, endDate);

            return _dynamoDb.FromQuery<DynDailyStatSnapshot>(s => s.Id == publisherAccountId &&
                                                                  Dynamo.Between(s.EdgeId, startDateKey, endDateKey))
                            .Filter(s => s.DeletedOnUtc == null &&
                                         s.TypeId == (int)DynItemType.DailyStatSnapshot)
                            .Exec()
                            .Where(p => p != null &&
                                        !p.Stats.IsNullOrEmptyRydr() &&
                                        p.Stats.ContainsKey(PublisherMetricName.Follows) &&
                                        p.Stats.ContainsKey(PublisherMetricName.FollowedBy) &&
                                        p.DayTimestamp >= startDate &&
                                        p.DayTimestamp <= endDate);
        }

        private async Task<DynDailyStatSnapshot> GetLatestStatSnapshotAsync(long publisherAccountId)
        {
            var todayUtc = _dateTimeProvider.UtcNow.Date;

            // To get the latest audience snapshot, try to get today's, then yesterday's if not available, and last get the latest one we have
            var latestDailyStatSnapshot = await _dynamoDb.GetItemAsync<DynDailyStatSnapshot>(publisherAccountId, DynDailyStatSnapshot.BuildEdgeId(todayUtc.ToUnixTimestamp()))
                                          ??
                                          await _dynamoDb.GetItemAsync<DynDailyStatSnapshot>(publisherAccountId, DynDailyStatSnapshot.BuildEdgeId(todayUtc.AddDays(-1).ToUnixTimestamp()))
                                          ??
                                          await _dynamoDb.FromQuery<DynDailyStatSnapshot>(s => s.Id == publisherAccountId &&
                                                                                               Dynamo.BeginsWith(s.EdgeId, string.Concat(DynItemType.DailyStatSnapshot, "|")))
                                                         .Filter(s => s.DeletedOnUtc != null &&
                                                                      s.TypeId == (int)DynItemType.DailyStatSnapshot)
                                                         .ExecAsync()
                                                         .FirstOrDefaultAsync();

            return latestDailyStatSnapshot;
        }

        private void CalcPriorDayGrowthValues(AudienceGrowthResult currentDay, AudienceGrowthResult nextDay)
        {
            if (currentDay == null || nextDay == null)
            {
                return;
            }

            // Basically just do the same 2 calcs for each of the metrics - growh and percentage day over day
            nextDay.FollowerPriorDayGrowth = nextDay.Followers - currentDay.Followers;

            nextDay.FollowerPriorDayGrowthPercent = currentDay.Followers == 0
                                                        ? 0
                                                        : Math.Round((nextDay.FollowerPriorDayGrowth.Value / (double)currentDay.Followers) * 100.0, 2);

            nextDay.FollowingPriorDayGrowth = nextDay.Following - currentDay.Following;

            nextDay.FollowingPriorDayGrowthPercent = currentDay.Following == 0
                                                         ? 0
                                                         : Math.Round((nextDay.FollowingPriorDayGrowth.Value / (double)currentDay.Following) * 100.0, 2);

            nextDay.OnlineFollowersPriorDayGrowth = nextDay.OnlineFollowers - currentDay.OnlineFollowers;

            nextDay.OnlineFollowersPriorDayGrowthPercent = currentDay.OnlineFollowers == 0
                                                               ? 0
                                                               : Math.Round((nextDay.OnlineFollowersPriorDayGrowth.Value / (double)currentDay.OnlineFollowers) * 100.0, 2);

            nextDay.ImpressionsPriorDayGrowth = nextDay.Impressions - currentDay.Impressions;

            nextDay.ImpressionsPriorDayGrowthPercent = currentDay.Impressions == 0
                                                           ? 0
                                                           : Math.Round((nextDay.ImpressionsPriorDayGrowth.Value / (double)currentDay.Impressions) * 100.0, 2);

            nextDay.ReachPriorDayGrowth = nextDay.Reach - currentDay.Reach;

            nextDay.ReachPriorDayGrowthPercent = currentDay.Reach == 0
                                                     ? 0
                                                     : Math.Round((nextDay.ReachPriorDayGrowth.Value / (double)currentDay.Reach) * 100.0, 2);

            nextDay.ProfileViewsPriorDayGrowth = nextDay.ProfileViews - currentDay.ProfileViews;

            nextDay.ProfileViewsPriorDayGrowthPercent = currentDay.ProfileViews == 0
                                                            ? 0
                                                            : Math.Round((nextDay.ProfileViewsPriorDayGrowth.Value / (double)currentDay.ProfileViews) * 100.0, 2);

            nextDay.WebsitePriorDayGrowth = nextDay.WebsiteClicks - currentDay.WebsiteClicks;

            nextDay.WebsitePriorDayGrowthPercent = currentDay.WebsiteClicks == 0
                                                       ? 0
                                                       : Math.Round((nextDay.WebsitePriorDayGrowth.Value / (double)currentDay.WebsiteClicks) * 100.0, 2);

            nextDay.EmailPriorDayGrowth = nextDay.EmailContacts - currentDay.EmailContacts;

            nextDay.EmailPriorDayGrowthPercent = currentDay.EmailContacts == 0
                                                     ? 0
                                                     : Math.Round((nextDay.EmailPriorDayGrowth.Value / (double)currentDay.EmailContacts) * 100.0, 2);

            nextDay.GetDirectionClicksPriorDayGrowth = nextDay.GetDirectionClicks - currentDay.GetDirectionClicks;

            nextDay.GetDirectionClicksPriorDayGrowthPercent = currentDay.GetDirectionClicks == 0
                                                                  ? 0
                                                                  : Math.Round((nextDay.GetDirectionClicksPriorDayGrowth.Value / (double)currentDay.GetDirectionClicks) * 100.0, 2);

            nextDay.PhoneCallClicksPriorDayGrowth = nextDay.PhoneCallClicks - currentDay.PhoneCallClicks;

            nextDay.PhoneCallClicksPriorDayGrowthPercent = currentDay.PhoneCallClicks == 0
                                                               ? 0
                                                               : Math.Round((nextDay.PhoneCallClicksPriorDayGrowth.Value / (double)currentDay.PhoneCallClicks) * 100.0, 2);

            nextDay.TextMessageClicksPriorDayGrowth = nextDay.TextMessageClicks - currentDay.TextMessageClicks;

            nextDay.TextMessageClicksPriorDayGrowthPercent = currentDay.TextMessageClicks == 0
                                                                 ? 0
                                                                 : Math.Round((nextDay.TextMessageClicksPriorDayGrowth.Value / (double)currentDay.TextMessageClicks) * 100.0, 2);
        }

        private async Task<DataPublisherDealStats> GetPublisherDealStatsAsync(long publisherAccountId, int minDealValueExclusive, int maxDealValueInclusive)
        {
            // Now get the costs and counts for the deals the account has requested/completed
            var lifetimeEnumId = _rydrDataService.GetOrCreateRydrEnumId("lifetime");

            var dataResult = await _rydrDataService.QueryMultipleAsync(@"
-- Request metrics
SELECT	COUNT(DISTINCT dr.DealId) AS Requests,
		SUM(CASE WHEN se.Name = 'impressions' THEN ms.Value END) AS Impressions,
		SUM(CASE WHEN se.Name = 'reach' THEN ms.Value END) AS Reach,
		SUM(CASE WHEN se.Name = 'engagement' THEN ms.Value END) AS Engagements
FROM    DealRequests dr
JOIN    Deals d
ON      dr.DealId = d.Id
LEFT JOIN
		DealRequestMedia drm
ON		dr.DealId = drm.DealId
		AND dr.PublisherAccountId = drm.PublisherAccountId
LEFT JOIN
		MediaStats ms
ON		drm.PublisherAccountId = ms.PublisherAccountId
		AND drm.MediaId = ms.MediaId
        AND ms.PeriodEnumId = @PeriodEnumId
        AND ms.EndTime = @LifetimeEndTime
LEFT JOIN
		Enums se
ON		ms.StatEnumId = se.Id
LEFT JOIN
		Enums sp
ON		ms.PeriodEnumId = sp.Id
WHERE	dr.PublisherAccountId = @PublisherAccountId
        AND d.Value > @MinDealValue
        AND d.Value <= @MaxDealValue;

-- Deal completion metrics
SELECT	SUM(d.Value) AS CompletedRequestsCost,
		COUNT(*) AS CompletedRequests
FROM	Deals d
WHERE	d.Value > @MinDealValue
        AND d.Value <= @MaxDealValue
        AND EXISTS
		(
        SELECT	NULL
        FROM	DealRequestMedia drm
		WHERE	drm.DealId = d.Id
			    AND drm.PublisherAccountId = @PublisherAccountId
        );
",
                                                                       new
                                                                       {
                                                                           PublisherAccountId = publisherAccountId,
                                                                           MinDealValue = minDealValueExclusive,
                                                                           MaxDealValue = maxDealValueInclusive,
                                                                           PeriodEnumId = lifetimeEnumId,
                                                                           LifetimeEndTime = FbIgInsights.LifetimeEndTime.ToDateTime()
                                                                       },
                                                                       data => new DataPublisherDealStats
                                                                               {
                                                                                   Requests = data.ReadOrDefault<DataPublisherDealRequestStats>(),
                                                                                   Completions = data.ReadOrDefault<DataPublisherDealCompletionStats>()
                                                                               });

            return dataResult;
        }

        private class DataPublisherDealStats
        {
            public DataPublisherDealRequestStats Requests { get; set; }
            public DataPublisherDealCompletionStats Completions { get; set; }
        }

        private class DataPublisherDealRequestStats
        {
            public int Requests { get; set; }
            public long Impressions { get; set; }
            public long Reach { get; set; }
            public long Engagements { get; set; }
        }

        private class DataPublisherDealCompletionStats
        {
            public double CompletedRequestsCost { get; set; }
            public int CompletedRequests { get; set; }
        }
    }
}
