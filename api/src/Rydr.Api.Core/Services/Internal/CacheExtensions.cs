using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.Logging;
using ServiceStack.Model;
using ServiceStack.Pcl;

namespace Rydr.Api.Core.Services.Internal
{
    public static class CacheExtensions
    {
        private const string _cachePrefixApi = "api";
        private const string _cachePrefixLock = "lock";

        private static readonly ILog _log = LogManager.GetLogger("CacheExtensions");
        private static readonly bool _cacheDisabled = RydrEnvironment.GetAppSetting("Caching.DisableAll", false);
        private static readonly bool _cacheSetAsync = RydrEnvironment.GetAppSetting("Caching.SetAsync", false);

        public static ICacheClient InMemoryCacheInstance { get; } = new MemoryCacheClient();
        public static IDistributedLockService InMemoryLockService { get; } = new CacheDistributedLockService(InMemoryCacheInstance);

        private static string GetCacheKey(string prefix, string category, string id)
        {
            var key = string.Concat("urn:", prefix, ".", category, "|", id).ToLowerInvariant();

            return key;
        }

        public static string GetLockCacheKey(string id, string category) => GetCacheKey(_cachePrefixLock, category, id);

        public static string ToCacheKey<T>(this T model)
            where T : IHasId<long>
            => GetCacheKey(_cachePrefixApi, typeof(T).Name, model.Id.ToString());

        public static string ToCacheKey<T>(this string id) => GetCacheKey(_cachePrefixApi, typeof(T).Name, id);
        public static string ToCacheKey<T>(this string id, string namedCategory) => GetCacheKey(_cachePrefixApi, string.Concat(namedCategory, "|", typeof(T).Name), id);

        public static bool CanUseCache(this CacheConfig config)
            => !_cacheDisabled && config.HasValidExpiry();

        public static T TryGet<T>(this ICacheClient cache, string id, Func<T> getter = null, CacheConfig config = null)
        {
            var typeName = typeof(T).Name;

            // If the getter is null and no config is specified, use a dummy valid config so the GET attempt will still try to get an existing value...
            return TryGet(cache, id, getter,
                          config ?? (getter == null
                                         ? CacheConfig.FrameworkConfig
                                         : CacheConfig.GetConfig(typeName)),
                          typeName);
        }

        public static bool TryGetUrlForCacheKeyFromDto<T>(this T requestDto, out string getUrl)
            where T : IRequestBase
        {
            getUrl = null;

            if (requestDto == null)
            {
                return false;
            }

            if (!Try.Get(() => requestDto.ToGetUrl(), out getUrl) || !getUrl.HasValue())
            {
                return false;
            }

            var getUri = new Uri(string.Concat("https://api.getrydr.com", getUrl));

            var queryStringForCache = HttpUtility.ParseQueryString(getUri.Query);

            queryStringForCache.Remove("Unset");
            queryStringForCache.Remove("ForceRefresh");
            queryStringForCache.Remove("IsSystemRequest");
            queryStringForCache.Remove("ReceivedAt");
            queryStringForCache.Remove("SessionId");
            queryStringForCache.Remove("RoleId");

            getUrl = (queryStringForCache.Count > 0
                          ? string.Concat(getUri.AbsolutePath, "?", queryStringForCache)
                          : getUri.AbsolutePath).ToNullIfEmpty();

            return getUrl != null;
        }

        public static async Task<T> TryGetAsync<T>(this ICacheClient cache, string id, Func<ValueTask<T>> getter, CacheConfig config = null)
        {
            var typeName = typeof(T).Name;

            // If the getter is null and no config is specified, use a dummy valid config so the GET attempt will still try to get an existing value...
            var result = await TryGetAsync(cache, id, getter,
                                           config ?? (getter == null
                                                          ? CacheConfig.FrameworkConfig
                                                          : CacheConfig.GetConfig(typeName)),
                                           typeName);

            return result;
        }

        public static Task<T> TryGetTaskAsync<T>(this ICacheClient cache, string id, Func<Task<T>> getter = null, CacheConfig config = null)
            => TryGetAsync(cache, id, getter, config);

        public static async Task<T> TryGetAsync<T>(this ICacheClient cache, string id, Func<Task<T>> getter = null, CacheConfig config = null)
        {
            var typeName = typeof(T).Name;

            // If the getter is null and no config is specified, use a dummy valid config so the GET attempt will still try to get an existing value...
            var result = await TryGetAsync(cache, id, getter,
                                           config ?? (getter == null
                                                          ? CacheConfig.FrameworkConfig
                                                          : CacheConfig.GetConfig(typeName)),
                                           typeName);

            return result;
        }

        private static T TryGet<T>(this ICacheClient cache, string id, Func<T> getter, CacheConfig config, string typeName)
        {
            var attempt = GetCachedResource(id, getter, cache, config, typeName);

            if (attempt.ShouldSetInCache())
            {
                var ced = new CacheExecData<T>(cache, attempt);
                TryExecCache(ced);
            }

            return attempt.Resource;
        }

        private static async Task<T> TryGetAsync<T>(this ICacheClient cache, string id, Func<ValueTask<T>> getter, CacheConfig config, string typeName)
        {
            var attempt = await GetCachedResourceAsync(id, getter, cache, config, typeName);

            if (attempt.ShouldSetInCache())
            {
                var ced = new CacheExecData<T>(cache, attempt);

                TryExecCache(ced);
            }

            return attempt.Resource;
        }

        private static async Task<T> TryGetAsync<T>(this ICacheClient cache, string id, Func<Task<T>> getter, CacheConfig config, string typeName)
        {
            var attempt = await GetCachedResourceAsync(id, getter, cache, config, typeName);

            if (attempt.ShouldSetInCache())
            {
                var ced = new CacheExecData<T>(cache, attempt);

                TryExecCache(ced);
            }

            return attempt.Resource;
        }

        public static void TrySet<T, TIdType>(this ICacheClient cache, T entity, CacheConfig cacheConfig = null)
            where T : IHasId<TIdType>
            => TrySet(cache, entity, entity.Id.ToString(), cacheConfig);

        public static Task TrySetAsync<T, TIdType>(this ICacheClient cache, T entity)
            where T : IHasId<TIdType>
            => TrySetAsync(cache, entity, entity.Id.ToString());

        public static Task TrySetAsync<T>(this ICacheClient cache, T entity, string id, CacheConfig config = null)
        {
            var typeName = typeof(T).Name;
            var cacheConfig = config ?? CacheConfig.GetConfig(typeName);

            var cacheKey = GetCacheKey(_cachePrefixApi, typeName, id);

            return TrySetWithKeyAsync(cache, cacheKey, entity, cacheConfig);
        }

        public static void TrySet<T>(this ICacheClient cache, IEnumerable<T> entities, Func<T, string> idResolver)
        {
            var typeName = typeof(T).Name;
            var cacheConfig = CacheConfig.GetConfig(typeName);

            if (!CanUseCache(cache, cacheConfig))
            {
                return;
            }

            foreach (var entity in entities)
            {
                var cacheKey = GetCacheKey(_cachePrefixApi, typeName, idResolver(entity));
                TrySetWithKey(cache, cacheKey, entity, cacheConfig);
            }
        }

        public static void TrySet<T>(this ICacheClient cache, T entity, string id, CacheConfig config = null)
        {
            var typeName = typeof(T).Name;
            var cacheConfig = config ?? CacheConfig.GetConfig(typeName);

            var cacheKey = GetCacheKey(_cachePrefixApi, typeName, id);

            TrySetWithKey(cache, cacheKey, entity, cacheConfig);
        }

        public static Task TrySetWithKeyAsync<T>(this ICacheClient cache, string key, T entity, CacheConfig config)
        {
            var expireIn = CacheExpiry.GetCacheExpireTime(config?.DurationSeconds ?? 0, config?.MinutesPastMidnight ?? 0);

            if (!expireIn.HasValue)
            {
                return Task.CompletedTask;
            }

            return TrySetWithKeyAsync(key, entity, cache, expireIn.Value);
        }

        public static void TrySetWithKey<T>(this ICacheClient cache, string key, T entity, CacheConfig config)
        {
            var expireIn = CacheExpiry.GetCacheExpireTime(config?.DurationSeconds ?? 0, config?.MinutesPastMidnight ?? 0);

            if (!expireIn.HasValue)
            {
                return;
            }

            TrySetWithKey(key, entity, cache, expireIn.Value);
        }

        public static Task TrySetWithKeyAsync<T>(string key, T entity, ICacheClient cache, TimeSpan expireIn)
        {
            TrySetWithKey(key, entity, cache, expireIn);

            return Task.CompletedTask;
        }

        public static void TrySetWithKey<T>(string key, T entity, ICacheClient cache, TimeSpan expireIn)
        {
            var ced = new CacheExecData<T>(cache, key, entity, expireIn);

            TryExecCache(ced);
        }

        public static void TryRemove<T>(this ICacheClient cache, string id)
        {
            var typeName = typeof(T).Name;
            TryRemove(cache, id, typeName);
        }

        public static void TryRemove(this ICacheClient cache, string id, string typeName)
        {
            if (cache == null)
            {
                return;
            }

            var cacheKey = GetCacheKey(_cachePrefixApi, typeName, id);

            if (!cacheKey.HasValue())
            {
                return;
            }

            Try.Exec(() => cache.Remove(cacheKey));
        }

        public static bool CanUseCache(this ICacheClient cache, CacheConfig config)
        {
            if (_cacheDisabled || cache == null || config == null)
            {
                return false;
            }

            return config.CanUseCache();
        }

        private static void TryExecCache<T>(CacheExecData<T> ced)
        {
            if (_cacheDisabled || !ced.IsValid())
            {
                return;
            }

            if (_cacheSetAsync)
            {
                TryExecCacheAsync(ced, SetAsync);
            }
            else
            {
                try
                {
                    Set(ced);
                }
                catch(Exception ex)
                {
                    _log.Warn("Could not write to cache", ex);
                }
            }
        }

        private static void Set<T>(CacheExecData<T> ced)
            => ced.CacheClient.CacheSet(ced.CacheKey, ced.Resource, ced.ExpiresIn);

        private static Task SetAsync<T>(CacheExecData<T> ced)
        {
            ced.CacheClient.CacheSet(ced.CacheKey, ced.Resource, ced.ExpiresIn);

            return Task.CompletedTask;
        }

        private static void TryExecCacheAsync<T>(CacheExecData<T> ced, Func<CacheExecData<T>, Task> onExec)
            => LocalAsyncTaskExecuter.DefaultTaskExecuter.ExecAsync(ced, onExec,
                                                                    true,
                                                                    (c, x) => _log.Warn("Could not write to cache", x),
                                                                    2);

        private static CachedGetAttempt<T> GetCachedResource<T>(string id, Func<T> getter, ICacheClient cache,
                                                                CacheConfig config, string resourceTypeName)
        {
            var cachedItemTypeName = resourceTypeName.HasValue()
                                         ? resourceTypeName
                                         : typeof(T).Name;

            var cacheKey = id.HasValue()
                               ? GetCacheKey(_cachePrefixApi, cachedItemTypeName, id)
                               : null;

            return GetCachedResource(cacheKey, getter, cache, config);
        }

        private static CachedGetAttempt<T> GetCachedResource<T>(string cacheKey, Func<T> getter, ICacheClient cache, CacheConfig config)
        {
            var attempt = new CachedGetAttempt<T>();

            if (!CanUseCache(cache, config) || !cacheKey.HasValue())
            {
                attempt.Resource = getter == null
                                       ? default
                                       : getter();

                return attempt;
            }

            attempt.Cacheable = true;

            attempt.ExpireIn = CacheExpiry.GetCacheExpireTime(config.DurationSeconds, config.MinutesPastMidnight);

            attempt.CacheKey = cacheKey;
            attempt.Resource = cache.Get<T>(attempt.CacheKey);

            if ((attempt.Resource != null && !attempt.Resource.IsDefault()) || getter == null)
            {
                attempt.FromCache = true;

                return attempt;
            }

            attempt.Resource = getter();
            attempt.Settable = true;

            return attempt;
        }

        private static async Task<CachedGetAttempt<T>> GetCachedResourceAsync<T>(string id, Func<ValueTask<T>> getter, ICacheClient cache,
                                                                                 CacheConfig config, string resourceTypeName)
        {
            var attempt = new CachedGetAttempt<T>();

            if (!CanUseCache(cache, config) || !id.HasValue())
            {
                attempt.Resource = getter == null
                                       ? default
                                       : await getter();

                return attempt;
            }

            attempt.Cacheable = true;

            attempt.ExpireIn = CacheExpiry.GetCacheExpireTime(config.DurationSeconds, config.MinutesPastMidnight);

            var cachedItemTypeName = resourceTypeName.HasValue()
                                         ? resourceTypeName
                                         : typeof(T).Name;

            attempt.CacheKey = GetCacheKey(_cachePrefixApi, cachedItemTypeName, id);
            attempt.Resource = cache.Get<T>(attempt.CacheKey);

            if ((attempt.Resource != null && !attempt.Resource.IsDefault()) || getter == null)
            {
                attempt.FromCache = true;

                return attempt;
            }

            attempt.Resource = await getter();
            attempt.Settable = true;

            return attempt;
        }

        private static async Task<CachedGetAttempt<T>> GetCachedResourceAsync<T>(string id, Func<Task<T>> getter, ICacheClient cache,
                                                                                 CacheConfig config, string resourceTypeName)
        {
            var attempt = new CachedGetAttempt<T>();

            if (!CanUseCache(cache, config) || !id.HasValue())
            {
                attempt.Resource = getter == null
                                       ? default
                                       : await getter();

                return attempt;
            }

            attempt.Cacheable = true;

            attempt.ExpireIn = CacheExpiry.GetCacheExpireTime(config.DurationSeconds, config.MinutesPastMidnight);

            var cachedItemTypeName = resourceTypeName.HasValue()
                                         ? resourceTypeName
                                         : typeof(T).Name;

            attempt.CacheKey = GetCacheKey(_cachePrefixApi, cachedItemTypeName, id);
            attempt.Resource = cache.Get<T>(attempt.CacheKey);

            if ((attempt.Resource != null && !attempt.Resource.IsDefault()) || getter == null)
            {
                attempt.FromCache = true;

                return attempt;
            }

            attempt.Resource = await getter();
            attempt.Settable = true;

            return attempt;
        }

        private class CachedGetAttempt<T>
        {
            public T Resource { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public bool Cacheable { get; set; }
            public bool Settable { get; set; }
            public TimeSpan? ExpireIn { get; set; }
            public string CacheKey { get; set; }
            public bool FromCache { get; set; }

            public bool ShouldSetInCache() => Settable && Resource != null && CacheKey.HasValue() && ExpireIn.HasValue && !FromCache;
        }

        private class CacheExecData<T>
        {
            public CacheExecData(ICacheClient cache, string key, T resource, TimeSpan? expiresIn)
            {
                CacheClient = cache;
                CacheKey = key;
                Resource = resource;
                ExpiresIn = expiresIn;
            }

            public CacheExecData(ICacheClient cache, CachedGetAttempt<T> attempt)
            {
                CacheClient = cache;
                CacheKey = attempt?.CacheKey;

                Resource = attempt == null
                               ? default
                               : attempt.Resource;

                ExpiresIn = attempt?.ExpireIn;
            }

            public bool IsValid() => CacheClient != null && CacheKey.HasValue() && Resource != null && ExpiresIn.HasValue && ExpiresIn.Value.TotalSeconds > 0;

            public ICacheClient CacheClient { get; }
            public string CacheKey { get; }
            public T Resource { get; }
            public TimeSpan? ExpiresIn { get; }
        }
    }
}
