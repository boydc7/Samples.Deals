using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using ServiceStack.Caching;

namespace Rydr.Api.Core.DataAccess
{
    public class CachedStorageMarshaller : IStorageMarshaller
    {
        private readonly ICacheClient _cacheClient;

        public CachedStorageMarshaller(ICacheClient cacheClient)
        {
            _cacheClient = cacheClient;
        }

        public T Get<T, TIdType>(TIdType id, Func<TIdType, T> getter)
            => _cacheClient.TryGet(id.ToString(), () => getter(id));

        public Task<T> GetAsync<T, TIdType>(TIdType id, Func<TIdType, Task<T>> getter)
            => _cacheClient.TryGetAsync(id.ToString(), () => getter(id));

        public IEnumerable<T> Query<T, TIdType>(Func<IEnumerable<T>> query, Func<T, TIdType> idResolver, bool intentToUpdate = false)
        {
            if (intentToUpdate || idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)) || typeof(T).HasLoadReferences())
            {
                return query();
            }

            return query().Select(e =>
                                  {
                                      _cacheClient.TrySet(e, idResolver(e).ToString());

                                      return e;
                                  });
        }

        public void DeleteAdHoc<T>(Action action)
        {
            if (_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                throw new InvalidApplicationStateException($"Cannot use AdHoc deletion method on protected model [{typeof(T).Name}]");
            }

            action();
        }

        public Task DeleteAdHocAsync<T>(Func<Task> action)
        {
            if (_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                throw new InvalidApplicationStateException($"Cannot use AdHoc deletion method on protected model [{typeof(T).Name}]");
            }

            return action();
        }

        public T QueryAdHoc<T>(Func<T> query)
            => query();

        public Task<T> QueryAdHocAsync<T>(Func<Task<T>> query)
            => query();

        public async Task<IEnumerable<T>> QueryAsync<T, TIdType>(Func<Task<List<T>>> query, Func<T, TIdType> idResolver, bool intentToUpdate = false)
        {
            if (intentToUpdate || idResolver == null || typeof(T).HasLoadReferences())
            {
                return await query();
            }

            var results = await query();

            if (results == null)
            {
                return Enumerable.Empty<T>();
            }

            _cacheClient.TrySet(results, t => idResolver(t).ToString());

            return results;
        }

        public IEnumerable<T> GetRange<T, TIdType>(IEnumerable<TIdType> ids,
                                                   Func<IEnumerable<TIdType>, IEnumerable<T>> getter,
                                                   Func<T, TIdType> idResolver,
                                                   int batchSize = 500, bool intentToUpdate = false)
        {
            if (idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                foreach (var nonCachedResult in ids.ToLazyBatchesOf(batchSize)
                                                   .SelectMany(getter))
                {
                    yield return nonCachedResult;
                }

                yield break;
            }

            var missingIds = new List<TIdType>();

            foreach (var id in ids)
            {
                var cached = _cacheClient.TryGet<T>(id.ToString());

                if (cached == null)
                {
                    missingIds.Add(id);

                    if (missingIds.Count < batchSize)
                    {
                        continue;
                    }

                    var getterResults = getter(missingIds);

                    foreach (var getterResult in getterResults)
                    {
                        if (!intentToUpdate && getterResult != null)
                        {
                            _cacheClient.TrySet(getterResult, idResolver(getterResult).ToString());
                        }

                        yield return getterResult;
                    }

                    missingIds.Clear();
                }
                else
                {
                    yield return cached;
                }
            }

            if (missingIds.Count <= 0)
            {
                yield break;
            }

            var finalGetterResults = getter(missingIds);

            foreach (var getterResult in finalGetterResults)
            {
                if (!intentToUpdate && getterResult != null)
                {
                    _cacheClient.TrySet(getterResult, idResolver(getterResult).ToString());
                }

                yield return getterResult;
            }
        }

        public async Task<IEnumerable<T>> GetRangeAsync<T, TIdType>(IEnumerable<TIdType> ids,
                                                                    Func<IEnumerable<TIdType>, Task<List<T>>> getter,
                                                                    Func<T, TIdType> idResolver,
                                                                    int batchSize = int.MaxValue, bool intentToUpdate = false)
        {
            if (idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                return await getter(ids);
            }

            var missingIds = new List<TIdType>();
            var results = new List<IReadOnlyCollection<T>>();
            var cachedResults = new List<T>();

            results.Add(cachedResults);

            foreach (var id in ids)
            {
                var cached = await _cacheClient.TryGetAsync<T>(id.ToString());

                if (cached == null)
                {
                    missingIds.Add(id);

                    if (missingIds.Count < batchSize)
                    {
                        continue;
                    }

                    var getterResults = await getter(missingIds);

                    if (!intentToUpdate)
                    {
                        foreach (var getterResult in getterResults.Where(g => g != null))
                        {
                            await _cacheClient.TrySetAsync(getterResult, idResolver(getterResult).ToString());
                        }
                    }

                    results.Add(getterResults);

                    missingIds.Clear();
                }
                else
                {
                    cachedResults.Add(cached);
                }
            }

            if (missingIds.Count <= 0)
            {
                return results.SelectMany(c => c);
            }

            var finalGetterResults = await getter(missingIds);

            if (!intentToUpdate)
            {
                foreach (var getterResult in finalGetterResults.Where(g => g != null))
                {
                    await _cacheClient.TrySetAsync(getterResult, idResolver(getterResult).ToString());
                }
            }

            results.Add(finalGetterResults);

            return results.SelectMany(c => c);
        }

        public void Store<T, TIdType>(T model, Action<T> setter, Func<T, TIdType> idResolver, bool partialModel = false)
        {
            if (idResolver == null)
            {
                setter(model);
            }
            else if (partialModel || typeof(T).HasLoadReferences())
            {
                setter(model);
                _cacheClient.TryRemove<T>(idResolver(model).ToString());
            }
            else
            {
                Store(model, setter.EchoIn(), idResolver);
            }
        }

        public T Store<T, TIdType>(T model, Func<T, T> setter, Func<T, TIdType> idResolver)
        {
            var stored = setter(model);

            if (idResolver != null)
            {
                _cacheClient.TrySet(stored, idResolver(stored).ToString());
            }
            else if (typeof(T).HasLoadReferences())
            {
                _cacheClient.TryRemove<T>(idResolver(stored).ToString());
            }

            return stored;
        }

        public async Task StoreAsync<T, TIdType>(T model, Func<T, Task> setter, Func<T, TIdType> idResolver, bool partialModel = false)
        {
            if (idResolver == null)
            {
                await setter(model);
            }
            else if (partialModel || typeof(T).HasLoadReferences())
            {
                await setter(model);
                _cacheClient.TryRemove<T>(idResolver(model).ToString());
            }
            else
            {
                await StoreAsync(model, setter.EchoIn(), idResolver);
            }
        }

        public async Task<T> StoreAsync<T, TIdType>(T model, Func<T, Task<T>> setter, Func<T, TIdType> idResolver)
        {
            var stored = await setter(model);

            if (idResolver != null)
            {
                await _cacheClient.TrySetAsync(stored, idResolver(stored).ToString());
            }
            else if (typeof(T).HasLoadReferences())
            {
                _cacheClient.TryRemove<T>(idResolver(stored).ToString());
            }

            return stored;
        }

        public void StoreRange<T, TIdType>(IEnumerable<T> models, Action<IEnumerable<T>> setter, Func<T, TIdType> idResolver)
        {
            if (idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                setter(models);

                return;
            }

            // If this is a model that requires an identity on storage, we cannot cache as we don't have and cannot easily get the id assigned to each, so we
            // instead simply flush the cache of any of them.
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            //    NOTE: the above resharper recommendation would be really dumb in this case, resulting in a type check on every loop iteration...
            if (typeof(T).HasIdentityId() || typeof(T).HasLoadReferences())
            {
                setter(CacheRemove(models, idResolver));
            }
            else
            { // Otherwise we can safely store the incoming model...
                setter(CacheStore(models, idResolver));
            }
        }

        public IEnumerable<T> StoreRange<T, TIdType>(IEnumerable<T> models, Func<IEnumerable<T>, IEnumerable<T>> setter, Func<T, TIdType> idResolver)
        {
            if (idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                return setter(models);
            }

            // If this is a model that requires an identity on storage, we cannot cache as we don't have and cannot easily get the id assigned to each, so we
            // instead simply flush the cache of any of them. Otherwise we can safely store the incoming model...
            return typeof(T).HasIdentityId() || typeof(T).HasLoadReferences()
                       ? setter(CacheRemove(models, idResolver))
                       : setter(CacheStore(models, idResolver));
        }

        public async Task<IEnumerable<T>> StoreRangeAsync<T, TIdType>(IEnumerable<T> models, Func<IEnumerable<T>, Task<IEnumerable<T>>> setter, Func<T, TIdType> idResolver)
        {
            if (idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                return await setter(models);
            }

            // If this is a model that requires an identity on storage, we cannot cache as we don't have and cannot easily get the id assigned to each, so we
            // instead simply flush the cache of any of them. Otherwise we can safely store the incoming model...
            return typeof(T).HasIdentityId() || typeof(T).HasLoadReferences()
                       ? await setter(CacheRemove(models, idResolver))
                       : await setter(CacheStore(models, idResolver));
        }

        public async Task StoreRangeAsync<T, TIdType>(IEnumerable<T> models, Func<IEnumerable<T>, Task> setter, Func<T, TIdType> idResolver)
        {
            if (idResolver == null || !_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                await setter(models);

                return;
            }

            // If this is a model that requires an identity on storage, we cannot cache as we don't have and cannot easily get the id assigned to each, so we
            // instead simply flush the cache of any of them.
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            //    NOTE: the above resharper recommendation would be really dumb in this case, resulting in a type check on every loop iteration...
            if (typeof(T).HasIdentityId() || typeof(T).HasLoadReferences())
            {
                await setter(CacheRemove(models, idResolver));
            }
            else
            { // Otherwise we can safely store the incoming model...
                await setter(CacheStore(models, idResolver));
            }
        }

        public Task DeleteAsync<T, TIdType>(TIdType id, Func<TIdType, Task> exec)
        {
            _cacheClient.TryRemove<T>(id.ToString());

            return exec(id);
        }

        public async Task DeleteRangeAsync<T, TIdType>(IEnumerable<TIdType> ids, Func<IEnumerable<TIdType>, Task> exec)
        {
            if (!_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                await exec(ids);

                return;
            }

            await exec(CacheRemove<T, TIdType>(ids));
        }

        public void DeleteRange<T, TIdType>(IEnumerable<TIdType> ids, Action<IEnumerable<TIdType>> exec)
        {
            if (!_cacheClient.CanUseCache(CacheConfig.GetConfig(typeof(T).Name)))
            {
                exec(ids);

                return;
            }

            exec(CacheRemove<T, TIdType>(ids));
        }

        private IEnumerable<T> CacheStore<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
        {
            foreach (var model in models)
            {
                _cacheClient.TrySet(model, idResolver(model).ToString());

                yield return model;
            }
        }

        private IEnumerable<TIdType> CacheRemove<T, TIdType>(IEnumerable<TIdType> ids)
        {
            foreach (var id in ids)
            {
                _cacheClient.TryRemove<T>(id.ToString());

                yield return id;
            }
        }

        private IEnumerable<T> CacheRemove<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
        {
            foreach (var model in models)
            {
                _cacheClient.TryRemove<T>(idResolver(model).ToString());

                yield return model;
            }
        }
    }
}
