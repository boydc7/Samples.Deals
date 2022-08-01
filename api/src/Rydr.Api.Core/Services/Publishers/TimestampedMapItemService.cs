using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Publishers
{
    public class TimestampedMapItemService : TimestampCachedServiceBase<DynItemMap>, IMapItemService
    {
        private readonly IPocoDynamo _dynamoDb;

        public TimestampedMapItemService(ICacheClient cacheClient,
                                         IPocoDynamo dynamoDb)
            : base(cacheClient, 500)
        {
            _dynamoDb = dynamoDb;
        }

        public DynItemMap TryGetMap(long id, string edgeId, bool consistentRead = false)
        {
            if (edgeId.IsNullOrEmpty())
            {
                return null;
            }

            return GetModel(string.Concat(id, "|", edgeId),
                            () => _dynamoDb.GetItem<DynItemMap>(id, edgeId, consistentRead));
        }

        public Task<DynItemMap> TryGetMapAsync(long id, string edgeId, bool consistentRead = false)
        {
            if (edgeId.IsNullOrEmpty())
            {
                return Task.FromResult<DynItemMap>(null);
            }

            return GetModelAsync(string.Concat(id, "|", edgeId),
                                 () => _dynamoDb.GetItemAsync<DynItemMap>(id, edgeId, consistentRead));
        }

        public async Task<bool> MapExistsAsync(long id, string edgeId)
        {
            var map = await TryGetMapAsync(id, edgeId);

            return map != null;
        }

        public async Task<DynItemMap> GetMapAsync(long id, string edgeId, bool consistentRead = false)
        {
            var map = await TryGetMapAsync(id, edgeId, consistentRead);

            Guard.AgainstRecordNotFound(map == null, "Map record was not found or you do not have access to it");

            return map;
        }

        public DynItemMap TryGetMapByHashedEdge(DynItemType mapItemType, string hashedEdgeValue, bool consistentRead = false)
        {
            var longId = hashedEdgeValue.ToLongHashCode();

            return TryGetMap(longId, DynItemMap.BuildEdgeId(mapItemType, hashedEdgeValue), consistentRead);
        }

        public Task<DynItemMap> TryGetMapByHashedEdgeAsync(DynItemType mapItemType, string hashedEdgeValue, bool consistentRead = false)
        {
            var longId = hashedEdgeValue.ToLongHashCode();

            return TryGetMapAsync(longId, DynItemMap.BuildEdgeId(mapItemType, hashedEdgeValue), consistentRead);
        }

        public void PutMap(DynItemMap mapItem)
        {
            _dynamoDb.PutItem(mapItem);

            SetModel(string.Concat(mapItem.Id, "|", mapItem.EdgeId), mapItem);
        }

        public async Task PutMapAsync(DynItemMap mapItem)
        {
            await _dynamoDb.PutItemAsync(mapItem);

            SetModel(string.Concat(mapItem.Id, "|", mapItem.EdgeId), mapItem);
        }

        public void DeleteMap(long id, string edgeId)
        {
            _dynamoDb.DeleteItem<DynItemMap>(id, edgeId);

            OnMapUpdate(id, edgeId);
        }

        public async Task DeleteMapAsync(long id, string edgeId)
        {
            await _dynamoDb.DeleteItemAsync<DynItemMap>(id, edgeId);

            OnMapUpdate(id, edgeId);
        }

        public void OnMapUpdate(long id, string edgeId)
            => FlushModel(string.Concat(id, "|", edgeId));
    }

    public static class MapItemService
    {
        public static IMapItemService DefaultMapItemService { get; } = RydrEnvironment.Container.Resolve<IMapItemService>();
    }
}
