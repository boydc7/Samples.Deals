using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;
using DoubleRange = Rydr.Api.Dto.Shared.DoubleRange;
using LongRange = Rydr.Api.Dto.Shared.LongRange;

namespace Rydr.Api.Core.DataAccess
{
    public class ElasticSearchService : IElasticSearchService
    {
        private static readonly bool _honorEngagementRestrictions = RydrEnvironment.GetAppSetting("DealRestrictions.HonorEngagementFilter", true);
        private static readonly bool _honorAgeRestrictions = RydrEnvironment.GetAppSetting("DealRestrictions.HonorAgeFilter", false);

        private static readonly HashSet<char> _leadingSearchReplacementChars = new HashSet<char>
                                                                               {
                                                                                   '#',
                                                                                   '@'
                                                                               };

        private static readonly ILog _log = LogManager.GetLogger("ElasticSearchService");

        // private static readonly bool _useWildcardQueries = RydrEnvironment.GetAppSetting("Elastic.UseWildcards", false);
        private static readonly bool _logElasticRequestBodies = RydrEnvironment.GetAppSetting("Elastic.LogRequestBody", false);

        private readonly IElasticClient _elasticClient;

        public ElasticSearchService(IElasticClient elasticClient)
        {
            _elasticClient = elasticClient;
        }

        public async Task<EsSearchResult<EsCreator>> SearchCreatorIdsAsync(EsCreatorSearch creatorSearch)
        {
            var sd = GetBaseEsCreatorSearchDescriptor(creatorSearch).From(creatorSearch.Skip.Gz(0))
                                                                    .Size(creatorSearch.Take.Gz(50))
                                                                    .Source(d => d.Includes(fd => fd.Fields(esl => esl.PublisherAccountId,
                                                                                                            esl => esl.AccountId,
                                                                                                            esl => esl.PublisherType,
                                                                                                            esl => esl.LastLocation,
                                                                                                            esl => esl.LastLocationModifiedOn)))
                                                                    .Sort(s => s.Descending(e => e.PublisherAccountId));

            var response = await _elasticClient.SearchAsync<EsCreator>(sd);

            return ToEsResult2(response, r => r.Hits.Select(h => h.Source));
        }

        public async Task<EsSearchResult<EsBusiness>> SearchBusinessIdsAsync(EsBusinessSearch businessSearch)
        {
            var geoLocation = businessSearch.IsValidLatLon()
                                  ? GeoLocation.TryCreate(businessSearch.Latitude.Value,
                                                          businessSearch.Longitude.Value)
                                  : null;

            var validGeoLocationAndRange = geoLocation != null && businessSearch.Miles.HasValue && businessSearch.Miles.Value > 0;

            IEnumerable<Func<QueryContainerDescriptor<EsBusiness>, QueryContainer>> getFilters()
            {
                yield return f => f.Term(p => p.IsDeleted, false);

                if (!businessSearch.Tags.IsNullOrEmpty())
                {
                    if (businessSearch.Tags.Count == 1)
                    {
                        yield return f => f.Term(p => p.Tags, businessSearch.Tags.Single().ToString());
                    }
                    else
                    {
                        yield return f => f.Terms(t => t.Field(d => d.Tags)
                                                        .Terms(businessSearch.Tags.Select(g => g.ToString())));
                    }
                }

                if (businessSearch.PublisherAccountId > 0)
                {
                    yield return f => f.Term(c => c.PublisherAccountId, businessSearch.PublisherAccountId);
                }

                if (validGeoLocationAndRange)
                {
                    yield return f => f.GeoDistance(gq => gq.Distance(businessSearch.Miles.Value, DistanceUnit.Miles)
                                                            .DistanceType(GeoDistanceType.Plane)
                                                            .Location(geoLocation)
                                                            .ValidationMethod(GeoValidationMethod.IgnoreMalformed)
                                                            .Field(d => d.Location));
                }
            }

            IEnumerable<Func<QueryContainerDescriptor<EsBusiness>, QueryContainer>> getShoulds()
            {
                if (businessSearch.Search.IsNullOrEmpty())
                {
                    yield return s => s.MatchAll();

                    yield break;
                }

                yield return s => s.SimpleQueryString(smd => smd.Fields(d => d.Field(e => e.SearchValue))
                                                                .Lenient()
                                                                .Query(FormatSearchStringNoWildcards(businessSearch.Search, true)));
            }

            var sd = new SearchDescriptor<EsBusiness>().Index(ElasticIndexes.BusinessesAlias)
                                                       .Query(qcd => qcd.Bool(bqd => bqd.Filter(getFilters())
                                                                                        .Should(getShoulds())
                                                                                        .MinimumShouldMatch(MinimumShouldMatch.Fixed(1))))
                                                       .From(businessSearch.Skip.Gz(0))
                                                       .Size(businessSearch.Take.Gz(50))
                                                       .Sort(d => d.Descending(eb => eb.PublisherAccountId))
                                                       .Source(d => d.Includes(fd => fd.Fields(esl => esl.PublisherAccountId,
                                                                                               esl => esl.AccountId,
                                                                                               esl => esl.PublisherType)));

            var response = await _elasticClient.SearchAsync<EsBusiness>(sd);

            return ToEsResult2(response, r => r.Hits.Select(h => h.Source));
        }

        public async Task<EsSearchResult<EsCreatorStatAggregate>> SearchAggregateCreatorsAsync(EsCreatorSearch creatorSearch)
        {
            var sd = GetBaseEsCreatorSearchDescriptor(creatorSearch).Size(0)
                                                                    .Aggregations(a => a.ExtendedStats("mediacount", c => c.Field(f => f.MediaCount))
                                                                                        .ExtendedStats("followedby", c => c.Field(f => f.FollowedBy))
                                                                                        .ExtendedStats("following", c => c.Field(f => f.Following))
                                                                                        .ExtendedStats("storyengagementrating", c => c.Field(f => f.StoryEngagementRating))
                                                                                        .ExtendedStats("storyimpressions", c => c.Field(f => f.StoryImpressions))
                                                                                        .ExtendedStats("storyreach", c => c.Field(f => f.StoryReach))
                                                                                        .ExtendedStats("storyactions", c => c.Field(f => f.StoryActions))
                                                                                        .ExtendedStats("stories", c => c.Field(f => f.Stories))
                                                                                        .ExtendedStats("mediaengagementrating", c => c.Field(f => f.MediaEngagementRating))
                                                                                        .ExtendedStats("mediatrueengagementrating", c => c.Field(f => f.MediaTrueEngagementRating))
                                                                                        .ExtendedStats("mediaimpressions", c => c.Field(f => f.MediaImpressions))
                                                                                        .ExtendedStats("mediareach", c => c.Field(f => f.MediaReach))
                                                                                        .ExtendedStats("mediaactions", c => c.Field(f => f.MediaActions))
                                                                                        .ExtendedStats("medias", c => c.Field(f => f.Medias))
                                                                                        .ExtendedStats("avgstoryimpressions", c => c.Field(f => f.AvgStoryImpressions))
                                                                                        .ExtendedStats("avgmediaimpressions", c => c.Field(f => f.AvgMediaImpressions))
                                                                                        .ExtendedStats("avgstoryreach", c => c.Field(f => f.AvgStoryReach))
                                                                                        .ExtendedStats("avgmediareach", c => c.Field(f => f.AvgMediaReach))
                                                                                        .ExtendedStats("avgstoryactions", c => c.Field(f => f.AvgStoryActions))
                                                                                        .ExtendedStats("avgmediaactions", c => c.Field(f => f.AvgMediaActions))
                                                                                        .ExtendedStats("follower7dayjitter", c => c.Field(f => f.Follower7DayJitter))
                                                                                        .ExtendedStats("follower14dayjitter", c => c.Field(f => f.Follower14DayJitter))
                                                                                        .ExtendedStats("follower30dayjitter", c => c.Field(f => f.Follower30DayJitter))
                                                                                        .ExtendedStats("audienceusa", c => c.Field(f => f.AudienceUsa))
                                                                                        .ExtendedStats("audienceenglish", c => c.Field(f => f.AudienceEnglish))
                                                                                        .ExtendedStats("audiencespanish", c => c.Field(f => f.AudienceSpanish))
                                                                                        .ExtendedStats("audiencemale", c => c.Field(f => f.AudienceMale))
                                                                                        .ExtendedStats("audiencefemale", c => c.Field(f => f.AudienceFemale))
                                                                                        .ExtendedStats("audienceage1317", c => c.Field(f => f.AudienceAge1317))
                                                                                        .ExtendedStats("audienceage18up", c => c.Field(f => f.AudienceAge18Up))
                                                                                        .ExtendedStats("audienceage1824", c => c.Field(f => f.AudienceAge1824))
                                                                                        .ExtendedStats("audienceage25up", c => c.Field(f => f.AudienceAge25Up))
                                                                                        .ExtendedStats("audienceage2534", c => c.Field(f => f.AudienceAge2534))
                                                                                        .ExtendedStats("audienceage3544", c => c.Field(f => f.AudienceAge3544))
                                                                                        .ExtendedStats("audienceage4554", c => c.Field(f => f.AudienceAge4554))
                                                                                        .ExtendedStats("audienceage5564", c => c.Field(f => f.AudienceAge5564))
                                                                                        .ExtendedStats("audienceage65up", c => c.Field(f => f.AudienceAge65Up))
                                                                                        .ExtendedStats("imagesavgage", c => c.Field(f => f.ImagesAvgAge))
                                                                                        .ExtendedStats("suggestiverating", c => c.Field(f => f.SuggestiveRating))
                                                                                        .ExtendedStats("violencerating", c => c.Field(f => f.ViolenceRating))
                                                                                        .ExtendedStats("rydr7dayactivityrating", c => c.Field(f => f.Rydr7DayActivityRating))
                                                                                        .ExtendedStats("rydr14dayactivityrating", c => c.Field(f => f.Rydr14DayActivityRating))
                                                                                        .ExtendedStats("rydr30dayactivityrating", c => c.Field(f => f.Rydr30DayActivityRating))
                                                                                        .ExtendedStats("requests", c => c.Field(f => f.Requests))
                                                                                        .ExtendedStats("completedrequests", c => c.Field(f => f.CompletedRequests))
                                                                                        .ExtendedStats("avgcpmr", c => c.Field(f => f.AvgCPMr))
                                                                                        .ExtendedStats("avgcpmi", c => c.Field(f => f.AvgCPMi))
                                                                                        .ExtendedStats("avgcpe", c => c.Field(f => f.AvgCPE))
                                                                                        .ExtendedStats("requests1", c => c.Field(f => f.Requests1))
                                                                                        .ExtendedStats("completedrequests1", c => c.Field(f => f.CompletedRequests1))
                                                                                        .ExtendedStats("avgcpmr1", c => c.Field(f => f.AvgCPMr1))
                                                                                        .ExtendedStats("avgcpmi1", c => c.Field(f => f.AvgCPMi1))
                                                                                        .ExtendedStats("avgcpe1", c => c.Field(f => f.AvgCPE1))
                                                                                        .ExtendedStats("requests2", c => c.Field(f => f.Requests2))
                                                                                        .ExtendedStats("completedrequests2", c => c.Field(f => f.CompletedRequests2))
                                                                                        .ExtendedStats("avgcpmr2", c => c.Field(f => f.AvgCPMr2))
                                                                                        .ExtendedStats("avgcpmi2", c => c.Field(f => f.AvgCPMi2))
                                                                                        .ExtendedStats("avgcpe2", c => c.Field(f => f.AvgCPE2))
                                                                                        .ExtendedStats("requests3", c => c.Field(f => f.Requests3))
                                                                                        .ExtendedStats("completedrequests3", c => c.Field(f => f.CompletedRequests3))
                                                                                        .ExtendedStats("avgcpmr3", c => c.Field(f => f.AvgCPMr3))
                                                                                        .ExtendedStats("avgcpmi3", c => c.Field(f => f.AvgCPMi3))
                                                                                        .ExtendedStats("avgcpe3", c => c.Field(f => f.AvgCPE3))
                                                                                        .ExtendedStats("requests4", c => c.Field(f => f.Requests4))
                                                                                        .ExtendedStats("completedrequests4", c => c.Field(f => f.CompletedRequests4))
                                                                                        .ExtendedStats("avgcpmr4", c => c.Field(f => f.AvgCPMr4))
                                                                                        .ExtendedStats("avgcpmi4", c => c.Field(f => f.AvgCPMi4))
                                                                                        .ExtendedStats("avgcpe4", c => c.Field(f => f.AvgCPE4))
                                                                                        .ExtendedStats("requests5", c => c.Field(f => f.Requests5))
                                                                                        .ExtendedStats("completedrequests5", c => c.Field(f => f.CompletedRequests5))
                                                                                        .ExtendedStats("avgcpmr5", c => c.Field(f => f.AvgCPMr5))
                                                                                        .ExtendedStats("avgcpmi5", c => c.Field(f => f.AvgCPMi5))
                                                                                        .ExtendedStats("avgcpe5", c => c.Field(f => f.AvgCPE5)));

            var response = await _elasticClient.SearchAsync<EsCreator>(sd);

            return ToEsResult2(response, r =>
                                         {
                                             var aggregate = r.Aggregations
                                                              .Select(a => (a.Key, Stats: response.Aggregations.ExtendedStats(a.Key)))
                                                              .ToDictionary(t => t.Key, t => t.Stats)
                                                              .ToJson()
                                                              .FromJson<EsCreatorStatAggregate>();

                                             if (aggregate == null)
                                             {
                                                 return null;
                                             }

                                             aggregate.Creators = r.Total;

                                             return new[]
                                                    {
                                                        aggregate
                                                    };
                                         });
        }

        public async Task<EsSearchResult<EsDeal>> SearchDealsAsync(EsDealSearch esDealSearch)
        {
            Func<QueryContainerDescriptor<EsDeal>, QueryContainer> intRangeFilter<TValue>(Func<EsDealSearch, IntRange> forRange,
                                                                                          Expression<Func<EsDeal, TValue>> onField)
                => GetIntRangeFilterOrNull(esDealSearch, forRange, onField);

            Func<QueryContainerDescriptor<EsDeal>, QueryContainer> longRangeFilter<TValue>(Func<EsDealSearch, LongRange> forRange,
                                                                                           Expression<Func<EsDeal, TValue>> onField)
                => GetLongRangeFilterOrNull(esDealSearch, forRange, onField);

            Func<QueryContainerDescriptor<EsDeal>, QueryContainer> doubleRangeFilter<TValue>(Func<EsDealSearch, DoubleRange> forRange,
                                                                                             Expression<Func<EsDeal, TValue>> onField)
                => GetDoubleRangeFilterOrNull(esDealSearch, forRange, onField);

            IEnumerable<Func<QueryContainerDescriptor<EsDeal>, QueryContainer>> getFilters()
            {
                if (esDealSearch.DealId > 0)
                {
                    yield return f => f.Term(t => t.DealId, esDealSearch.DealId);
                }

                if (esDealSearch.WorkspaceId > GlobalItemIds.MinUserDefinedObjectId)
                {
                    yield return f => f.Term(ed => ed.WorkspaceId, esDealSearch.WorkspaceId);
                }

                if (esDealSearch.ContextWorkspaceId.HasValue && esDealSearch.ContextWorkspaceId.Value > GlobalItemIds.MinUserDefinedObjectId)
                {
                    yield return f => f.Terms(t => t.Field(d => d.ContextWorkspaceId)
                                                    .Terms(esDealSearch.WorkspaceId, esDealSearch.ContextWorkspaceId.Value));
                }

                // PublisherAccountId is a NOT filter...

                if (esDealSearch.DealPublisherAccountId > 0)
                {
                    yield return f => f.Term(ed => ed.PublisherAccountId, esDealSearch.DealPublisherAccountId);
                }

                // GEO filters....
                if (esDealSearch.BoundingBox.IsValidElasticGeoBoundingBox())
                {
                    yield return f => f.GeoBoundingBox(bq => bq.BoundingBox(esDealSearch.BoundingBox.NorthWestLatitude,
                                                                            esDealSearch.BoundingBox.NorthWestLongitude,
                                                                            esDealSearch.BoundingBox.SouthEastLatitude,
                                                                            esDealSearch.BoundingBox.SouthEastLongitude)
                                                               .Type(GeoExecution.Memory)
                                                               .ValidationMethod(GeoValidationMethod.IgnoreMalformed)
                                                               .Field(d => d.Location));
                }
                else
                {
                    var geoLocation = esDealSearch.IsValidLatLon() && esDealSearch.Miles.HasValue && esDealSearch.Miles.Value > 0
                                          ? GeoLocation.TryCreate(esDealSearch.Latitude.Value, esDealSearch.Longitude.Value)
                                          : null;

                    if (geoLocation != null)
                    {
                        yield return f => f.GeoDistance(gq => gq.Distance(esDealSearch.Miles.Value, DistanceUnit.Miles)
                                                                .DistanceType(GeoDistanceType.Plane)
                                                                .Location(geoLocation)
                                                                .ValidationMethod(GeoValidationMethod.IgnoreMalformed)
                                                                .Field(d => d.Location));
                    }
                    else if (esDealSearch.BoundingBox.IsValidGeoBoundingBox())
                    {
                        var miles = GeoExtensions.DistanceBetween(geoLocation.Latitude, geoLocation.Longitude, esDealSearch.BoundingBox);

                        if (miles.HasValue && miles > 0)
                        {
                            yield return f => f.GeoDistance(gq => gq.Distance(miles.Value, DistanceUnit.Miles)
                                                                    .DistanceType(GeoDistanceType.Plane)
                                                                    .Location(geoLocation)
                                                                    .ValidationMethod(GeoValidationMethod.IgnoreMalformed)
                                                                    .Field(d => d.Location));
                        }
                    }
                }

                // Search is a should filter...

                if (esDealSearch.PlaceId > 0)
                {
                    yield return f => f.Term(p => p.PlaceId, esDealSearch.PlaceId);
                }

                if (!esDealSearch.IncludeInactive)
                {
                    yield return f => f.Term(p => p.IsDeleted, false);
                }

                if (!esDealSearch.IncludeExpired)
                {
                    yield return f => f.LongRange(lr => lr.Field(p => p.ExpiresOn)
                                                          .GreaterThanOrEquals(DateTimeHelper.UtcNowTs));
                }

                if (esDealSearch.PrivateDealOption != PrivateDealOption.All)
                {
                    if (esDealSearch.PrivateDealOption == PrivateDealOption.PrivateOnly)
                    { // Asked for private deals only - get those that are private and available to the given publisher
                        yield return f => f.Term(t => t.IsPrivateDeal, true) &&
                                          f.Term(t => t.InvitedPublisherAccountIds, esDealSearch.PublisherAccountId);
                    }
                    else if (esDealSearch.PrivateDealOption == PrivateDealOption.PublicOnly)
                    { // Only non-private deals
                        yield return f => f.Term(t => t.IsPrivateDeal, false);
                    }
                    else
                    { // Public and private - so get public deals and any private deals this user was invited to
                        yield return f => f.Term(t => t.IsPrivateDeal, false) ||
                                          f.Term(t => t.InvitedPublisherAccountIds, esDealSearch.PublisherAccountId);
                    }
                }

                if (esDealSearch.DealStatuses != null && esDealSearch.DealStatuses.Length > 0)
                {
                    if (esDealSearch.DealStatuses.Length == 1)
                    {
                        yield return f => f.Term(p => p.DealStatus, (int)esDealSearch.DealStatuses.Single());
                    }
                    else
                    {
                        yield return f => f.Terms(t => t.Field(d => d.DealStatus)
                                                        .Terms(esDealSearch.DealStatuses.Select(s => (int)s)));
                    }
                }

                // ExcludeGroupIds is a NOT filter...

                if (!esDealSearch.DealTypes.IsNullOrEmpty())
                {
                    if (esDealSearch.DealTypes.Length == 1)
                    {
                        yield return f => f.Term(p => p.DealType, (int)esDealSearch.DealTypes.Single());
                    }
                    else
                    {
                        yield return f => f.Terms(t => t.Field(d => d.DealType)
                                                        .Terms(esDealSearch.DealTypes.Select(s => (int)s)));
                    }
                }

                if (!esDealSearch.Tags.IsNullOrEmptyRydr())
                {
                    if (esDealSearch.Tags.Count == 1)
                    {
                        yield return f => f.Term(p => p.Tags, esDealSearch.Tags.Single().ToString());
                    }
                    else
                    {
                        yield return f => f.Terms(t => t.Field(d => d.Tags)
                                                        .Terms(esDealSearch.Tags.Select(g => g.ToString())));
                    }
                }

                if (_honorAgeRestrictions)
                {
                    yield return intRangeFilter(s => s.AgeRange, c => c.MinAge);
                }

                yield return longRangeFilter(s => s.FollowerCount, c => c.MinFollowerCount);

                if (_honorEngagementRestrictions)
                {
                    yield return doubleRangeFilter(s => s.EngagementRating, c => c.MinEngagementRating);
                }

                yield return doubleRangeFilter(s => s.Value, c => c.Value);
                yield return intRangeFilter(s => s.RequestCount, c => c.RequestCount);
                yield return intRangeFilter(s => s.RemainingQuantity, c => c.RemainingQuantity);
                yield return longRangeFilter(s => s.CreatedBetween, c => c.CreatedOn);
                yield return longRangeFilter(s => s.PublishedBetween, c => c.PublishedOn);
            }

            IEnumerable<Func<QueryContainerDescriptor<EsDeal>, QueryContainer>> getMustNots()
            {
                if (esDealSearch.Tags != null && esDealSearch.Tags.Count <= 0)
                {   // Searching for deals that have no tags
                    yield return d => d.Exists(x => x.Field(f => f.Tags));
                }

                // Never include any deals the requester has already requested in the past
                if (esDealSearch.PublisherAccountId > 0)
                {
                    yield return n => n.Term(p => p.RequestedByPublisherAccountIds, esDealSearch.PublisherAccountId);

                    if (esDealSearch.DealPublisherAccountId != esDealSearch.PublisherAccountId)
                    { // Do not return the publisher's own deals
                        yield return n => n.Term(p => p.PublisherAccountId, esDealSearch.PublisherAccountId);
                    }
                }

                if (!esDealSearch.ExcludeGroupIds.IsNullOrEmptyReadOnly())
                {
                    if (esDealSearch.ExcludeGroupIds.Count == 1)
                    {
                        yield return f => f.Term(p => p.GroupId, esDealSearch.ExcludeGroupIds.Single());
                    }
                    else
                    {
                        yield return f => f.Terms(t => t.Field(d => d.GroupId)
                                                        .Terms(esDealSearch.ExcludeGroupIds));
                    }
                }
            }

            IEnumerable<Func<QueryContainerDescriptor<EsDeal>, QueryContainer>> getShoulds()
            {
                if (esDealSearch.Search.IsNullOrEmpty())
                {
                    yield return s => s.MatchAll();

                    yield break;
                }

                yield return s => s.SimpleQueryString(smd => smd.Fields(d => d.Field(p => p.SearchValue))
                                                                .Lenient()
                                                                .Query(FormatSearchStringNoWildcards(esDealSearch.Search, true)));
            }

            Func<SortDescriptor<EsDeal>, IPromise<IList<ISort>>> getSort()
            {
                if (esDealSearch.Sort == DealSort.Closest || esDealSearch.Sort == DealSort.Default && esDealSearch.IsValidUserLatLon())
                {   // Specified closest or nothing as the search, see if we have a valid user location, and if so sort by geoLocation closest to furthest
                    var userGeoLocation = GeoLocation.TryCreate(esDealSearch.UserLatitude.Value, esDealSearch.UserLongitude.Value);

                    if (userGeoLocation != null)
                    {
                        return s => s.GeoDistance(gds => gds.Field(d => d.Location)
                                                            .DistanceType(GeoDistanceType.Plane)
                                                            .IgnoreUnmapped()
                                                            .Unit(DistanceUnit.Miles)
                                                            .Points(userGeoLocation)
                                                            .Ascending())
                                     .Descending(d => d.MinFollowerCount)
                                     .Descending(d => d.DealId);
                    }
                }

                // Don't have a location specified or asked to sort non-geo
                return esDealSearch.Sort switch
                {
                    DealSort.FollowerValue => (s => s.Descending(d => d.MinFollowerCount)
                                                     .Ascending(d => d.Value)
                                                     .Descending(d => d.DealId)),
                    DealSort.Expiring => (s => s.Ascending(d => d.ExpiresOn)
                                                .Descending(d => d.MinFollowerCount)
                                                .Descending(d => d.DealId)),
                    _ => (s => s.Descending(d => d.PublishedOn)
                                .Descending(d => d.DealId))
                };
            }

            var searchDescriptor = new SearchDescriptor<EsDeal>().Index(ElasticIndexes.DealsAlias)
                                                                 .Query(qcd => qcd.Bool(bqd => bqd.Filter(getFilters().Where(gf => gf != null))
                                                                                                  .MustNot(getMustNots())
                                                                                                  .Should(getShoulds())
                                                                                                  .MinimumShouldMatch(MinimumShouldMatch.Fixed(1))));

            // Default to the standard, non-grouped result selector
            Func<ISearchResponse<EsDeal>, IEnumerable<EsDeal>> resultSelector = r => r.Hits.Select(h => h.Source);

            if (esDealSearch.Grouping == DealSearchGroupOption.None)
            { // No grouping of results - standard search
                searchDescriptor = searchDescriptor.From(esDealSearch.Skip.Gz(0))
                                                   .Size(esDealSearch.Take.Gz(100))
                                                   .Source(sd => esDealSearch.IdsOnly
                                                                     ? sd.Includes(fd => fd.Fields(esl => esl.DealId,
                                                                                                   esl => esl.PublisherAccountId))
                                                                     : sd.IncludeAll())
                                                   .Sort(getSort());
            }
            else if (esDealSearch.Grouping == DealSearchGroupOption.Tags)
            {   // Tag grouping groups by tags then the standard dealGroup inside that, returning deals grouped by tag for the requestor...
                resultSelector = r => r.Aggregations
                                       .Terms("tags")
                                       .Buckets
                                       .SelectMany(t => t.Terms("dealGroups")
                                                         .Buckets
                                                         .SelectMany(b => b.TopHits("topHits")
                                                                           .Hits<EsDeal>()
                                                                           .Select(h => h.Source)
                                                                           .Where(d => d != null)));

                searchDescriptor = searchDescriptor.Size(0)
                                                   .Aggregations(a => a.Terms("tags",
                                                                              td => td.Field(f => f.Tags)
                                                                                      .Size(esDealSearch.Take.Gz(25))
                                                                                      .CollectMode(TermsAggregationCollectMode.BreadthFirst)
                                                                                      .Order(to => to.CountDescending())
                                                                                      .Aggregations(ga => ga.Terms("dealGroups",
                                                                                                                   dg => dg.Field(f => f.GroupId)
                                                                                                                           .Size(esDealSearch.Take.Gz(25))
                                                                                                                           .CollectMode(TermsAggregationCollectMode.BreadthFirst)
                                                                                                                           .Order(to => to.Descending("topHit"))
                                                                                                                           .Aggregations(ta => ta.TopHits("topHits",
                                                                                                                                                          th => th.Size(1)
                                                                                                                                                                  .Sort(getSort())
                                                                                                                                                                  .Source(sd => esDealSearch.IdsOnly
                                                                                                                                                                                    ? sd.Includes(fd => fd.Fields(esl => esl.DealId,
                                                                                                                                                                                                                  esl => esl.PublisherAccountId))
                                                                                                                                                                                    : sd.IncludeAll()))
                                                                                                                                                 .Max("topHit",
                                                                                                                                                      m => m.Field(new Field(esDealSearch.Sort == DealSort.FollowerValue
                                                                                                                                                                                 ? "minFollowerCount"
                                                                                                                                                                                 : "publishedOn"))))))));
            }
            else if (esDealSearch.Sort == DealSort.Closest || esDealSearch.Sort == DealSort.Expiring)
            { // Location and expiration date cannot change for a deal group, use a more efficient and pageable collapsing search to group and return
                searchDescriptor = searchDescriptor.Collapse(cd => cd.Field(f => f.GroupId))
                                                   .From(esDealSearch.Skip.Gz(0))
                                                   .Size(esDealSearch.Take.Gz(100))
                                                   .Source(sd => esDealSearch.IdsOnly
                                                                     ? sd.Includes(fd => fd.Fields(esl => esl.DealId,
                                                                                                   esl => esl.PublisherAccountId))
                                                                     : sd.IncludeAll())
                                                   .Sort(getSort());
            }
            else
            { // Sorting on something that can be different inside a group, so use an aggregate query - currently the only other sort option here is MinFollowerCount
                resultSelector = r => r.Aggregations
                                       .Terms("dealGroups")
                                       .Buckets
                                       .SelectMany(b => b.TopHits("topHits")
                                                         .Hits<EsDeal>()
                                                         .Select(h => h.Source)
                                                         .Where(d => d != null));

                searchDescriptor = searchDescriptor.Size(0)
                                                   .Aggregations(a => a.Terms("dealGroups",
                                                                              td => td.Field(f => f.GroupId)
                                                                                      .Size(esDealSearch.Take.Gz(100))
                                                                                      .CollectMode(TermsAggregationCollectMode.BreadthFirst)
                                                                                      .Order(to => to.Descending("topHit"))
                                                                                      .Aggregations(ac => ac.TopHits("topHits",
                                                                                                                     ta => ta.Size(1)
                                                                                                                             .Sort(getSort())
                                                                                                                             .Source(sd => esDealSearch.IdsOnly
                                                                                                                                               ? sd.Includes(fd => fd.Fields(esl => esl.DealId,
                                                                                                                                                                             esl => esl.PublisherAccountId))
                                                                                                                                               : sd.IncludeAll()))
                                                                                                            .Max("topHit", m => m.Field(new Field(esDealSearch.Sort == DealSort.FollowerValue
                                                                                                                                                      ? "minFollowerCount"
                                                                                                                                                      : "publishedOn"))))));
            }

            var response = await _elasticClient.SearchAsync<EsDeal>(searchDescriptor);

            return ToEsResult(response, resultSelector);
        }

        public async Task<EsSearchResult<EsMedia>> SearchMediaAsync(PublisherAccountMediaVisionSectionSearchDescriptor request)
        {
            Func<QueryContainerDescriptor<EsMedia>, QueryContainer> intRangeFilter<TValue>(Func<PublisherAccountMediaVisionSectionSearchDescriptor, IntRange> forRange,
                                                                                           Expression<Func<EsMedia, TValue>> onField)
                => GetIntRangeFilterOrNull(request, forRange, onField);

            Func<QueryContainerDescriptor<EsMedia>, QueryContainer> longRangeFilter<TValue>(Func<PublisherAccountMediaVisionSectionSearchDescriptor, LongRange> forRange,
                                                                                            Expression<Func<EsMedia, TValue>> onField)
                => GetLongRangeFilterOrNull(request, forRange, onField);

            IEnumerable<Func<QueryContainerDescriptor<EsMedia>, QueryContainer>> getFilters()
            {
                yield return f => f.Term(t => t.PublisherAccountId, request.PublisherAccountId);

                if (!request.Sentiments.IsNullOrEmpty())
                {
                    yield return f => f.Terms(t => t.Field(d => d.Sentiment)
                                                    .Terms(request.Sentiments));
                }

                if (request.ContentType != PublisherContentType.Unknown)
                {
                    yield return f => f.Term(t => t.ContentType, (int)request.ContentType);
                }

                yield return longRangeFilter(s => s.FacesRange, c => c.ImageFacesCount);
                yield return intRangeFilter(s => s.FacesAvgAgeRange, c => c.ImageFacesAgeAvg);
                yield return intRangeFilter(s => s.FacesMalesRange, c => c.ImageFacesMales);
                yield return intRangeFilter(s => s.FacesFemalesRange, c => c.ImageFacesFemales);
                yield return intRangeFilter(s => s.FacesSmilesRange, c => c.ImageFacesSmiles);
                yield return intRangeFilter(s => s.FacesBeardsRange, c => c.ImageFacesBeards);
                yield return intRangeFilter(s => s.FacesMustachesRange, c => c.ImageFacesMustaches);
                yield return intRangeFilter(s => s.FacesEyeglassesRange, c => c.ImageFacesEyeglasses);
                yield return intRangeFilter(s => s.FacesSunglassesRange, c => c.ImageFacesSunglasses);
            }

            var searchDescriptor = new SearchDescriptor<EsMedia>().Index(ElasticIndexes.MediaAlias)
                                                                  .Query(q => q.SimpleQueryString(qs => qs.Fields(d => d.Fields("searchValue", "tags"))
                                                                                                          .Lenient()
                                                                                                          .Flags(SimpleQueryStringFlags.And | SimpleQueryStringFlags.Escape |
                                                                                                                 SimpleQueryStringFlags.Or | SimpleQueryStringFlags.Not |
                                                                                                                 SimpleQueryStringFlags.Phrase | SimpleQueryStringFlags.Prefix |
                                                                                                                 SimpleQueryStringFlags.Whitespace)
                                                                                                          .DefaultOperator(Operator.And)
                                                                                                          .Query(FormatSimpleSearchString(request.Query)))
                                                                              &&
                                                                              q.Bool(bqd => bqd.Filter(getFilters().Where(gf => gf != null))))
                                                                  .From(request.Skip.Gz(0))
                                                                  .Size(request.Take.Gz(50))
                                                                  .Source(sd => sd.Includes(fd => fd.Fields(esl => esl.PublisherMediaId,
                                                                                                            esl => esl.MediaId,
                                                                                                            esl => esl.PublisherType)));

            if (request.SortRecent)
            {
                searchDescriptor.Sort(m => m.Descending(e => e.PublisherMediaId));
            }

            var response = await _elasticClient.SearchAsync<EsMedia>(searchDescriptor);

            return ToEsResult(response, r => r.Hits.Select(h => h.Source));
        }

        private SearchDescriptor<EsCreator> GetBaseEsCreatorSearchDescriptor(EsCreatorSearch creatorSearch)
        {
            var hasSearchText = creatorSearch.Search.HasValue();

            var geoLocation = creatorSearch.IsValidLatLon()
                                  ? GeoLocation.TryCreate(creatorSearch.Latitude.Value,
                                                          creatorSearch.Longitude.Value)
                                  : null;

            var validGeoLocationAndRange = geoLocation != null && creatorSearch.Miles.HasValue && creatorSearch.Miles.Value > 0;

            Func<QueryContainerDescriptor<EsCreator>, QueryContainer> intRangeFilter<TValue>(Func<EsCreatorSearch, IntRange> forRange,
                                                                                             Expression<Func<EsCreator, TValue>> onField)
                => GetIntRangeFilterOrNull(creatorSearch, forRange, onField);

            Func<QueryContainerDescriptor<EsCreator>, QueryContainer> longRangeFilter<TValue>(Func<EsCreatorSearch, LongRange> forRange,
                                                                                              Expression<Func<EsCreator, TValue>> onField)
                => GetLongRangeFilterOrNull(creatorSearch, forRange, onField);

            Func<QueryContainerDescriptor<EsCreator>, QueryContainer> scaledRangeFilter<TValue>(Func<EsCreatorSearch, DoubleRange> forRange,
                                                                                                Expression<Func<EsCreator, TValue>> onField,
                                                                                                int scaledBy)
                => GetScaledRangeFilterOrNull(creatorSearch, forRange, onField, scaledBy);

            IEnumerable<Func<QueryContainerDescriptor<EsCreator>, QueryContainer>> getAllRangeFilters()
            {
                yield return intRangeFilter(s => s.MinAgeRange, c => c.AgeRangeMin);
                yield return intRangeFilter(s => s.MaxAgeRange, c => c.AgeRangeMax);
                yield return intRangeFilter(s => s.MediaCountRange, c => c.MediaCount);
                yield return longRangeFilter(s => s.FollowedByRange, c => c.FollowedBy);
                yield return longRangeFilter(s => s.FollowingRange, c => c.Following);
                yield return scaledRangeFilter(s => s.StoryEngagementRatingRange, c => c.StoryEngagementRating, EsCreatorScales.EngagementRatingScale);
                yield return longRangeFilter(s => s.StoryImpressionsRange, c => c.StoryImpressions);
                yield return longRangeFilter(s => s.StoryReachRange, c => c.StoryReach);
                yield return intRangeFilter(s => s.StoryActionsRange, c => c.StoryActions);
                yield return intRangeFilter(s => s.StoriesRange, c => c.Stories);
                yield return scaledRangeFilter(s => s.MediaEngagementRatingRange, c => c.MediaEngagementRating, EsCreatorScales.EngagementRatingScale);
                yield return scaledRangeFilter(s => s.MediaTrueEngagementRatingRange, c => c.MediaTrueEngagementRating, EsCreatorScales.EngagementRatingScale);
                yield return longRangeFilter(s => s.MediaImpressionsRange, c => c.MediaImpressions);
                yield return longRangeFilter(s => s.MediaReachRange, c => c.MediaReach);
                yield return intRangeFilter(s => s.MediaActionsRange, c => c.MediaActions);
                yield return intRangeFilter(s => s.MediasRange, c => c.Medias);
                yield return longRangeFilter(s => s.AvgStoryImpressionsRange, c => c.AvgStoryImpressions);
                yield return longRangeFilter(s => s.AvgMediaImpressionsRange, c => c.AvgMediaImpressions);
                yield return longRangeFilter(s => s.AvgStoryReachRange, c => c.AvgStoryReach);
                yield return longRangeFilter(s => s.AvgMediaReachRange, c => c.AvgMediaReach);
                yield return intRangeFilter(s => s.AvgStoryActionsRange, c => c.AvgStoryActions);
                yield return intRangeFilter(s => s.AvgMediaActionsRange, c => c.AvgMediaActions);
                yield return longRangeFilter(s => s.Follower7DayJitterRange, c => c.Follower7DayJitter);
                yield return longRangeFilter(s => s.Follower14DayJitterRange, c => c.Follower14DayJitter);
                yield return longRangeFilter(s => s.Follower30DayJitterRange, c => c.Follower30DayJitter);
                yield return longRangeFilter(s => s.AudienceUsaRange, c => c.AudienceUsa);
                yield return longRangeFilter(s => s.AudienceEnglishRange, c => c.AudienceEnglish);
                yield return longRangeFilter(s => s.AudienceSpanishRange, c => c.AudienceSpanish);
                yield return longRangeFilter(s => s.AudienceMaleRange, c => c.AudienceMale);
                yield return longRangeFilter(s => s.AudienceFemaleRange, c => c.AudienceFemale);
                yield return longRangeFilter(s => s.AudienceAge1317Range, c => c.AudienceAge1317);
                yield return longRangeFilter(s => s.AudienceAge18UpRange, c => c.AudienceAge18Up);
                yield return longRangeFilter(s => s.AudienceAge1824Range, c => c.AudienceAge1824);
                yield return longRangeFilter(s => s.AudienceAge25UpRange, c => c.AudienceAge25Up);
                yield return longRangeFilter(s => s.AudienceAge2534Range, c => c.AudienceAge2534);
                yield return longRangeFilter(s => s.AudienceAge3544Range, c => c.AudienceAge3544);
                yield return longRangeFilter(s => s.AudienceAge4554Range, c => c.AudienceAge4554);
                yield return longRangeFilter(s => s.AudienceAge5564Range, c => c.AudienceAge5564);
                yield return longRangeFilter(s => s.AudienceAge65UpRange, c => c.AudienceAge65Up);
                yield return intRangeFilter(s => s.ImagesAvgAgeRange, c => c.ImagesAvgAge);
                yield return scaledRangeFilter(s => s.SuggestiveRatingRange, c => c.SuggestiveRating, EsCreatorScales.PercentageScale);
                yield return scaledRangeFilter(s => s.ViolenceRatingRange, c => c.ViolenceRating, EsCreatorScales.PercentageScale);
                yield return scaledRangeFilter(s => s.Rydr7DayActivityRatingRange, c => c.Rydr7DayActivityRating, EsCreatorScales.PercentageScale);
                yield return scaledRangeFilter(s => s.Rydr14DayActivityRatingRange, c => c.Rydr14DayActivityRating, EsCreatorScales.PercentageScale);
                yield return scaledRangeFilter(s => s.Rydr30DayActivityRatingRange, c => c.Rydr30DayActivityRating, EsCreatorScales.PercentageScale);
                yield return intRangeFilter(s => s.RequestsRange, c => c.Requests);
                yield return intRangeFilter(s => s.CompletedRequestsRange, c => c.CompletedRequests);
                yield return scaledRangeFilter(s => s.AvgCPMrRange, c => c.AvgCPMr, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPMiRange, c => c.AvgCPMi, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPERange, c => c.AvgCPE, EsCreatorScales.AvgCostPerScale);
                yield return intRangeFilter(s => s.RequestsRange1, c => c.Requests1);
                yield return intRangeFilter(s => s.CompletedRequestsRange1, c => c.CompletedRequests1);
                yield return scaledRangeFilter(s => s.AvgCPMrRange1, c => c.AvgCPMr1, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPMiRange1, c => c.AvgCPMi1, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPERange1, c => c.AvgCPE1, EsCreatorScales.AvgCostPerScale);
                yield return intRangeFilter(s => s.RequestsRange2, c => c.Requests2);
                yield return intRangeFilter(s => s.CompletedRequestsRange2, c => c.CompletedRequests2);
                yield return scaledRangeFilter(s => s.AvgCPMrRange2, c => c.AvgCPMr2, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPMiRange2, c => c.AvgCPMi2, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPERange2, c => c.AvgCPE2, EsCreatorScales.AvgCostPerScale);
                yield return intRangeFilter(s => s.RequestsRange3, c => c.Requests3);
                yield return intRangeFilter(s => s.CompletedRequestsRange3, c => c.CompletedRequests3);
                yield return scaledRangeFilter(s => s.AvgCPMrRange3, c => c.AvgCPMr3, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPMiRange3, c => c.AvgCPMi3, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPERange3, c => c.AvgCPE3, EsCreatorScales.AvgCostPerScale);
                yield return intRangeFilter(s => s.RequestsRange4, c => c.Requests4);
                yield return intRangeFilter(s => s.CompletedRequestsRange4, c => c.CompletedRequests4);
                yield return scaledRangeFilter(s => s.AvgCPMrRange4, c => c.AvgCPMr4, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPMiRange4, c => c.AvgCPMi4, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPERange4, c => c.AvgCPE4, EsCreatorScales.AvgCostPerScale);
                yield return intRangeFilter(s => s.RequestsRange5, c => c.Requests5);
                yield return intRangeFilter(s => s.CompletedRequestsRange5, c => c.CompletedRequests5);
                yield return scaledRangeFilter(s => s.AvgCPMrRange5, c => c.AvgCPMr5, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPMiRange5, c => c.AvgCPMi5, EsCreatorScales.AvgCostPerScale);
                yield return scaledRangeFilter(s => s.AvgCPERange5, c => c.AvgCPE, EsCreatorScales.AvgCostPerScale);
            }

            IEnumerable<Func<QueryContainerDescriptor<EsCreator>, QueryContainer>> getFilters()
            {
                yield return f => f.Term(p => p.IsDeleted, false);

                if (!creatorSearch.Tags.IsNullOrEmpty())
                {
                    if (creatorSearch.Tags.Count == 1)
                    {
                        yield return f => f.Term(p => p.Tags, creatorSearch.Tags.Single().ToString());
                    }
                    else
                    {
                        yield return f => f.Terms(t => t.Field(d => d.Tags)
                                                        .Terms(creatorSearch.Tags.Select(g => g.ToString())));
                    }
                }

                if (creatorSearch.PublisherAccountId > 0)
                {
                    yield return f => f.Term(c => c.PublisherAccountId, creatorSearch.PublisherAccountId);
                }

                if (validGeoLocationAndRange)
                {
                    yield return f => f.GeoDistance(gq => gq.Distance(creatorSearch.Miles.Value, DistanceUnit.Miles)
                                                            .DistanceType(GeoDistanceType.Plane)
                                                            .Location(geoLocation)
                                                            .ValidationMethod(GeoValidationMethod.IgnoreMalformed)
                                                            .Field(d => d.LastLocation));
                }

                if (creatorSearch.Gender != GenderType.Unknown)
                {
                    yield return f => f.Term(p => p.Gender, (int)creatorSearch.Gender);
                }

                foreach (var rangeFilter in getAllRangeFilters().Where(f => f != null))
                {
                    yield return rangeFilter;
                }
            }

            IEnumerable<Func<QueryContainerDescriptor<EsCreator>, QueryContainer>> getMustNots()
            {
                if (!creatorSearch.ExcludePublisherAccountIds.IsNullOrEmpty())
                {
                    yield return f => f.Terms(t => t.Field(d => d.PublisherAccountId)
                                                    .Terms(creatorSearch.ExcludePublisherAccountIds));
                }
            }

            IEnumerable<Func<QueryContainerDescriptor<EsCreator>, QueryContainer>> getShoulds()
            {
                if (!hasSearchText)
                {
                    yield return s => s.MatchAll();

                    yield break;
                }

                yield return s => s.SimpleQueryString(smd => smd.Fields(d => d.Field(e => e.SearchValue))
                                                                .Lenient()
                                                                .Query(FormatSearchStringNoWildcards(creatorSearch.Search, true)));
            }

            var sd = new SearchDescriptor<EsCreator>().Index(ElasticIndexes.CreatorsAlias)
                                                      .Query(qcd => qcd.Bool(bqd => bqd.Filter(getFilters())
                                                                                       .MustNot(getMustNots())
                                                                                       .Should(getShoulds())
                                                                                       .MinimumShouldMatch(MinimumShouldMatch.Fixed(1))));

            return sd;
        }

        private Func<QueryContainerDescriptor<TModel>, QueryContainer> GetIntRangeFilterOrNull<TModel, TSearch, TField>(TSearch searchRequest,
                                                                                                                        Func<TSearch, IntRange> forRange,
                                                                                                                        Expression<Func<TModel, TField>> onField)
            where TModel : class
        {
            var range = forRange(searchRequest);

            if (range == null || !range.IsValid())
            {
                return null;
            }

            return f => f.LongRange(lr => lr.Field(onField)
                                            .GreaterThanOrEquals(range.Min)
                                            .LessThanOrEquals(range.Max));
        }

        private Func<QueryContainerDescriptor<TModel>, QueryContainer> GetLongRangeFilterOrNull<TModel, TSearch, TField>(TSearch searchRequest,
                                                                                                                         Func<TSearch, LongRange> forRange,
                                                                                                                         Expression<Func<TModel, TField>> onField)
            where TModel : class
        {
            var range = forRange(searchRequest);

            if (range == null || !range.IsValid())
            {
                return null;
            }

            return f => f.LongRange(lr => lr.Field(onField)
                                            .GreaterThanOrEquals(range.Min)
                                            .LessThanOrEquals(range.Max));
        }

        private Func<QueryContainerDescriptor<TModel>, QueryContainer> GetScaledRangeFilterOrNull<TModel, TSearch, TField>(TSearch searchRequest,
                                                                                                                           Func<TSearch, DoubleRange> forRange,
                                                                                                                           Expression<Func<TModel, TField>> onField,
                                                                                                                           int scaledBy)
            where TModel : class
        {
            var range = forRange(searchRequest);

            if (range == null || !range.IsValid())
            {
                return null;
            }

            return f => f.LongRange(lr => lr.Field(onField)
                                            .GreaterThanOrEquals((long)(range.Min * scaledBy))
                                            .LessThanOrEquals((long)(range.Max * scaledBy)));
        }

        private Func<QueryContainerDescriptor<TModel>, QueryContainer> GetDoubleRangeFilterOrNull<TModel, TSearch, TField>(TSearch searchRequest,
                                                                                                                           Func<TSearch, DoubleRange> forRange,
                                                                                                                           Expression<Func<TModel, TField>> onField)
            where TModel : class
        {
            var range = forRange(searchRequest);

            if (range == null || !range.IsValid())
            {
                return null;
            }

            return f => f.Range(lr => lr.Field(onField)
                                        .GreaterThanOrEquals(range.Min)
                                        .LessThanOrEquals(range.Max));
        }

        private static string FormatSimpleSearchString(string search)
        {
            if (search.IsNullOrEmpty())
            {
                return search;
            }

            var formattedSearch = search.StripChars(ElasticHelpers.QueryStringForbiddenChars);

            return _leadingSearchReplacementChars.Aggregate(formattedSearch, (current, searchReplacement) => current.Replace(searchReplacement, '_'));
        }

        private static string FormatSearchStringNoWildcards(string search, bool trailingWildcard = false)
        {
            var formattedSearch = search.DelimitAll(ElasticHelpers.QueryStringReservedChars, '\\')
                                        .StripChars(ElasticHelpers.QueryStringForbiddenChars);

            return trailingWildcard
                       ? string.Concat(formattedSearch, "*")
                       : formattedSearch;
        }

        private static EsSearchResult<T> ToEsResult<T>(ISearchResponse<T> searchResponse,
                                                       Func<ISearchResponse<T>, IEnumerable<T>> successfulResults,
                                                       [CallerMemberName] string methodName = null)
            where T : class
            => ToEsResult2(searchResponse, successfulResults, methodName);

        private static EsSearchResult<TResult> ToEsResult2<T, TResult>(ISearchResponse<T> searchResponse,
                                                                       Func<ISearchResponse<T>, IEnumerable<TResult>> successfulResults,
                                                                       [CallerMemberName] string methodName = null)
            where T : class
        {
            var result = new EsSearchResult<TResult>
                         {
                             Successful = searchResponse.Successful(),
                             TimedOut = searchResponse.TimedOut,
                             TookMs = searchResponse.Took,
                             TotalHits = searchResponse.HitsMetadata?.Total?.Value ?? 0
                         };

            if (_logElasticRequestBodies || RydrEnvironment.IsLocalEnvironment || RydrEnvironment.IsTestEnvironment)
            { // NOTE: Keep the wrapped if block above even though the format extension below checks for this, just cause we don't want to be
                // actually decoding and pulling the entire request body everytime this sucker hits...
                var requestBody = Encoding.UTF8.GetString(searchResponse.ApiCall.RequestBodyInBytes);

                _log.DebugInfoFormat("ElasticSearchService.{0} API Request: [{1}]", methodName, requestBody);

                if (RydrEnvironment.IsTestEnvironment)
                {
                    var responseBody = Encoding.UTF8.GetString(searchResponse.ApiCall.ResponseBodyInBytes);

                    Debug.Print(requestBody);

                    result.Request = requestBody;
                    result.Response = responseBody;
                }
            }

            result.Results = result.Successful && searchResponse != null
                                 ? successfulResults(searchResponse).AsList()
                                 : new List<TResult>();

            return result;
        }
    }
}
