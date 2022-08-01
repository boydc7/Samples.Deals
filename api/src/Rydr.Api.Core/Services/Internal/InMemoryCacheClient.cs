using System;
using System.Collections.Generic;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Internal
{
    public class InMemoryCacheClient : ILocalRequestCacheClient
    {
        private readonly MemoryCacheClient _memoryCacheClient = new MemoryCacheClient();

        public static ICacheClient Default { get; } = new InMemoryCacheClient();

        public void Dispose() { }

        public bool Remove(string key) => _memoryCacheClient.Remove(key);

        public void RemoveAll(IEnumerable<string> keys) => _memoryCacheClient.RemoveAll(keys);

        public T Get<T>(string key) => _memoryCacheClient.Get<T>(key);

        public long Increment(string key, uint amount) => _memoryCacheClient.Increment(key, amount);

        public long Decrement(string key, uint amount) => _memoryCacheClient.Decrement(key, amount);

        public bool Add<T>(string key, T value) => _memoryCacheClient.Add(key, value);

        public bool Set<T>(string key, T value) => _memoryCacheClient.Set(key, value);

        public bool Replace<T>(string key, T value) => _memoryCacheClient.Replace(key, value);

        public bool Add<T>(string key, T value, DateTime expiresAt) => _memoryCacheClient.Add(key, value, expiresAt);

        public bool Set<T>(string key, T value, DateTime expiresAt) => _memoryCacheClient.Set(key, value, expiresAt);

        public bool Replace<T>(string key, T value, DateTime expiresAt) => _memoryCacheClient.Replace(key, value, expiresAt);

        public bool Add<T>(string key, T value, TimeSpan expiresIn) => _memoryCacheClient.Add(key, value, expiresIn);

        public bool Set<T>(string key, T value, TimeSpan expiresIn) => _memoryCacheClient.Set(key, value, expiresIn);

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) => _memoryCacheClient.Replace(key, value, expiresIn);

        public void FlushAll() => _memoryCacheClient.FlushAll();

        public void SetAll<T>(IDictionary<string, T> values) => _memoryCacheClient.SetAll(values);

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys) => _memoryCacheClient.GetAll<T>(keys);
    }
}
