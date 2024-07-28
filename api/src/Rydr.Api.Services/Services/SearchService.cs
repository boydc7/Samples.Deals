using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Search;
using Rydr.Api.Dto.Users;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;
using CollectionExtensions = System.Collections.Generic.CollectionExtensions;

namespace Rydr.Api.Services.Services;

public class SearchService : BaseAuthenticatedApiService
{
    private readonly IElasticSearchService _esService;
    private readonly IDealService _dealService;
    private readonly IPublisherAccountStatsService _publisherAccountStatsService;
    private readonly IDeferRequestsService _deferRequestsService;

    public SearchService(IElasticSearchService esService,
                         IDealService dealService,
                         IPublisherAccountStatsService publisherAccountStatsService,
                         IDeferRequestsService deferRequestsService)
    {
        _esService = esService;
        _dealService = dealService;
        _publisherAccountStatsService = publisherAccountStatsService;
        _deferRequestsService = deferRequestsService;
    }

    [RydrForcedSimpleCacheResponse(300)]
    public async Task<OnlyResultsResponse<CreatorAccountInfo>> Get(GetCreatorsSearch request)
    {
        var esCreatorSearch = request.ToEsCreatorSearch();

        if (request.ExcludeInvitesDealId > 0)
        {
            var dynDeal = await _dealService.GetDealAsync(request.ExcludeInvitesDealId, true);

            if (!(dynDeal?.InvitedPublisherAccountIds).IsNullOrEmpty())
            {
                esCreatorSearch.ExcludePublisherAccountIds = dynDeal.InvitedPublisherAccountIds
                                                                    .Union((esCreatorSearch.ExcludePublisherAccountIds ?? Enumerable.Empty<long>()))
                                                                    .AsList();
            }
        }

        (string latString, string lonString, long locationId) getCreatorLocationId(EsCreator ec)
        {
            if (ec?.LastLocation == null)
            {
                return (null, null, 0);
            }

            var latString = ec.LastLocation.Latitude.ToLocationMapLatLonString();
            var lonString = ec.LastLocation.Longitude.ToLocationMapLatLonString();
            var locationId = AddressTransforms.ToLocationMapId(latString, lonString);

            return (latString, lonString, locationId);
        }

        DynamoId getDynamoIdFromCreator(EsCreator ec)
        {
            var (latString, lonString, locationId) = getCreatorLocationId(ec);

            return locationId > 0
                       ? new DynamoId(locationId, AddressTransforms.ToLocationMapEdgeId(latString, lonString))
                       : null;
        }

        var esSearchResult = await _esService.SearchCreatorIdsAsync(esCreatorSearch);

        var publisherStatsMap = (esSearchResult?.Results).IsNullOrEmptyRydr()
                                    ? null
                                    : await _publisherAccountStatsService.GetPublisherAccountsStats(esSearchResult.Results.Select(r => r.PublisherAccountId));

        var locationMap = (esSearchResult?.Results).IsNullOrEmptyRydr()
                              ? null
                              : await _dynamoDb.QueryItemsAsync<DynItemMap>(esSearchResult.Results
                                                                                          .Select(getDynamoIdFromCreator)
                                                                                          .Where(i => i != null)
                                                                                          .Distinct())
                                               .ToDictionarySafe(m => m.EdgeId, m => m.ToAddress(), StringComparer.OrdinalIgnoreCase);

        var publisherAccounts = (esSearchResult?.Results).IsNullOrEmptyRydr()
                                    ? null
                                    : await _dynamoDb.QueryItemsAsync<DynPublisherAccount>(esSearchResult.Results
                                                                                                         .Select(r => new DynamoId(r.PublisherAccountId,
                                                                                                                                   DynPublisherAccount.BuildEdgeId(r.PublisherType.TryToEnum(PublisherType.Unknown),
                                                                                                                                                                   r.AccountId))))
                                                     .Select(p =>
                                                             {
                                                                 var creatorAccountInfo = p.ToPublisherAccount().ConvertTo<CreatorAccountInfo>();

                                                                 var searchResult = esSearchResult.Results.FirstOrDefault(r => r.PublisherAccountId == p.PublisherAccountId);

                                                                 if (searchResult != null)
                                                                 {
                                                                     creatorAccountInfo.LastLatitude = searchResult.LastLocation?.Latitude;
                                                                     creatorAccountInfo.LastLongitude = searchResult.LastLocation?.Longitude;

                                                                     creatorAccountInfo.LocationUpdatedOn = searchResult.LastLocationModifiedOn > DateTimeHelper.MinApplicationDateTs
                                                                                                                ? searchResult.LastLocationModifiedOn.ToDateTime()
                                                                                                                : null;

                                                                     var (latString, lonString, locationId) = getCreatorLocationId(searchResult);

                                                                     var edgeId = locationId > 0
                                                                                      ? AddressTransforms.ToLocationMapEdgeId(latString, lonString)
                                                                                      : null;

                                                                     if (edgeId != null)
                                                                     {
                                                                         if (locationMap.ContainsKey(edgeId))
                                                                         {
                                                                             creatorAccountInfo.LastLocationAddress = locationMap[edgeId];
                                                                         }
                                                                         else
                                                                         { // This creator has a last location, however we do not have a mapped address location for it yet, defer a request to do so
                                                                             _deferRequestsService.DeferLowPriRequest(new AddLocationMap
                                                                                                                      {
                                                                                                                          Latitude = creatorAccountInfo.LastLatitude,
                                                                                                                          Longitude = creatorAccountInfo.LastLongitude,
                                                                                                                          ForPublisherAccountId = creatorAccountInfo.Id
                                                                                                                      }.WithAdminRequestInfo());
                                                                         }
                                                                     }
                                                                 }

                                                                 creatorAccountInfo.CreatorStats = publisherStatsMap.ContainsKey(creatorAccountInfo.Id)
                                                                                                       ? new PublisherAccountStats
                                                                                                         {
                                                                                                             DealRequestStats = publisherStatsMap[creatorAccountInfo.Id]
                                                                                                         }
                                                                                                       : null;

                                                                 return creatorAccountInfo;
                                                             })
                                                     .OrderByDescending(p => p.Id)
                                                     .ToList();

        return publisherAccounts?.AsOnlyResultsResponse(esSearchResult?.TotalHits);
    }

    [RydrForcedSimpleCacheResponse(300)]
    public async Task<OnlyResultsResponse<PublisherAccountInfo>> Get(GetBusinessesSearch request)
    {
        var esBusinessSearch = request.ToEsBusinessSearch();

        var esSearchResult = await _esService.SearchBusinessIdsAsync(esBusinessSearch);
        var esSearchResults = esSearchResult?.Results?.AsListReadOnly();

        if (esSearchResults.IsNullOrEmptyReadOnly())
        {
            return new OnlyResultsResponse<PublisherAccountInfo>();
        }

        var publisherAccounts = await _dynamoDb.QueryItemsAsync<DynPublisherAccount>(esSearchResult.Results
                                                                                                   .Select(r => new DynamoId(r.PublisherAccountId,
                                                                                                                             DynPublisherAccount.BuildEdgeId(r.PublisherType.TryToEnum(PublisherType.Unknown),
                                                                                                                                                             r.AccountId))))
                                               .Select(p => p.ToPublisherAccountInfo())
                                               .ToDictionarySafe(p => p.Id);

        return esSearchResults.Select(er => CollectionExtensions.GetValueOrDefault(publisherAccounts, er.PublisherAccountId))
                              .Where(er => er != null)
                              .AsOnlyResultsResponse(esSearchResult.TotalHits);
    }
}
