using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IDealService
    {
        Task<bool> CanBeDeletedAsync(long dealId);
        Task<bool> CanBeDeletedAsync(DynDeal dynDeal);

        Task<IReadOnlyList<DynDeal>> GetDynDealsAsync(IEnumerable<DynamoId> dealIds);
        Task<DynDeal> GetDealAsync(long dealId, bool ignoreNotFound = false);
        Task<DynDeal> GetDealAsync(long publisherAccountId, long dealId);

        Task<(Dictionary<long, PublisherMedia> PublisherMediaMap, Dictionary<long, Place> PlaceMap,
              Dictionary<long, Hashtag> HashtagMap, Dictionary<long, PublisherAccount> PublisherMap)> GetDealMapsForTransformAsync(IReadOnlyCollection<DynDeal> dynDeals);

        Task<DynDealStat> GetDealStatAsync(long dealId, DealStatType dealStatType);
        Task<DynDealStat> GetDealStatAsync(long dealId, long dealPublisherAccountId, DealStatType dealStatType);
        Task<List<DynDealStat>> GetDealStatsAsync(long dealId);

        Task<Dictionary<long, List<DynDealStat>>> GetDealStatsAsync<T>(IEnumerable<T> deals)
            where T : IHasDealId, IHasPublisherAccountId;

        Task ProcesDealStatsAsync(long dealId, long fromPublisherAccountId, DealStatType statType, DealStatType? fromStatType);

        Task SendDealNotificationAsync(long fromPublisherAccountId, long toPublisherAccountId, long dealId,
                                       string title, string message, ServerNotificationType notificationType,
                                       long workspaceId, string protectedSetPrefix = null);
    }

    public interface IDealMetricService
    {
        void Measure(DealTrackMetricType type, DealResponse dealResponse, long otherPublisherAccountId = 0, long workspaceId = 0, IHasUserLatitudeLongitude fromLocation = null, long userId = 0);
        void Measure(DealTrackMetricType type, IReadOnlyList<DealResponse> dealResponses, long otherPublisherAccountId = 0, long workspaceId = 0, IHasUserLatitudeLongitude fromLocation = null, long userId = 0);
        void Measure(DealTrackMetricType type, DynDeal dynDeal, long otherPublisherAccountId = 0, long workspaceId = 0, long userId = 0);
    }
}
