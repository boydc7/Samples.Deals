using System.Collections.Generic;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack;
using ServiceStack.Redis;

namespace Rydr.Api.Core.Services.Internal
{
    public class RedisCounterAndListService : IPersistentCounterAndListService
    {
        private readonly IRedisClientsManager _clientManager;

        public RedisCounterAndListService(IRedisClientsManager clientManager)
        {
            _clientManager = clientManager;
        }

        public IStatefulCounterAndListService CreateStatefulInstance => new UnsafeRedisCounterAndListService(_clientManager.GetClient());

        public long Increment(string keyName, int incrementBy = 1)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.IncrementValueBy(keyName, incrementBy);
            }
        }

        public long Decrement(string keyName, int decrementBy = 1)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.DecrementValueBy(keyName, decrementBy);
            }
        }

        public long DecrementNonNegative(string keyName, int decrementBy = 1)
        {
            using(var client = _clientManager.GetClient())
            {
                var decrementValue = client.DecrementValueBy(keyName, decrementBy);

                if (decrementBy >= 0)
                {
                    return decrementValue;
                }

                client.Set(keyName, 0);

                return 0;
            }
        }

        public long GetCounter(string keyName)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.GetValue(keyName).ToLong();
            }
        }

        public void Clear(string keyName)
        {
            using(var client = _clientManager.GetClient())
            {
                client.RemoveEntry(keyName);
            }
        }

        public void AddItem(string keyName, string value)
        {
            using(var client = _clientManager.GetClient())
            {
                client.AddItemToList(keyName, value);
            }
        }

        public void RemoveItem(string keyName, string value)
        {
            using(var client = _clientManager.GetClient())
            {
                client.RemoveItemFromList(keyName, value);
            }
        }

        public void EnqueueItem(string keyName, string value, int maxQueueSize = 0)
        {
            using(var client = _clientManager.GetClient())
            {
                client.EnqueueItemOnList(keyName, value);

                if (maxQueueSize > 0)
                {
                    client.TrimList(keyName, 0, (maxQueueSize - 1));
                }
            }
        }

        public string DequeueItem(string keyName)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.DequeueItemFromList(keyName);
            }
        }

        public void AddUniqueItem(string keyName, string value)
        {
            using(var client = _clientManager.GetClient())
            {
                client.AddItemToSet(keyName, value);
            }
        }

        public bool RemoveUniqueItem(string keyName, string value)
        {
            using(var client = _clientManager.GetClient())
            {
                var exists = client.SetContainsItem(keyName, value);

                client.RemoveItemFromSet(keyName, value);

                return exists;
            }
        }

        public void RemoveUniqueItems(string keyName, IEnumerable<string> values)
        {
            using(var client = _clientManager.GetClient())
            {
                values.Each(v => client.RemoveItemFromSet(keyName, v));
            }
        }

        public IReadOnlyList<string> PopUniqueItems(string keyName, int countRandomItemsToRemove)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.PopItemsFromSet(keyName, countRandomItemsToRemove);
            }
        }

        public long CountOfItems(string keyName)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.GetListCount(keyName);
            }
        }

        public long CountOfUniqueItems(string keyName)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.GetSetCount(keyName);
            }
        }

        public bool Exists(string keyName, string value)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.SetContainsItem(keyName, value);
            }
        }

        public HashSet<string> GetSetItems(string setKeyName)
        {
            using(var client = _clientManager.GetClient())
            {
                return client.GetAllItemsFromSet(setKeyName);
            }
        }
    }

    public class UnsafeRedisCounterAndListService : IStatefulCounterAndListService
    {
        private readonly IRedisClient _client;

        public UnsafeRedisCounterAndListService(IRedisClient client)
        {
            _client = client;
        }

        public long DecrementNonNegative(string keyName, int decrementBy = 1)
        {
            var decrementValue = _client.DecrementValueBy(keyName, decrementBy);

            if (decrementBy >= 0)
            {
                return decrementValue;
            }

            _client.Set(keyName, 0);

            return 0;
        }

        public long Increment(string keyName, int incrementBy = 1) => _client.IncrementValueBy(keyName, incrementBy);
        public long Decrement(string keyName, int decrementBy = 1) => _client.DecrementValueBy(keyName, decrementBy);
        public void Clear(string keyName) => _client.RemoveEntry(keyName);
        public long GetCounter(string keyName) => _client.GetValue(keyName).ToLong();
        public void AddItem(string keyName, string value) => _client.AddItemToList(keyName, value);
        public void RemoveItem(string keyName, string value) => _client.RemoveItemFromList(keyName, value);
        public void AddUniqueItem(string keyName, string value) => _client.AddItemToSet(keyName, value);

        public bool RemoveUniqueItem(string keyName, string value)
        {
            var exists = _client.SetContainsItem(keyName, value);

            _client.RemoveItemFromSet(keyName, value);

            return exists;
        }

        public void RemoveUniqueItems(string keyName, IEnumerable<string> values)
            => values.Each(v => _client.RemoveItemFromSet(keyName, v));

        public IReadOnlyList<string> PopUniqueItems(string keyName, int countRandomItemsToRemove)
            => _client.PopItemsFromSet(keyName, countRandomItemsToRemove);

        public HashSet<string> GetSetItems(string setKeyName) => _client.GetAllItemsFromSet(setKeyName);
        public long CountOfItems(string keyName) => _client.GetListCount(keyName);
        public long CountOfUniqueItems(string keyName) => _client.GetSetCount(keyName);
        public bool Exists(string keyName, string value) => _client.SetContainsItem(keyName, value);

        public void EnqueueItem(string keyName, string value, int maxQueueSize = 0)
        {
            _client.EnqueueItemOnList(keyName, value);

            if (maxQueueSize > 0)
            {
                _client.TrimList(keyName, 0, (maxQueueSize - 1));
            }
        }

        public string DequeueItem(string keyName) => _client.DequeueItemFromList(keyName);
        public void Dispose() => _client.TryDispose();
    }
}
