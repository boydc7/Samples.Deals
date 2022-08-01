using System;
using System.Collections.Generic;
using System.Linq;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Internal
{
    public class LocalDistributedCacheClient : ILocalDistributedCacheClient
    {
        private readonly ICacheClient _cacheClient;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly Func<ICacheClient> _localCacheFactory;

        public LocalDistributedCacheClient(ICacheClient cacheClient, Func<ICacheClient> localCacheFactory, IDateTimeProvider dateTimeProvider)
        {
            _cacheClient = cacheClient;
            _localCacheFactory = localCacheFactory;
            _dateTimeProvider = dateTimeProvider;
        }

        public void Dispose() { }

        public bool Remove(string key)
        {
            _localCacheFactory().Remove(key);

            return _cacheClient.Remove(key);
        }

        public void RemoveAll(IEnumerable<string> keys)
        {
            _localCacheFactory().RemoveAll(keys);
            _cacheClient.RemoveAll(keys);
        }

        public T Get<T>(string key)
        {
            var localCache = _localCacheFactory();

            var entry = localCache.Get<T>(key);

            if (entry != null)
            {
                return entry;
            }

            entry = _cacheClient.Get<T>(key);

            if (entry != null)
            {
                localCache.Add(key, entry);
            }

            return entry;
        }

        public long Increment(string key, uint amount) => _cacheClient.Increment(key, amount);

        public long Decrement(string key, uint amount) => _cacheClient.Decrement(key, amount);

        public bool Add<T>(string key, T value) => AddInternal(key, value, DateTime.MaxValue);

        public bool Set<T>(string key, T value) => AddInternal(key, value, DateTime.MaxValue);

        public bool Replace<T>(string key, T value) => AddInternal(key, value, DateTime.MaxValue);

        public bool Add<T>(string key, T value, DateTime expiresAt) => AddInternal(key, value, expiresAt);

        public bool Set<T>(string key, T value, DateTime expiresAt) => AddInternal(key, value, expiresAt);

        public bool Replace<T>(string key, T value, DateTime expiresAt) => AddInternal(key, value, expiresAt);

        public bool Add<T>(string key, T value, TimeSpan expiresIn) => AddInternal(key, value, _dateTimeProvider.UtcNow.Add(expiresIn));

        public bool Set<T>(string key, T value, TimeSpan expiresIn) => AddInternal(key, value, _dateTimeProvider.UtcNow.Add(expiresIn));

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) => AddInternal(key, value, _dateTimeProvider.UtcNow.Add(expiresIn));

        private bool AddInternal<T>(string key, T value, DateTime expiresAt)
        {
            if (value == null)
            {
                return false;
            }

            var localCache = _localCacheFactory();

            _cacheClient.Set(key, value, expiresAt);

            return localCache.Set(key, value, expiresAt);
        }

        public void FlushAll()
        {
            _localCacheFactory().FlushAll();
            _cacheClient.FlushAll();
        }

        public void SetAll<T>(IDictionary<string, T> values)
        {
            _localCacheFactory().SetAll(values);
            _cacheClient.SetAll(values);
        }

        private IEnumerable<KeyValuePair<string, T>> GetRangeInternal<T>(IEnumerable<string> keys)
        {
            var allKeys = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var localCache = _localCacheFactory();

            var locals = localCache.GetAll<T>(allKeys);

            foreach (var local in locals.Where(l => l.Value != null))
            {
                yield return local;

                allKeys.Remove(local.Key);
            }

            if (allKeys.Count <= 0)
            {
                yield break;
            }

            var remotes = _cacheClient.GetAll<T>(allKeys);

            foreach (var remote in remotes.Where(r => r.Value != null && !locals.ContainsKey(r.Key)))
            {
                localCache.Set(remote.Key, remote.Value);

                yield return remote;
            }
        }

        public IEnumerable<T> GetRange<T>(IEnumerable<string> keys)
            => GetRangeInternal<T>(keys).Select(k => k.Value);

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys)
            => GetRangeInternal<T>(keys).ToDictionary(k => k.Key, k => k.Value);
    }
}
