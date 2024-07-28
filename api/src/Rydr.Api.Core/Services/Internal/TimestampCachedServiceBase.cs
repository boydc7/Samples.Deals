using System.Collections.Concurrent;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Models.Doc;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.Model;
using ServiceStack.OrmLite.Dapper;

// ReSharper disable InconsistentlySynchronizedField

// ReSharper disable StaticMemberInGenericType

namespace Rydr.Api.Core.Services.Internal;

public abstract class TimestampDynItemIdCachedServiceBase<T> : TimestampCachedServiceBase<T>
    where T : class, IDynItem
{
    private readonly IPocoDynamo _dynamoDb;

    protected TimestampDynItemIdCachedServiceBase(IPocoDynamo dynamoDb, ICacheClient cacheClient,
                                                  int localCacheSeconds = 0, int maxBufferCount = 25000)
        : base(cacheClient, localCacheSeconds, maxBufferCount)
    {
        _dynamoDb = dynamoDb;
    }

    // For these requests, we simply get them from dynamo and cache what's returned - the assumption being that at
    // least one thing will be a cache miss most of the time, in which case making one or more cache calls is basically
    // the same round-trip cost as a dyamo batch get...
    protected IEnumerable<T> GetIdModels(IEnumerable<long> ids)
        => _dynamoDb.GetItems<T>(ids.Select(k => new DynamoId(k, k.ToEdgeId())))
                    .Where(m => m != null)
                    .Select(m =>
                            {
                                _typeMap.AddOrUpdate(m.Id.ToStringInvariant(),
                                                     new TrackedTypeMapModel
                                                     {
                                                         Model = m,
                                                         StoredAt = DateTimeHelper.UtcNowTs
                                                     },
                                                     (k, x) =>
                                                     {
                                                         x.Model = m;
                                                         x.StoredAt = DateTimeHelper.UtcNowTs;

                                                         return x;
                                                     });

                                return m;
                            });

    // For these requests, we simply get them from dynamo and cache what's returned - the assumption being that at
    // least one thing will be a cache miss most of the time, in which case making one or more cache calls is basically
    // the same round-trip cost as a dyamo batch get...
    protected async IAsyncEnumerable<T> GetIdModelsAsync(IAsyncEnumerable<long> ids)
    {
        await foreach (var idBatch in ids.ToBatchesOfAsync(50.ToDynamoBatchCeilingTake()))
        {
            await foreach (var item in _dynamoDb.QueryItemsAsync<T>(idBatch.Select(k => new DynamoId(k, k.ToEdgeId()))))
            {
                _typeMap.AddOrUpdate(item.Id.ToStringInvariant(),
                                     new TrackedTypeMapModel
                                     {
                                         Model = item,
                                         StoredAt = DateTimeHelper.UtcNowTs
                                     },
                                     (k, x) =>
                                     {
                                         x.Model = item;
                                         x.StoredAt = DateTimeHelper.UtcNowTs;

                                         return x;
                                     });

                yield return item;
            }
        }
    }

    // For these requests, we simply get them from dynamo and cache what's returned - the assumption being that at
    // least one thing will be a cache miss most of the time, in which case making one or more cache calls is basically
    // the same round-trip cost as a dyamo batch get...
    protected async IAsyncEnumerable<T> GetIdModelsAsync<TFrom>(IAsyncEnumerable<TFrom> idSource)
        where TFrom : IHasLongId
    {
        await foreach (var sourceBatch in idSource.ToBatchesOfAsync(100, true))
        {
            await foreach (var item in _dynamoDb.QueryItemsAsync<T>(sourceBatch.Select(k => new DynamoId(k.Id, k.Id.ToEdgeId()))
                                                                               .Distinct()))
            {
                _typeMap.AddOrUpdate(item.Id.ToStringInvariant(),
                                     new TrackedTypeMapModel
                                     {
                                         Model = item,
                                         StoredAt = DateTimeHelper.UtcNowTs
                                     },
                                     (k, x) =>
                                     {
                                         x.Model = item;
                                         x.StoredAt = DateTimeHelper.UtcNowTs;

                                         return x;
                                     });

                yield return item;
            }
        }
    }

    // This implies the item in question is a mapped model that stores an id map of the id and the id as an edgeId in the maps table
    // that allows us to batch query by id only to get the actual models (which use the id/mappedEdge combination from the map results)
    protected IEnumerable<T> GetMappedIdModels(IEnumerable<long> ids, DynItemType forType)
        => _dynamoDb.GetItems<T>(_dynamoDb.GetItems<DynItemMap>(ids.Distinct()
                                                                   .Select(k => new DynamoId(k,
                                                                                             DynItemMap.BuildEdgeId(forType,
                                                                                                                    k.ToEdgeId()))))
                                          .Select(map => new DynamoId(map.Id, map.MappedItemEdgeId)))
                    .Where(m => m != null)
                    .Select(m =>
                            {
                                _typeMap.AddOrUpdate(m.Id.ToStringInvariant(),
                                                     new TrackedTypeMapModel
                                                     {
                                                         Model = m,
                                                         StoredAt = DateTimeHelper.UtcNowTs
                                                     },
                                                     (k, x) =>
                                                     {
                                                         x.Model = m;
                                                         x.StoredAt = DateTimeHelper.UtcNowTs;

                                                         return x;
                                                     });

                                return m;
                            });

    // This implies the item in question is a mapped model that stores an id map of the id and the id as an edgeId in the maps table
    // that allows us to batch query by id only to get the actual models (which use the id/mappedEdge combination from the map results)
    protected IAsyncEnumerable<T> GetMappedIdModelsAsync(IEnumerable<long> ids, DynItemType forType)
        => _dynamoDb.GetItemsFromAsync<T, DynItemMap>(_dynamoDb.QueryItemsAsync<DynItemMap>(ids.Distinct()
                                                                                               .Select(i => new DynamoId(i,
                                                                                                                         DynItemMap.BuildEdgeId(forType,
                                                                                                                                                i.ToEdgeId())))),
                                                      f => new DynamoId(f.Id, f.MappedItemEdgeId));

    // This implies the item in question is a mapped model that stores an id map of the id and the id as an edgeId in the maps table
    // that allows us to batch query by id only to get the actual models (which use the id/mappedEdge combination from the map results)
    protected async IAsyncEnumerable<T> GetMappedIdModelsAsync(IAsyncEnumerable<long> ids, DynItemType forType)
    {
        await foreach (var idBatch in ids.ToBatchesOfAsync(50.ToDynamoBatchCeilingTake()))
        {
            await foreach (var model in _dynamoDb.GetItemsFromAsync<T, DynItemMap>(_dynamoDb.QueryItemsAsync<DynItemMap>(idBatch.Select(i => new DynamoId(i, DynItemMap.BuildEdgeId(forType, i.ToEdgeId())))),
                                                                                   f => new DynamoId(f.Id, f.MappedItemEdgeId)))
            {
                yield return model;
            }
        }
    }
}

public abstract class TimestampCachedServiceBase<T>
    where T : class
{
    private static readonly bool _cacheDisabled = RydrEnvironment.GetAppSetting("Caching.DisableAll", false) ||
                                                  RydrEnvironment.GetAppSetting("Caching.DisableTimestamped", false);

    private static readonly TimeSpan _timestampCacheDuration = TimeSpan.FromDays(15);

    private readonly object _lockObject = new();
    private readonly int _localCacheSeconds;
    private readonly int _maxBufferCount;

    private bool _registeredManagement;

    protected readonly ConcurrentDictionary<string, TrackedTypeMapModel> _typeMap = new();
    protected readonly ICacheClient _cacheClient;

    protected TimestampCachedServiceBase(ICacheClient cacheClient, int localCacheSeconds = 0, int maxBufferCount = 25000)
    {
        _cacheClient = cacheClient;
        _localCacheSeconds = localCacheSeconds;
        _maxBufferCount = maxBufferCount;
    }

    public void ManageMapSize()
    {
        if (_typeMap.Count <= _maxBufferCount)
        {
            return;
        }

        if (_cacheDisabled)
        {
            if (_typeMap.Count > 0)
            {
                _typeMap.Clear();
            }

            return;
        }

        // Remove half the buffer
        var keysToRemove = _typeMap.OrderBy(t => t.Value.StoredAt)
                                   .Take(_maxBufferCount / 2)
                                   .Select(k => k.Key)
                                   .AsList();

        foreach (var keyToRemove in keysToRemove)
        {
            _typeMap.TryRemove(keyToRemove, out _);
        }
    }

    protected Task<T> GetModelAsync(long id, Func<Task<T>> getter)
        => GetModelAsync(id.ToStringInvariant(), getter);

    protected T GetModel(long id, Func<T> getter)
        => GetModel(id.ToStringInvariant(), getter);

    protected T GetModel(string id, Func<T> getter)
    {
        if (_cacheDisabled)
        {
            return getter();
        }

        var now = DateTimeHelper.UtcNowTs;
        var setNullModelOnGetterNull = false;

        if (_typeMap.TryGetValue(id, out var trackedModel))
        {
            if ((trackedModel.StoredAt + _localCacheSeconds) >= now)
            { // Safe to not check for localCacheSeconds > 0, as that will effectively result in storedAt time, which has to be less than now...
                return trackedModel.Model;
            }

            // Local cached must be validated with remote timestamp
            var cachedLastUpdated = _cacheClient.Get<long>(GetTrackedModelTimestampCacheKey(id));

            if (cachedLastUpdated > 0 && trackedModel.StoredAt >= cachedLastUpdated)
            { // Still valid, bump the local stored timestamp and return the model
                trackedModel.StoredAt = now;

                return trackedModel.Model;
            }

            setNullModelOnGetterNull = true;
        }

        var model = getter();

        if (model == null)
        {
            if (setNullModelOnGetterNull)
            {
                FlushModel(id);
            }
        }
        else
        {
            _typeMap.AddOrUpdate(id,
                                 new TrackedTypeMapModel
                                 {
                                     Model = model,
                                     StoredAt = now
                                 },
                                 (k, x) =>
                                 {
                                     x.Model = model;
                                     x.StoredAt = now;

                                     return x;
                                 });
        }

        if (!_registeredManagement)
        {
            lock(_lockObject)
            {
                if (!_registeredManagement)
                {
                    LocalResourceManager.Instance.RegisterManagementCallback(this, t => t.ManageMapSize());

                    _registeredManagement = true;
                }
            }
        }

        return model;
    }

    protected async Task<T> GetModelAsync(string id, Func<Task<T>> getter)
    {
        if (_cacheDisabled)
        {
            return await getter();
        }

        var now = DateTimeHelper.UtcNowTs;
        var setNullModelOnGetterNull = false;

        if (_typeMap.TryGetValue(id, out var trackedModel))
        {
            if ((trackedModel.StoredAt + _localCacheSeconds) >= now)
            { // Safe to not check for localCacheSeconds > 0, as that will effectively result in storedAt time, which has to be less than now...
                return trackedModel.Model;
            }

            // Local cached must be validated with remote timestamp
            var cachedLastUpdated = _cacheClient.Get<long>(GetTrackedModelTimestampCacheKey(id));

            if (cachedLastUpdated > 0 && trackedModel.StoredAt >= cachedLastUpdated)
            { // Still valid, bump the local stored timestamp and return the model
                trackedModel.StoredAt = now;

                return trackedModel.Model;
            }

            setNullModelOnGetterNull = true;
        }

        var model = await getter();

        if (model == null)
        {
            if (setNullModelOnGetterNull)
            {
                FlushModel(id);
            }
        }
        else
        {
            _typeMap.AddOrUpdate(id,
                                 new TrackedTypeMapModel
                                 {
                                     Model = model,
                                     StoredAt = now
                                 },
                                 (k, x) =>
                                 {
                                     x.Model = model;
                                     x.StoredAt = now;

                                     return x;
                                 });
        }

        if (!_registeredManagement)
        {
            lock(_lockObject)
            {
                if (!_registeredManagement)
                {
                    LocalResourceManager.Instance.RegisterManagementCallback(this, t => t.ManageMapSize());

                    _registeredManagement = true;
                }
            }
        }

        return model;
    }

    public void FlushModel(long id)
    {
        if (id <= 0 || _cacheDisabled)
        {
            return;
        }

        FlushModel(id.ToStringInvariant());
    }

    protected void FlushModel(string id)
        => SetModel(id, null);

    protected void SetModel(long id, T model)
    {
        if (id <= 0 || _cacheDisabled)
        {
            return;
        }

        SetModel(id.ToStringInvariant(), model);
    }

    protected void SetModel(string id, T model)
    {
        if (_cacheDisabled)
        {
            return;
        }

        var now = DateTimeHelper.UtcNowTs;

        if (model == null)
        {
            _typeMap.TryRemove(id, out _);
        }
        else
        {
            _typeMap.AddOrUpdate(id,
                                 new TrackedTypeMapModel
                                 {
                                     Model = model,
                                     StoredAt = now
                                 },
                                 (k, x) =>
                                 {
                                     x.Model = model;
                                     x.StoredAt = now;

                                     return x;
                                 });
        }

        Invalidate(id, model == null
                           ? long.MaxValue
                           : now);
    }

    protected void Invalidate(long id, long atTimestamp = 0)
    {
        if (id <= 0 || _cacheDisabled)
        {
            return;
        }

        Invalidate(id.ToStringInvariant(), atTimestamp);
    }

    protected void Invalidate(string id, long atTimestamp = 0)
    {
        if (_cacheDisabled)
        {
            return;
        }

        var now = DateTimeHelper.UtcNowTs;

        if (atTimestamp <= DateTimeHelper.MinApplicationDateTs)
        {
            atTimestamp = now;
        }

        if (atTimestamp > now)
        {
            _cacheClient.Remove(GetTrackedModelTimestampCacheKey(id));
        }
        else
        {
            _cacheClient.Set(GetTrackedModelTimestampCacheKey(id),
                             atTimestamp,
                             _timestampCacheDuration);
        }
    }

    protected string GetTrackedModelTimestampCacheKey(string id)
        => id.ToCacheKey<T>("TimestampCached");

    protected class TrackedTypeMapModel
    {
        public T Model { get; set; }
        public long StoredAt { get; set; }
    }
}
