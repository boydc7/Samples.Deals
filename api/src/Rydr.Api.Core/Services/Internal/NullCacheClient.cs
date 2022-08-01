using System;
using System.Collections.Generic;
using System.Linq;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Internal
{
    public class NullCacheClient : ICacheClient
    {
        private NullCacheClient() { }

        public static ICacheClient Instance { get; } = new NullCacheClient();

        public void Dispose() { }

        public bool Remove(string key) => true;

        public void RemoveAll(IEnumerable<string> keys) { }

        public T Get<T>(string key) => default;

        public long Increment(string key, uint amount) => 0;

        public long Decrement(string key, uint amount) => 0;

        public bool Add<T>(string key, T value) => true;

        public bool Set<T>(string key, T value) => true;

        public bool Replace<T>(string key, T value) => true;

        public bool Add<T>(string key, T value, DateTime expiresAt) => true;

        public bool Set<T>(string key, T value, DateTime expiresAt) => true;

        public bool Replace<T>(string key, T value, DateTime expiresAt) => true;

        public bool Add<T>(string key, T value, TimeSpan expiresIn) => true;

        public bool Set<T>(string key, T value, TimeSpan expiresIn) => true;

        public bool Replace<T>(string key, T value, TimeSpan expiresIn) => true;

        public void FlushAll() { }

        public void SetAll<T>(IDictionary<string, T> values) { }

        public IEnumerable<T> GetRange<T>(IEnumerable<string> keys)
            => Enumerable.Empty<T>();

        public IDictionary<string, T> GetAll<T>(IEnumerable<string> keys)
            => new Dictionary<string, T>();
    }
}
