// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Linq.Expressions;
// using System.Threading;
// using System.Threading.Tasks;
// using Amazon.DynamoDBv2;
// using Amazon.DynamoDBv2.DocumentModel;
// using Amazon.DynamoDBv2.Model;
// using Rydr.Api.Core.Extensions;
// using Rydr.Api.Core.Interfaces.Models;
// using Rydr.Api.Core.Models.Doc;
// using Rydr.Api.Core.Models.Internal;
// using Rydr.Api.Core.Services.Internal;
// using ServiceStack;
// using ServiceStack.Aws.DynamoDb;
// using ServiceStack.Caching;
// using ServiceStack.OrmLite.Dapper;
//
// namespace Rydr.Api.Core.DataAccess
// {
//     public class CachedPocoDynamo : IPocoDynamo
//     {
//         private readonly IPocoDynamo _dynamoDb;
//         private readonly Func<ICacheClient> _cacheClientFactory;
//
//         public CachedPocoDynamo(IPocoDynamo dynamoDb, Func<ICacheClient> cacheClientFactory)
//         {
//             _dynamoDb = dynamoDb;
//             _cacheClientFactory = cacheClientFactory;
//         }
//
//         private string GetCacheKey(DynamoId dynId)
//             => GetCacheKey(dynId.Hash, dynId.Range);
//
//         private string GetCacheKey(object hash, object range = null)
//             => string.Concat(hash.ToString(), "|", range == null
//                                                        ? string.Empty
//                                                        : range.ToString());
//
//         private ICacheClient GetCacheClient() => _cacheClientFactory();
//
//         public Task<bool> CreateMissingTablesAsync(IEnumerable<DynamoMetadataType> tables, CancellationToken token =  new CancellationToken())
//             => _dynamoDb.CreateMissingTablesAsync(tables, token);
//
//         public Task<bool> WaitForTablesToBeReadyAsync(IEnumerable<string> tableNames, CancellationToken token = new CancellationToken())
//             => _dynamoDb.WaitForTablesToBeReadyAsync(tableNames, token);
//
//         public Task InitSchemaAsync()
//         {
//             
//         }
//
//         public void InitSchema()
//             => CreateMissingTables(DynamoMetadata.GetTables());
//
//         public Table GetTableSchema(Type table)
//             => _dynamoDb.GetTableSchema(table);
//
//         public DynamoMetadataType GetTableMetadata(Type table)
//             => _dynamoDb.GetTableMetadata(table);
//
//         public IEnumerable<string> GetTableNames()
//             => _dynamoDb.GetTableNames();
//
//         public bool CreateMissingTables(IEnumerable<DynamoMetadataType> tables, TimeSpan? timeout = null)
//         {
//             var existingTableNames = GetTableNames().AsHashSet(StringComparer.OrdinalIgnoreCase);
//
//             var tablesToCreate = tables.Safe()
//                                        .Where(m => !existingTableNames.Contains(m.Name))
//                                        .GroupBy(t => t.Name)
//                                        .Select(g => g.First())
//                                        .AsList();
//
//             return tablesToCreate.IsNullOrEmpty() || CreateTables(tablesToCreate, timeout);
//         }
//
//         public bool CreateTables(IEnumerable<DynamoMetadataType> tables, TimeSpan? timeout = null)
//             => _dynamoDb.CreateTables(tables, timeout);
//
//         public bool WaitForTablesToBeReady(IEnumerable<string> tableNames, TimeSpan? timeout = null)
//             => _dynamoDb.WaitForTablesToBeReady(tableNames, timeout);
//
//         public bool DeleteAllTables(TimeSpan? timeout = null)
//             => _dynamoDb.DeleteAllTables(timeout);
//
//         public bool DeleteTables(IEnumerable<string> tableNames, TimeSpan? timeout = null)
//             => _dynamoDb.DeleteTables(tableNames, timeout);
//
//         public bool WaitForTablesToBeDeleted(IEnumerable<string> tableNames, TimeSpan? timeout = null)
//             => _dynamoDb.WaitForTablesToBeDeleted(tableNames, timeout);
//
//         public async Task<T> GetItemAsync<T>(object hash, bool? consistentRead = null)
//         {
//             var cacheClient = GetCacheClient();
//
//             if (!consistentRead.HasValue || !consistentRead.Value)
//             {
//                 return await cacheClient.TryGetAsync(GetCacheKey(hash),
//                                                      () => _dynamoDb.GetItemAsync<T>(hash),
//                                                      CacheConfig.LongConfig);
//             }
//
//             var item = await _dynamoDb.GetItemAsync<T>(hash, true);
//
//             await cacheClient.TrySetAsync(item, GetCacheKey(hash), CacheConfig.LongConfig);
//
//             return item;
//         }
//
//         public async Task<T> GetItemAsync<T>(DynamoId id)
//         {
//             var cacheClient = GetCacheClient();
//
//             if (!consistentRead.HasValue || !consistentRead.Value)
//             {
//                 return await cacheClient.TryGetAsync(GetCacheKey(id),
//                                                      () => _dynamoDb.GetItemAsync<T>(id),
//                                                      CacheConfig.LongConfig);
//             }
//
//             var item = await _dynamoDb.GetItemAsync<T>(id);
//
//             await cacheClient.TrySetAsync(item, GetCacheKey(id), CacheConfig.LongConfig);
//
//             return item;
//         }
//
//         public async Task<T> GetItemAsync<T>(object hash, object range, bool? consistentRead = null)
//         {
//             var cacheClient = GetCacheClient();
//
//             if (!consistentRead.HasValue || !consistentRead.Value)
//             {
//                 return await cacheClient.TryGetAsync(GetCacheKey(hash, range),
//                                                      () => _dynamoDb.GetItemAsync<T>(hash, range),
//                                                      CacheConfig.LongConfig);
//             }
//
//             var item = await _dynamoDb.GetItemAsync<T>(hash, range, true);
//
//             await cacheClient.TrySetAsync(item, GetCacheKey(hash, range), CacheConfig.LongConfig);
//
//             return item;
//         }
//
//         public T GetItem<T>(object hash, bool? consistentRead = null)
//         {
//             if (!consistentRead.HasValue || !consistentRead.Value)
//             {
//                 return GetCacheClient().TryGet(GetCacheKey(hash), () => _dynamoDb.GetItem<T>(hash), CacheConfig.LongConfig);
//             }
//
//             var item = _dynamoDb.GetItem<T>(hash, true);
//
//             GetCacheClient().TrySet(item, GetCacheKey(hash), CacheConfig.LongConfig);
//
//             return item;
//         }
//
//         public T GetItem<T>(DynamoId id, bool? consistentRead = null)
//         {
//             if (!consistentRead.HasValue || !consistentRead.Value)
//             {
//                 return GetCacheClient().TryGet(GetCacheKey(id), () => _dynamoDb.GetItem<T>(id), CacheConfig.LongConfig);
//             }
//
//             var item = _dynamoDb.GetItem<T>(id, true);
//
//             GetCacheClient().TrySet(item, GetCacheKey(id), CacheConfig.LongConfig);
//
//             return item;
//         }
//
//         public T GetItem<T>(object hash, object range, bool? consistentRead = null)
//         {
//             if (!consistentRead.HasValue || !consistentRead.Value)
//             {
//                 return GetCacheClient().TryGet(GetCacheKey(hash, range), () => _dynamoDb.GetItem<T>(hash, range), CacheConfig.LongConfig);
//             }
//
//             var item = _dynamoDb.GetItem<T>(hash, range, true);
//
//             GetCacheClient().TrySet(item, GetCacheKey(hash, range), CacheConfig.LongConfig);
//
//             return item;
//         }
//
//         public IAsyncEnumerable<T> GetItemsAsync<T>(IEnumerable<object> hashes, bool? consistentRead = null)
//             => _dynamoDb.GetItemsAsync<T>(hashes, consistentRead);
//
//         public IAsyncEnumerable<T> GetItemsAsync<T>(IEnumerable<DynamoId> ids, bool? consistentRead = null)
//             => _dynamoDb.GetItemsAsync<T>(ids, consistentRead);
//
//         public List<T> GetItems<T>(IEnumerable<object> hashes, bool? consistentRead = null)
//             => _dynamoDb.GetItems<T>(hashes, consistentRead);
//
//         public List<T> GetItems<T>(IEnumerable<DynamoId> ids, bool? consistentRead = null)
//             => _dynamoDb.GetItems<T>(ids, consistentRead);
//
//         public async Task<T> PutItemAsync<T>(T value, bool returnOld = false)
//         {
//             T toReturn = default;
//
//             if (value is IHaveMappedEdgeId valueMapped)
//             { // Map the id of this item with an id/edgeid that gives a map to the actual id/edgeId combo for the items themselves
//                 // This essentially ends up creating a DynItemMap record with an Id and EdgeId of the valueMapped.Id, to allow batch lookups on
//                 // this item by Id only (i.e. do a GetItems() on the id as the id and edge) which then gives you the EdgeId of the DynItem
//                 // with the given Id to then batch-get into the DynItems table on
//                 await _dynamoDb.PutItemMappedAsync(value, valueMapped.Id, DynItemMap.BuildEdgeId(valueMapped.DynItemType, valueMapped.Id.ToEdgeId()), valueMapped.EdgeId);
//             }
//             else
//             {
//                 toReturn = await _dynamoDb.PutItemAsync(value, returnOld);
//             }
//
//             if (!(value is DynItem di))
//             {
//                 return toReturn;
//             }
//
//             await GetCacheClient().TrySetAsync(value, GetCacheKey(di.Id, di.EdgeId), CacheConfig.LongConfig);
//
//             return toReturn;
//         }
//
//         public T PutItem<T>(T value, bool returnOld = false)
//         {
//             T toReturn = default;
//
//             if (value is IHaveMappedEdgeId valueMapped)
//             { // Map the id of this item with an id/edgeid that gives a map to the actual id/edgeId combo for the items themselves
//                 _dynamoDb.PutItemMappedAsync(value, valueMapped.Id, DynItemMap.BuildEdgeId(valueMapped.DynItemType, valueMapped.Id.ToEdgeId()), valueMapped.EdgeId)
//                          .GetAwaiter().GetResult();
//             }
//             else
//             {
//                 toReturn = _dynamoDb.PutItem(value, returnOld);
//             }
//
//             if (!(value is DynItem di))
//             {
//                 return toReturn;
//             }
//
//             GetCacheClient().TrySet(value, GetCacheKey(di.Id, di.EdgeId), CacheConfig.LongConfig);
//
//             return toReturn;
//         }
//
//         public UpdateExpression<T> UpdateExpression<T>(object hash, object range = null)
//             => _dynamoDb.UpdateExpression<T>(hash, range);
//
//         public async Task<bool> UpdateItemAsync<T>(UpdateExpression<T> update)
//         {
//             var hash = update.Key.ContainsKey("Id")
//                            ? update.Key["Id"].N
//                            : null;
//
//             var range = hash != null && update.Key.ContainsKey("EdgeId")
//                             ? update.Key["EdgeId"].S
//                             : null;
//
//             var result = await _dynamoDb.UpdateItemAsync(update);
//
//             if (hash != null)
//             {
//                 GetCacheClient().TryRemove<T>(GetCacheKey(hash, range));
//             }
//
//             return result;
//         }
//
//         public bool UpdateItem<T>(UpdateExpression<T> update)
//         {
//             var hash = update.Key.ContainsKey("Id")
//                            ? update.Key["Id"].N
//                            : null;
//
//             var range = hash != null && update.Key.ContainsKey("EdgeId")
//                             ? update.Key["EdgeId"].S
//                             : null;
//
//             var result = _dynamoDb.UpdateItem(update);
//
//             if (hash != null)
//             {
//                 GetCacheClient().TryRemove<T>(GetCacheKey(hash, range));
//             }
//
//             return result;
//         }
//
//         public async Task UpdateItemAsync<T>(DynamoUpdateItem update)
//         {
//             GetCacheClient().TryRemove<T>(GetCacheKey(update.Hash, update.Range));
//
//             await _dynamoDb.UpdateItemAsync<T>(update);
//         }
//
//         public void UpdateItem<T>(DynamoUpdateItem update)
//         {
//             GetCacheClient().TryRemove<T>(GetCacheKey(update.Hash, update.Range));
//
//             _dynamoDb.UpdateItem<T>(update);
//         }
//
//         public T UpdateItemNonDefaults<T>(T value, bool returnOld = false)
//         {
//             var toReturn = _dynamoDb.UpdateItemNonDefaults(value, returnOld);
//
//             if (!(value is DynItem di))
//             {
//                 return toReturn;
//             }
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(di.Id, di.EdgeId));
//
//             return toReturn;
//         }
//
//         public async Task PutItemsAsync<T>(IEnumerable<T> items)
//         {
//             if (typeof(T).ImplementsInterface<IHaveMappedEdgeId>())
//             {
//                 throw new InvalidOperationException("Batch PutItems cannot be used with an object implementing a mappedEdge");
//             }
//
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<T> getLocalItems(IEnumerable<T> localItems)
//             {
//                 foreach (var localItem in localItems)
//                 {
//                     yield return localItem;
//
//                     if (!(localItem is DynItem di))
//                     {
//                         continue;
//                     }
//
//                     cacheClient.TryRemove<T>(GetCacheKey(di.Id, di.EdgeId));
//                 }
//             }
//
//             await _dynamoDb.PutItemsAsync(getLocalItems(items));
//         }
//
//         public void PutItems<T>(IEnumerable<T> items)
//         {
//             if (typeof(T).ImplementsInterface<IHaveMappedEdgeId>())
//             {
//                 throw new InvalidOperationException("Batch PutItems cannot be used with an object implementing a mappedEdge");
//             }
//
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<T> getLocalItems(IEnumerable<T> localItems)
//             {
//                 foreach (var localItem in localItems)
//                 {
//                     yield return localItem;
//
//                     if (!(localItem is DynItem di))
//                     {
//                         continue;
//                     }
//
//                     cacheClient.TryRemove<T>(GetCacheKey(di.Id, di.EdgeId));
//                 }
//             }
//
//             _dynamoDb.PutItems(getLocalItems(items));
//         }
//
//         public async Task<T> DeleteItemAsync<T>(object hash, ReturnItem returnItem = ReturnItem.None)
//         {
//             var toReturn = await _dynamoDb.DeleteItemAsync<T>(hash, returnItem);
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(hash));
//
//             return toReturn;
//         }
//
//         public async Task<T> DeleteItemAsync<T>(DynamoId id, ReturnItem returnItem = ReturnItem.None)
//         {
//             var toReturn = await _dynamoDb.DeleteItemAsync<T>(id, returnItem);
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(id));
//
//             return toReturn;
//         }
//
//         public async Task<T> DeleteItemAsync<T>(object hash, object range, ReturnItem returnItem = ReturnItem.None)
//         {
//             var toReturn = await _dynamoDb.DeleteItemAsync<T>(hash, range, returnItem);
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(hash, range));
//
//             return toReturn;
//         }
//
//         public async Task DeleteItemsAsync<T>(IEnumerable<object> hashes)
//         {
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<object> getLocalHashes(IEnumerable<object> localHashes)
//             {
//                 foreach (var localHash in localHashes)
//                 {
//                     yield return localHash;
//
//                     cacheClient.TryRemove<T>(GetCacheKey(localHash));
//                 }
//             }
//
//             await _dynamoDb.DeleteItemsAsync<T>(getLocalHashes(hashes));
//         }
//
//         public async Task DeleteItemsAsync<T>(IEnumerable<DynamoId> ids)
//         {
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<DynamoId> getLocalIds(IEnumerable<DynamoId> localIds)
//             {
//                 foreach (var localId in localIds)
//                 {
//                     yield return localId;
//
//                     cacheClient.TryRemove<T>(GetCacheKey(localId));
//                 }
//             }
//
//             await _dynamoDb.DeleteItemsAsync<T>(getLocalIds(ids));
//         }
//
//         public T DeleteItem<T>(object hash, ReturnItem returnItem = ReturnItem.None)
//         {
//             var toReturn = _dynamoDb.DeleteItem<T>(hash, returnItem);
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(hash));
//
//             return toReturn;
//         }
//
//         public T DeleteItem<T>(DynamoId id, ReturnItem returnItem = ReturnItem.None)
//         {
//             var toReturn = _dynamoDb.DeleteItem<T>(id, returnItem);
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(id));
//
//             return toReturn;
//         }
//
//         public T DeleteItem<T>(object hash, object range, ReturnItem returnItem = ReturnItem.None)
//         {
//             var toReturn = _dynamoDb.DeleteItem<T>(hash, range, returnItem);
//
//             GetCacheClient().TryRemove<T>(GetCacheKey(hash, range));
//
//             return toReturn;
//         }
//
//         public void DeleteItems<T>(IEnumerable<object> hashes)
//         {
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<object> getLocalHashes(IEnumerable<object> localHashes)
//             {
//                 foreach (var localHash in localHashes)
//                 {
//                     yield return localHash;
//
//                     cacheClient.TryRemove<T>(GetCacheKey(localHash));
//                 }
//             }
//
//             _dynamoDb.DeleteItems<T>(getLocalHashes(hashes));
//         }
//
//         public void DeleteItems<T>(IEnumerable<DynamoId> ids)
//         {
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<DynamoId> getLocalIds(IEnumerable<DynamoId> localIds)
//             {
//                 foreach (var localId in localIds)
//                 {
//                     yield return localId;
//
//                     cacheClient.TryRemove<T>(GetCacheKey(localId));
//                 }
//             }
//
//             _dynamoDb.DeleteItems<T>(getLocalIds(ids));
//         }
//
//         public Task<long> IncrementAsync<T>(object hash, string fieldName, long amount = 1)
//             => _dynamoDb.IncrementAsync<T>(hash, fieldName, amount);
//
//         public long Increment<T>(object hash, string fieldName, long amount = 1)
//             => _dynamoDb.Increment<T>(hash, fieldName, amount);
//
//         public void PutRelatedItem<T>(object hash, T item)
//         {
//             _dynamoDb.PutRelatedItem(hash, item);
//
//             if (!(item is DynItem di))
//             {
//                 return;
//             }
//
//             GetCacheClient().TrySet(item, GetCacheKey(di.Id, di.EdgeId), CacheConfig.LongConfig);
//         }
//
//         public void PutRelatedItems<T>(object hash, IEnumerable<T> items)
//         {
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<T> getLocalItems(IEnumerable<T> localItems)
//             {
//                 foreach (var localItem in localItems)
//                 {
//                     yield return localItem;
//
//                     if (!(localItem is DynItem di))
//                     {
//                         continue;
//                     }
//
//                     cacheClient.TrySet(localItem, GetCacheKey(di.Id, di.EdgeId), CacheConfig.LongConfig);
//                 }
//             }
//
//             _dynamoDb.PutRelatedItems(hash, getLocalItems(items));
//         }
//
//         public IEnumerable<T> GetRelatedItems<T>(object hash)
//             => _dynamoDb.GetRelatedItems<T>(hash);
//
//         public void DeleteRelatedItems<T>(object hash, IEnumerable<object> ranges)
//         {
//             var cacheClient = GetCacheClient();
//
//             IEnumerable<object> getLocalRanges(IEnumerable<object> localRanges)
//             {
//                 foreach (var localRange in localRanges)
//                 {
//                     yield return localRange;
//
//                     cacheClient.TryRemove<T>(GetCacheKey(hash, localRange));
//                 }
//             }
//
//             _dynamoDb.DeleteRelatedItems<T>(hash, getLocalRanges(ranges));
//         }
//
//         public IAsyncEnumerable<T> ScanAsync<T>(ScanRequest request, Func<ScanResponse, IEnumerable<T>> converter)
//             => _dynamoDb.ScanAsync(request, converter);
//
//         public IAsyncEnumerable<T> ScanAsync<T>(ScanExpression<T> request, int limit)
//             => _dynamoDb.ScanAsync(request, limit);
//
//         public IAsyncEnumerable<T> ScanAsync<T>(ScanExpression<T> request)
//             => _dynamoDb.ScanAsync(request);
//
//         public IAsyncEnumerable<T> ScanAsync<T>(ScanRequest request, int limit)
//             => _dynamoDb.ScanAsync<T>(request, limit);
//
//         public IAsyncEnumerable<T> ScanAsync<T>(ScanRequest request)
//             => _dynamoDb.ScanAsync<T>(request);
//
//         public IEnumerable<T> ScanAll<T>()
//             => _dynamoDb.ScanAll<T>();
//
//         public ScanExpression<T> FromScan<T>(Expression<Func<T, bool>> filterExpression = null)
//             => _dynamoDb.FromScan(filterExpression);
//
//         public ScanExpression<T> FromScanIndex<T>(Expression<Func<T, bool>> filterExpression = null)
//             => _dynamoDb.FromScanIndex(filterExpression);
//
//         public List<T> Scan<T>(ScanExpression<T> request, int limit)
//             => _dynamoDb.Scan(request, limit);
//
//         public IEnumerable<T> Scan<T>(ScanExpression<T> request)
//             => _dynamoDb.Scan(request);
//
//         public List<T> Scan<T>(ScanRequest request, int limit)
//             => _dynamoDb.Scan<T>(request, limit);
//
//         public IEnumerable<T> Scan<T>(ScanRequest request)
//             => _dynamoDb.Scan<T>(request);
//
//         public IEnumerable<T> Scan<T>(ScanRequest request, Func<ScanResponse, IEnumerable<T>> converter)
//             => _dynamoDb.Scan(request, converter);
//
//         public IAsyncEnumerable<T> QueryAsync<T>(QueryExpression<T> request, int limit)
//             => _dynamoDb.QueryAsync(request, limit);
//
//         public IAsyncEnumerable<T> QueryAsync<T>(QueryRequest request, int limit)
//             => _dynamoDb.QueryAsync<T>(request, limit);
//
//         public IAsyncEnumerable<T> QueryAsync<T>(QueryRequest request)
//             => _dynamoDb.QueryAsync<T>(request);
//
//         public IAsyncEnumerable<T> QueryAsync<T>(QueryRequest request, Func<QueryResponse, IEnumerable<T>> converter)
//             => _dynamoDb.QueryAsync(request, converter);
//
//         public QueryExpression<T> FromQuery<T>(Expression<Func<T, bool>> keyExpression = null)
//             => _dynamoDb.FromQuery(keyExpression);
//
//         public IEnumerable<T> Query<T>(QueryExpression<T> request)
//             => _dynamoDb.Query(request);
//
//         public List<T> Query<T>(QueryExpression<T> request, int limit)
//             => _dynamoDb.Query(request, limit);
//
//         public QueryExpression<T> FromQueryIndex<T>(Expression<Func<T, bool>> keyExpression = null)
//             => _dynamoDb.FromQueryIndex(keyExpression);
//
//         public List<T> Query<T>(QueryRequest request, int limit)
//             => _dynamoDb.Query<T>(request, limit);
//
//         public IEnumerable<T> Query<T>(QueryRequest request)
//             => _dynamoDb.Query<T>(request);
//
//         public IEnumerable<T> Query<T>(QueryRequest request, Func<QueryResponse, IEnumerable<T>> converter)
//             => _dynamoDb.Query(request, converter);
//
//         public long ScanItemCount<T>()
//             => _dynamoDb.ScanItemCount<T>();
//
//         public long DescribeItemCount<T>()
//             => _dynamoDb.DescribeItemCount<T>();
//
//         public IPocoDynamo ClientWith(bool? consistentRead = null, long? readCapacityUnits = null, long? writeCapacityUnits = null, TimeSpan? pollTableStatus = null,
//                                       TimeSpan? maxRetryOnExceptionTimeout = null, int? limit = null, bool? scanIndexForward = null)
//         {
//             var realPoco = _dynamoDb.ClientWith(consistentRead, readCapacityUnits, writeCapacityUnits, pollTableStatus,
//                                                 maxRetryOnExceptionTimeout, limit, scanIndexForward);
//
//             var cacheClient = GetCacheClient();
//
//             return new CachedPocoDynamo(realPoco, () => cacheClient);
//         }
//
//         public void Close() => _dynamoDb.Close();
//         public IAmazonDynamoDB DynamoDb => _dynamoDb.DynamoDb;
//         public ISequenceSource Sequences => _dynamoDb.Sequences;
//         public DynamoConverters Converters => _dynamoDb.Converters;
//         public TimeSpan MaxRetryOnExceptionTimeout => _dynamoDb.MaxRetryOnExceptionTimeout;
//     }
// }


