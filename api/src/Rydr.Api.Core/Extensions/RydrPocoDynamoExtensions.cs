using System.Linq.Expressions;
using Amazon.DAX;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;
using ServiceStack.Model;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Extensions;

public static class RydrPocoDynamoExtensions
{
    private static readonly Func<ILocalRequestCacheClient> _localRequestCacheClientFactory = () => RydrEnvironment.Container.Resolve<ILocalRequestCacheClient>();

    private static readonly ILog _log = LogManager.GetLogger("RydrPocoDynamoExtensions");
    private static readonly IAuthorizationService _authorizationService = RydrEnvironment.Container.Resolve<IAuthorizationService>();
    private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();
    private static readonly IRequestStateManager _requestStateManager = RydrEnvironment.Container.Resolve<IRequestStateManager>();
    private static readonly IDistributedLockService _distributedLockService = RydrEnvironment.Container.Resolve<IDistributedLockService>();

    private static readonly HashSet<char> _nonZeroNumbers =
    [
        '1',
        '2',
        '3',
        '4',
        '5',
        '6',
        '7',
        '8',
        '9'
    ];

    public static IAsyncEnumerable<T> QueryAsync<T>(this QueryExpression<T> queryExpression, IPocoDynamo dynamo)
        => dynamo.QueryAsync(queryExpression);

    public static async IAsyncEnumerable<T> QueryItemsAsync<T>(this IPocoDynamo dynamoDb, IEnumerable<object> ids)
    {
        foreach (var idBatch in ids.ToLazyBatchesOf(100))
        {
            var batch = await dynamoDb.GetItemsAsync<T>(idBatch);

            if (batch is not { Count: > 0 })
            {
                yield break;
            }

            foreach (var item in batch)
            {
                yield return item;
            }
        }
    }

    public static IAsyncEnumerable<TKey> QueryColumnAsync<T, TKey>(this QueryExpression<T> queryExpression,
                                                                   Expression<Func<T, TKey>> fields,
                                                                   IPocoDynamo dynamoDb)
    {
        var q = new PocoDynamoExpression(typeof(T)).Parse(fields);
        var field = q.ReferencedFields[0];
        queryExpression.ProjectionExpression = field;
        var rydrQx = new RydrQueryExpression<T>(dynamoDb);

        return dynamoDb.QueryAsync(queryExpression)
                       .Select(c => rydrQx.GetFieldValue<TKey>(field, c));
    }

    private class RydrQueryExpression<T> : QueryExpression<T>
    {
        public RydrQueryExpression(IPocoDynamo dynamo)
            : base(dynamo) { }

        public TKey GetFieldValue<TKey>(string field, T fromInstance)
        {
            var obj = Table.GetField(field).GetValue(fromInstance);

            return (TKey)obj;
        }
    }

    public static async IAsyncEnumerable<T> QueryItemsAsync<T>(this IPocoDynamo dynamoDb, IEnumerable<DynamoId> ids)
    {
        foreach (var idBatch in ids.ToLazyBatchesOf(100))
        {
            var batch = await dynamoDb.GetItemsAsync<T>(idBatch);

            if (batch is not { Count: > 0 })
            {
                yield break;
            }

            foreach (var item in batch)
            {
                yield return item;
            }
        }
    }

    public static IAsyncEnumerable<T> GetItemsFromAsync<T, TFrom>(this IPocoDynamo dynamoDb, IAsyncEnumerable<TFrom> idSources,
                                                                  int skip = 0, int take = int.MaxValue)
        where TFrom : IHaveIdAndEdgeId
        => GetItemsFromAsync<T, TFrom>(dynamoDb, idSources, i => i.ToDynamoId(), skip, take);

    public static async IAsyncEnumerable<T> GetItemsFromAsync<T, TFrom>(this IPocoDynamo dynamoDb, IAsyncEnumerable<TFrom> idSources,
                                                                        Func<TFrom, DynamoId> idSelector,
                                                                        int skip = 0, int take = int.MaxValue)
    {
        const int batchSize = 100;
        var enumerated = 0;
        var yielded = 0;

        await foreach (var idBatch in idSources.ToBatchesOfAsync(batchSize, true))
        {
            var thisSkip = skip > enumerated
                               ? skip - enumerated
                               : 0;

            var thisTake = take > yielded
                               ? (take - yielded)
                               : 0;

            if (thisSkip >= batchSize)
            {
                enumerated += batchSize;

                continue;
            }

            var items = await dynamoDb.GetItemsAsync<T>(idBatch.Select(i =>
                                                                       {
                                                                           enumerated++;

                                                                           return i;
                                                                       })
                                                               .Skip(thisSkip)
                                                               .Take(thisTake)
                                                               .Select(idSelector));

            foreach (var item in items)
            {
                yielded++;

                yield return item;
            }

            if (yielded >= take)
            {
                yield break;
            }
        }
    }

    public static async Task DeleteItemsFromAsync<T, TFrom>(this IPocoDynamo dynamoDb, IAsyncEnumerable<TFrom> idSources,
                                                            Func<TFrom, DynamoId> idSelector)
    {
        await foreach (var idSourceBatch in idSources.ToBatchesOfAsync(100, true))
        {
            await dynamoDb.DeleteItemsAsync<T>(idSourceBatch.Select(idSelector));
        }
    }

    public static async Task PutItemsFromAsync<T, TFrom>(this IPocoDynamo dynamoDb, IAsyncEnumerable<TFrom> fromSource, Func<TFrom, T> transform)
    {
        await foreach (var fromSourceBatch in fromSource.ToBatchesOfAsync(100, true))
        {
            await dynamoDb.PutItemsAsync(fromSourceBatch.Select(transform));
        }
    }

    public static Task UpdateItemAsync<T>(this IPocoDynamo dynamoDb, T itemToUpdate, Expression<Func<T>> put)
        where T : IDynItem
        => UpdateItemAsync(dynamoDb, itemToUpdate.Id, itemToUpdate.EdgeId, put);

    public static Task UpdateItemAsync<T>(this IPocoDynamo dynamoDb, long hashId, string edgeId, Expression<Func<T>> put)
        => dynamoDb.UpdateItemAsync<T>(new DynamoUpdateItem
                                       {
                                           Hash = hashId,
                                           Range = edgeId,
                                           Put = put.AssignedValues()
                                       });

    public static async Task<T> PutItemTrackedAsync<T>(this IPocoDynamo dynamoDb, T value, T existingValue = null, bool returnOld = false)
        where T : class, IDateTimeTracked
    {
        value.UpdateDateTimeTrackedValues(existingValue);

        var returned = await dynamoDb.PutItemAsync(value, returnOld);

        return returned;
    }

    public static Task<T> PutItemTrackDeferAsync<T>(this IPocoDynamo dynamoDb, T value, RecordType recordType, T existingValue = null, bool returnOld = false)
        where T : class, IDateTimeTracked, IHasLongId
        => PutItemTrackDeferAsync(dynamoDb, value, recordType, value.Id, existingValue, returnOld);

    public static async Task<T> PutItemTrackDeferAsync<T>(this IPocoDynamo dynamoDb, T value, RecordType recordType, long id,
                                                          T existingValue = null, bool returnOld = false)
        where T : class, IDateTimeTracked
    {
        var putItem = await PutItemTrackedAsync(dynamoDb, value, existingValue, returnOld);

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 Ids = new List<long>
                                                       {
                                                           id
                                                       },
                                                 Type = recordType
                                             });

        return putItem;
    }

    public static int ToDynamoBatchCeilingTake(this int limit)
    {
        // Optimize the take to the closest 100 value that is >= the limit requested
        if ((limit % 100) == 0)
        {
            return limit;
        }

        // Integer divide the limit by 100 (truncating to the given integral value), add 1, multiply by 100...
        return ((limit / 100) + 1) * 100;
    }

    public static async Task PutItemDeferAsync<T>(this IPocoDynamo dynamoDb, T value, RecordType recordType)
        where T : DynItem
    {
        await dynamoDb.PutItemAsync(value);

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(value.Id, value.EdgeId)
                                                                },
                                                 Type = recordType
                                             });
    }

    public static async Task PutItemsDeferAsync<T>(this IPocoDynamo dynamoDb, IEnumerable<T> values, RecordType recordType)
        where T : DynItem
    {
        const int publishMessageIdLimit = 50;

        var compositeIds = new List<DynamoItemIdEdge>(publishMessageIdLimit);

        IEnumerable<T> valuesWrapper(IEnumerable<T> wrapedValues)
        {
            foreach (var wrapedValue in wrapedValues)
            {
                yield return wrapedValue;

                compositeIds.Add(new DynamoItemIdEdge(wrapedValue.Id, wrapedValue.EdgeId));

                if (compositeIds.Count < publishMessageIdLimit)
                {
                    continue;
                }

                _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                     {
                                                         CompositeIds = compositeIds,
                                                         Type = recordType
                                                     });

                compositeIds = new List<DynamoItemIdEdge>(publishMessageIdLimit);
            }

            if (compositeIds.Count > 0)
            {
                _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                     {
                                                         CompositeIds = compositeIds,
                                                         Type = recordType
                                                     });
            }
        }

        await dynamoDb.PutItemsAsync(valuesWrapper(values));
    }

    public static async Task PutItemsDeferAsync<T>(this IPocoDynamo dynamoDb, IAsyncEnumerable<T> values, RecordType recordType)
        where T : DynItem
    {
        await foreach (var valueBatch in values.ToBatchesOfAsync(50))
        {
            _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                 {
                                                     CompositeIds = valueBatch.Select(v => new DynamoItemIdEdge(v.Id, v.EdgeId))
                                                                              .AsList(),
                                                     Type = recordType
                                                 });

            await dynamoDb.PutItemsAsync(valueBatch);
        }
    }

    public static async Task<T> PutItemTrackedInterlockedAsync<T>(this IPocoDynamo dynamoDb, T item, Action<T> updateItemBlock, int timeoutSeconds = 15)
        where T : DynItem
    {
        var lockId = string.Concat(item.Id, "|", item.EdgeId);
        var lockCategory = typeof(T).Name;

        var timeoutAt = DateTimeHelper.UtcNowTs + timeoutSeconds.Gz(15);

        do
        {
            using(var lockItem = _distributedLockService.TryGetKeyLock(lockId, lockCategory, timeoutSeconds / 2))
            {
                if (lockItem == null)
                {
                    await Task.Delay(RandomProvider.GetRandomIntBeween(150, 550));

                    continue;
                }

                var existingItem = await dynamoDb.GetItemAsync<T>(item.Id, item.EdgeId);

                var itemToUpdate = existingItem == null || ReferenceEquals(existingItem, item) || existingItem.ModifiedOnUtc <= item.ModifiedOnUtc
                                       ? item
                                       : existingItem;

                updateItemBlock(itemToUpdate);

                await PutItemTrackedAsync(dynamoDb, itemToUpdate, existingItem);

                return itemToUpdate;
            }
        } while (DateTimeHelper.UtcNowTs <= timeoutAt);

        throw new TimeoutException($"Could not get interlock for PutItem operation on [{lockId}-{lockCategory}]");
    }

    public static async Task<T> PutItemTrackedInterlockedDeferAsync<T>(this IPocoDynamo dynamoDb, T item, Action<T> updateItemBlock, RecordType recordType, int timeoutSeconds = 15)
        where T : DynItem
    {
        var toReturn = await PutItemTrackedInterlockedAsync(dynamoDb, item, updateItemBlock, timeoutSeconds);

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(item.Id, item.EdgeId)
                                                                },
                                                 Type = recordType
                                             });

        return toReturn;
    }

    public static async Task TryPutItemMappedDeferAsync<T>(this IPocoDynamo dynamoDb, T value, string mapValue, RecordType recordType)
        where T : IDynItem
    {
        // Try to do the mapped put, without re-throwing the transaction exceptions (they're logged inside the put call)
        try
        {
            await PutItemMappedAsync(dynamoDb, value, mapValue, true);

            _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                 {
                                                     CompositeIds = new List<DynamoItemIdEdge>
                                                                    {
                                                                        new(value.Id, value.EdgeId)
                                                                    },
                                                     Type = recordType
                                                 });
        }
        catch(TransactionCanceledException) { }
        catch(DaxTransactionCanceledException) { }
    }

    public static async Task TryPutItemMappedAsync<T>(this IPocoDynamo dynamoDb, T value, string mapValue)
        where T : IDynItem
    {
        // Try to do the mapped put, without re-throwing the transaction exceptions (they're logged inside the put call)
        try
        {
            await PutItemMappedAsync(dynamoDb, value, mapValue, true);
        }
        catch(TransactionCanceledException) { }
        catch(DaxTransactionCanceledException) { }
    }

    public static async Task PutItemMappedAsync<T>(this IPocoDynamo dynamoDb, T value, string mapValue, bool skipLog = false)
        where T : IDynItem
    {
        Guard.AgainstNullArgument(!mapValue.HasValue(), nameof(mapValue));

        var mapEdgeIdAsLong = _nonZeroNumbers.Contains(mapValue[0])
                                  ? mapValue.ToLong(0)
                                  : 0;

        var mapEdgeId = DynItemMap.BuildEdgeId(value.DynItemType,
                                               mapEdgeIdAsLong > 0
                                                   ? mapEdgeIdAsLong.ToEdgeId()
                                                   : mapValue);

        await PutItemMappedAsync(dynamoDb, value, value.Id, mapEdgeId, value.EdgeId, skipLog);
    }

    public static async Task PutItemMappedAsync<T>(this IPocoDynamo dynamoDb, T value, long mapId, string mapEdgeId, string mapToEdgeId, bool skipLog = false)
    {
        var table = DynamoMetadata.GetTable<T>();

        var request = new TransactWriteItemsRequest
                      {
                          TransactItems = new List<TransactWriteItem>
                                          { // Mapped item must either not already exist, or exist with the same MappedItemEdgeId as that being set in the value
                                              new()
                                              {
                                                  Update = new Update
                                                           {
                                                               TableName = DynItemTypeHelpers.DynamoItemMapsTableName,
                                                               Key = new Dictionary<string, AttributeValue>
                                                                     {
                                                                         {
                                                                             "Id", new AttributeValue
                                                                                   {
                                                                                       N = mapId.ToStringInvariant()
                                                                                   }
                                                                         },
                                                                         {
                                                                             "EdgeId", new AttributeValue
                                                                                       {
                                                                                           S = mapEdgeId
                                                                                       }
                                                                         }
                                                                     },
                                                               ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                                                                                           {
                                                                                               {
                                                                                                   ":itemEdgeId", new AttributeValue
                                                                                                                  {
                                                                                                                      S = mapToEdgeId
                                                                                                                  }
                                                                                               }
                                                                                           },
                                                               ConditionExpression = "attribute_not_exists(MappedItemEdgeId) OR MappedItemEdgeId = :itemEdgeId",
                                                               UpdateExpression = "SET MappedItemEdgeId = if_not_exists(MappedItemEdgeId, :itemEdgeId)"
                                                           }
                                              },
                                              new()
                                              {
                                                  Put = new Put
                                                        {
                                                            TableName = table.Name,
                                                            Item = dynamoDb.Converters.ToAttributeValues(dynamoDb, value, table),
                                                            ReturnValuesOnConditionCheckFailure = ReturnValuesOnConditionCheckFailure.NONE
                                                        }
                                              }
                                          }
                      };

        // Execute
        try
        {
            await dynamoDb.DynamoDb.TransactWriteItemsAsync(request);
        }
        catch(TransactionCanceledException tx) when(!skipLog && _log.LogExceptionReturnFalse(tx, $"mapEdgeId: [{mapEdgeId}], T: [{value.ToJsv().Left(250)}]"))
        { // Unreachable code
            throw;
        }
        catch(DaxTransactionCanceledException dx) when(!skipLog && _log.LogExceptionReturnFalse(dx, $"mapEdgeId: [{mapEdgeId}], T: [{value.ToJsv().Left(250)}]"))
        { // Unreachable code
            throw;
        }
        finally
        {
            MapItemService.DefaultMapItemService.OnMapUpdate(mapId, mapEdgeId);
        }
    }

    public static DynamoId ToItemDynamoId(this long uniqueId)
        => new(uniqueId, uniqueId.ToEdgeId());

    public static DynamoId ToDynamoId(this IHaveIdAndEdgeId item)
        => new(item.Id, item.EdgeId);

    public static DynamoItemIdEdge ToDynamoItemIdEdge(this IHaveIdAndEdgeId item)
        => new()
           {
               Id = item.Id,
               EdgeId = item.EdgeId
           };

    public static string ToDynamoItemIdEdgeCompositeStringId(this IHaveIdAndEdgeId item)
        => DynamoItemIdEdge.GetCompositeStringId(item.Id, item.EdgeId);

    public static T ExecDelayed<T>(this IPocoDynamo dynamo, Func<IPocoDynamo, T> exec)
        => ExecDelayedAsync(dynamo, d =>
                                    {
                                        var result = exec(d);

                                        return Task.FromResult(result);
                                    }).GetAwaiter().GetResult();

    public static async Task<T> ExecDelayedAsync<T>(this IPocoDynamo dynamo, Func<IPocoDynamo, Task<T>> exec)
    {
        const int maxAttempts = 3;

        var attempt = 1;
        RecordNotFoundException lastException = null;

        do
        {
            try
            {
                return await exec(dynamo);
            }
            catch(RecordNotFoundException rnx)
            {
                lastException = rnx;

                if (attempt >= maxAttempts)
                {
                    throw;
                }

                attempt++;
            }

            // Backoff
            await Task.Delay((550 * attempt));
        } while (attempt <= maxAttempts);

        throw lastException;
    }

    public static async Task<TDyn> UpdateFromRefRequestAsync<TRequest, TDyn>(this IPocoDynamo dynamo, TRequest request, long hashAndRefId,
                                                                             DynItemType type, Func<TRequest, TDyn, TDyn> transform)
        where TDyn : DynItem
        where TRequest : RequestBase
    {
        var existing = await GetItemByRefAsync<TDyn>(dynamo, hashAndRefId, type);

        var result = await UpdateFromExistingAsync(dynamo, existing, x => transform(request, x), request);

        return result;
    }

    public static async Task<TDyn> UpdateFromExistingAsync<TRequest, TDyn>(this IPocoDynamo dynamo, TRequest request,
                                                                           long hashKey, object rangeKey, Func<TRequest, TDyn, TDyn> transform)
        where TDyn : BaseDynModel
        where TRequest : RequestBase
    {
        var existing = await dynamo.GetItemAsync<TDyn>(new DynamoId(hashKey, rangeKey));

        var result = await UpdateFromExistingAsync(dynamo, existing, x => transform(request, x), request);

        return result;
    }

    public static async Task<TDyn> UpdateFromExistingAsync<TDyn>(this IPocoDynamo dynamo, TDyn existing, Func<TDyn, TDyn> transform, RequestBase request)
        where TDyn : BaseDynModel
    {
        await ValidateExistingAsync(request, existing);

        var dynModel = transform(existing);

        var result = await DoUpdateFromExistingWithModels(dynamo, existing, dynModel, request);

        return result;
    }

    public static async Task<TDyn> UpdateFromExistingAsync<TDyn>(this IPocoDynamo dynamo, TDyn existing, Func<TDyn, ValueTask<TDyn>> transform, RequestBase request)
        where TDyn : BaseDynModel
    {
        await ValidateExistingAsync(request, existing);

        var dynModel = await transform(existing);

        var result = await DoUpdateFromExistingWithModels(dynamo, existing, dynModel, request);

        return result;
    }

    private static async Task<TDyn> DoUpdateFromExistingWithModels<TDyn>(IPocoDynamo dynamo, TDyn existing, TDyn newModel, RequestBase request)
        where TDyn : BaseDynModel
    {
        existing.PopulateWithNonDefaultValues(newModel);

        if (!request.Unset.IsNullOrEmpty())
        {
            request.Unset.Each(u =>
                               {
                                   var propName = existing.UnsetNameToPropertyName(u);

                                   var propInfo = typeof(TDyn).GetPublicProperty(propName);

                                   if (propInfo == null)
                                   {
                                       return;
                                   }

                                   propInfo.SetValue(existing, null);
                               });
        }

        await dynamo.PutItemAsync(existing);

        return existing;
    }

    private static Task ValidateExistingAsync<TRequest, TDyn>(TRequest request, TDyn existing)
        where TDyn : BaseDynModel
        where TRequest : IHasUserAuthorizationInfo
    {
        if (existing == null || existing.IsDeleted())
        {
            throw new RecordNotFoundException();
        }

        return _authorizationService.VerifyAccessToAsync(existing, request);
    }

    public static DynItemEdgeIdGlobalIndex GetItemEdgeIndex(this IPocoDynamo dynamo, DynItemType itemType, string edgeId,
                                                            bool includeDeleted = false, bool ignoreRecordNotFound = false)
    { // Safe here to just lookup/store by the index model, as everything in there is read-only
        var indexModel = _localRequestCacheClientFactory().TryGet(string.Concat(edgeId, "|", itemType),
                                                                  () => dynamo.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == edgeId &&
                                                                                                                             Dynamo.BeginsWith(i.TypeReference,
                                                                                                                                               string.Concat((int)itemType, "|")))
                                                                              .Exec()
                                                                              .SingleOrDefault(),
                                                                  CacheConfig.LongConfig);

        if (!ignoreRecordNotFound)
        {
            Guard.AgainstRecordNotFound(indexModel == null, edgeId);

            if (!includeDeleted)
            {
                Guard.AgainstRecordNotFound(indexModel.DeletedOnUtc.HasValue, edgeId);
            }
        }

        return indexModel;
    }

    public static async Task<DynItemEdgeIdGlobalIndex> GetItemEdgeIndexAsync(this IPocoDynamo dynamo, DynItemType itemType, string edgeId,
                                                                             bool includeDeleted = false, bool ignoreRecordNotFound = false)
    { // Safe here to just lookup/store by the index model, as everything in there is read-only
        var indexModel = await _localRequestCacheClientFactory().TryGetAsync(string.Concat(edgeId, "|", itemType),
                                                                             () => dynamo.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == edgeId &&
                                                                                                                                        Dynamo.BeginsWith(i.TypeReference,
                                                                                                                                                          string.Concat((int)itemType, "|")))
                                                                                         .ExecAsync()
                                                                                         .SingleOrDefaultAsync(),
                                                                             CacheConfig.LongConfig);

        if (!ignoreRecordNotFound)
        {
            Guard.AgainstRecordNotFound(indexModel == null, edgeId);

            if (!includeDeleted)
            {
                Guard.AgainstRecordNotFound(indexModel.DeletedOnUtc.HasValue, edgeId);
            }
        }

        return indexModel;
    }

    public static TModel GetItemByEdgeInto<TModel>(this IPocoDynamo dynamo, DynItemType itemType, string edgeId, bool ignoreRecordNotFound = false)
        where TModel : DynItem
    {
        var indexModel = GetItemEdgeIndex(dynamo, itemType, edgeId, ignoreRecordNotFound: ignoreRecordNotFound);

        var model = indexModel == null
                        ? null
                        : dynamo.GetItem<TModel>(indexModel.GetDynamoId());

        return model;
    }

    public static Task<TModel> GetItemByEdgeIntoAsync<TModel>(this IPocoDynamo dynamo, DynItemType itemType, long edgeId)
        where TModel : DynItem
        => GetItemByEdgeIntoAsync<TModel>(dynamo, itemType, edgeId.ToEdgeId());

    public static async Task<TModel> GetItemByEdgeIntoAsync<TModel>(this IPocoDynamo dynamo, DynItemType itemType, string edgeId, bool ignoreRecordNotFound = false)
        where TModel : DynItem
    {
        var indexModel = await GetItemEdgeIndexAsync(dynamo, itemType, edgeId, ignoreRecordNotFound: ignoreRecordNotFound);

        var model = indexModel == null
                        ? null
                        : await dynamo.GetItemAsync<TModel>(indexModel.GetDynamoId());

        return model;
    }

    public static TModel GetItemByRef<TModel>(this IPocoDynamo dynamo, long id, string refId, DynItemType itemType,
                                              bool includeDeleted = false, bool ignoreRecordNotFound = false)
        where TModel : DynItem
    {
        if (!refId.HasValue())
        {
            return null;
        }

        var dynItemByRefId = _localRequestCacheClientFactory().TryGet(string.Concat(id, "|RefId:", refId, "|", itemType),
                                                                      () =>
                                                                      {
                                                                          var model = dynamo.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == id &&
                                                                                                                                                    i.TypeReference == DynItem.BuildTypeReferenceHash(itemType, refId))
                                                                                            .Exec()
                                                                                            .SingleOrDefault(m => m.TypeId == (int)itemType);

                                                                          return model == null || model.Id <= 0 || model.EdgeId.IsNullOrEmpty()
                                                                                     ? null
                                                                                     : new DynamoId(model.Id, model.EdgeId);
                                                                      },
                                                                      CacheConfig.LongConfig);

        // And now get the item - if it were already fetched above, it's a cheap cache lookup (in-memory), and this avoids and cache-clear issues
        var dyItemByRef = dynItemByRefId == null
                              ? null
                              : dynamo.GetItem<TModel>(dynItemByRefId);

        if (!ignoreRecordNotFound)
        {
            Guard.AgainstRecordNotFound(dyItemByRef == null, refId);

            if (!includeDeleted)
            {
                Guard.AgainstRecordNotFound(dyItemByRef.IsDeleted(), refId);
            }
        }

        return dyItemByRef;
    }

    public static Task<TModel> GetItemByRefAsync<TModel>(this IPocoDynamo dynamo, long idAndRefId, DynItemType itemType, bool includeDeleted = false)
        where TModel : DynItem
        => GetItemByRefAsync<TModel>(dynamo, idAndRefId, idAndRefId.ToStringInvariant(), itemType, includeDeleted);

    public static async Task<TModel> GetItemByRefAsync<TModel>(this IPocoDynamo dynamo, long id, string refId, DynItemType itemType,
                                                               bool includeDeleted = false, bool ignoreRecordNotFound = false)
        where TModel : DynItem
    {
        if (!refId.HasValue())
        {
            return null;
        }

        var dynItemByRefId = await _localRequestCacheClientFactory().TryGetAsync(string.Concat(id, "|RefId:", refId, "|", itemType),
                                                                                 () => dynamo.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == id &&
                                                                                                                                                     i.TypeReference == DynItem.BuildTypeReferenceHash(itemType, refId))
                                                                                             .ExecAsync()
                                                                                             .SingleOrDefaultAsync(m => m.TypeId == (int)itemType)
                                                                                             .Then(i => i is not { Id: > 0 } || i.EdgeId.IsNullOrEmpty()
                                                                                                            ? null
                                                                                                            : new DynamoId(i.Id, i.EdgeId)),
                                                                                 CacheConfig.LongConfig);

        // And now get the item - if it were already fetched above, it's a cheap cache lookup (in-memory), and this avoids and cache-clear issues
        var dyItemByRef = dynItemByRefId == null
                              ? null
                              : await dynamo.GetItemAsync<TModel>(dynItemByRefId);

        if (!ignoreRecordNotFound)
        {
            Guard.AgainstRecordNotFound(dyItemByRef == null, refId);

            if (!includeDeleted)
            {
                Guard.AgainstRecordNotFound(dyItemByRef.IsDeleted(), refId);
            }
        }

        return dyItemByRef;
    }

    public static async Task TryDeleteItemAsync<T>(this IPocoDynamo dynamo, long id, string edgeId)
    {
        try
        {
            await dynamo.DeleteItemAsync<T>(id, edgeId);
        }
        catch(Exception x)
        {
            _log.Exception(x, $"TryDeleteItem failed for item [{typeof(T).Name}.{id}.{edgeId}]", true);
        }
    }

    public static async Task TryDeleteItemAsync<T>(this IPocoDynamo dynamo, DynamoId dynamoId)
    {
        try
        {
            await dynamo.DeleteItemAsync<T>(dynamoId);
        }
        catch(Exception x)
        {
            _log.Exception(x, $"TryDeleteItem failed for item [{typeof(T).Name}.{dynamoId.Hash}.{dynamoId.Range}]", true);
        }
    }

    public static Task<T> SoftDeleteByRefIdAsync<T>(this IPocoDynamo dynamo, long idAndRefId, DynItemType type, IHasUserAuthorizationInfo state = null)
        where T : DynItem
        => SoftDeleteByRefIdAsync<T>(dynamo, idAndRefId, idAndRefId, type, state);

    public static async Task<T> SoftDeleteByRefIdAsync<T>(this IPocoDynamo dynamo, long id, long refId, DynItemType type, IHasUserAuthorizationInfo state = null)
        where T : DynItem
    {
        var existingItem = await GetItemByRefAsync<T>(dynamo, id, refId.ToStringInvariant(), type, ignoreRecordNotFound: true);

        var result = await DoSoftDeleteItemAsync(dynamo, existingItem, state);

        return result;
    }

    public static async Task<T> SoftDeleteByIdAsync<T>(this IPocoDynamo dynamo, object hash, object range, IHasUserAuthorizationInfo state = null)
        where T : BaseDynModel
    {
        var existingItem = await dynamo.GetItemAsync<T>(hash, range);

        var result = await DoSoftDeleteItemAsync(dynamo, existingItem, state);

        return result;
    }

    public static Task<T> SoftDeleteAsync<T>(this IPocoDynamo dynamo, T existingItem, IHasUserAuthorizationInfo state = null,
                                             bool isSystemRequest = false)
        where T : BaseDynModel
        => DoSoftDeleteItemAsync(dynamo, existingItem, state, isSystemRequest);

    public static async Task<bool> SoftUnDeleteAsync<T>(this IPocoDynamo dynamo, T itemToUnDelete, IHasUserAuthorizationInfo state = null)
        where T : BaseDynModel
    {
        if (itemToUnDelete == null || !itemToUnDelete.IsDeleted())
        {
            return false;
        }

        await _authorizationService.VerifyAccessToAsync(itemToUnDelete, state);

        itemToUnDelete.UpdateDateTimeTrackedValues(state);
        itemToUnDelete.DeletedBy = null;
        itemToUnDelete.DeletedOnUtc = null;

        await dynamo.PutItemAsync(itemToUnDelete);

        return true;
    }

    private static async Task<T> DoSoftDeleteItemAsync<T>(IPocoDynamo dynamo, T itemToDelete, IHasUserAuthorizationInfo state = null,
                                                          bool isSystemRequest = false)
        where T : BaseDynModel
    {
        if (itemToDelete == null || itemToDelete.IsDeleted())
        {
            return itemToDelete;
        }

        if (isSystemRequest)
        {
            _requestStateManager.UpdateStateToSystemRequest();
        }

        await _authorizationService.VerifyAccessToAsync(itemToDelete, state);

        itemToDelete.UpdateDateTimeTrackedValues(state);
        itemToDelete.UpdateDateTimeDeleteTrackedValuesOnly(state);

        await dynamo.PutItemAsync(itemToDelete);

        return itemToDelete;
    }
}
