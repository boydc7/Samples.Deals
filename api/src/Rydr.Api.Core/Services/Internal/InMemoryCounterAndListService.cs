using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Internal
{
    public class InMemoryCounterAndListService : InMemoryCounterAndListService<string>, IStatefulCounterAndListService, IPersistentCounterAndListService
    {
        private InMemoryCounterAndListService() { }

        public IStatefulCounterAndListService CreateStatefulInstance => this;

        public static InMemoryCounterAndListService Instance { get; } = new InMemoryCounterAndListService();

        public void Dispose() { }
    }

    public class InMemoryCounterAndListService<T>
    {
        private readonly ConcurrentDictionary<string, long> _counters = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, List<T>> _lists = new ConcurrentDictionary<string, List<T>>();
        private readonly ConcurrentDictionary<string, HashSet<T>> _sets = new ConcurrentDictionary<string, HashSet<T>>();
        private readonly object _lockObject = new object();

        public long Increment(string keyName, int incrementBy = 1)
        {
            return !keyName.HasValue()
                       ? 0
                       : _counters.AddOrUpdate(keyName, 1, (k, v) => v + 1);
        }

        public long DecrementNonNegative(string keyName, int decrementBy = 1)
        {
            return !keyName.HasValue()
                       ? 0
                       : _counters.AddOrUpdate(keyName, 0, (k, v) => v <= 0
                                                                         ? 0
                                                                         : v - 1);
        }

        public long Decrement(string keyName, int decrementBy = 1)
        {
            return !keyName.HasValue()
                       ? 0
                       : _counters.AddOrUpdate(keyName, 0, (k, v) => v - 1);
        }

        public void SetCounter(string keyName, long value)
        {
            if (!keyName.HasValue())
            {
                return;
            }

            _counters.AddOrUpdate(keyName, value, (k, v) => value);
        }

        public long GetCounter(string keyName)
        {
            if (!keyName.HasValue())
            {
                return 0;
            }

            long value = 0;
            _counters.TryGetValue(keyName, out value);

            return value;
        }

        public void Clear(string keyName)
        {
            if (!keyName.HasValue())
            {
                return;
            }

            _counters.TryRemove(keyName, out _);
            _lists.TryRemove(keyName, out _);
            _sets.TryRemove(keyName, out _);
        }

        public void AddItem(string keyName, T value)
        {
            if (!keyName.HasValue())
            {
                return;
            }

            _lists.AddOrUpdate(keyName, new List<T>
                                        {
                                            value
                                        },
                               (k, v) =>
                               {
                                   v.Add(value);

                                   return v;
                               });
        }

        public void RemoveItem(string keyName, T value)
        {
            if (!keyName.HasValue())
            {
                return;
            }

            if (_lists.ContainsKey(keyName))
            {
                _lists[keyName].RemoveAll(t => t.Equals(value));
            }
        }

        public void EnqueueItem(string keyName, T value, int maxQueueSize = 0)
        {
            AddItem(keyName, value);

            if (maxQueueSize > 0 && _lists.ContainsKey(keyName) && _lists[keyName].Count > maxQueueSize)
            {
                _lists[keyName].RemoveRange((maxQueueSize - 1), _lists[keyName].Count);
            }
        }

        public T DequeueItem(string keyName)
        {
            if (_lists.TryRemove(keyName, out var x) && x != null)
            {
                lock(_lockObject)
                {
                    if (x == null)
                    {
                        return default;
                    }

                    var dequeue = x.FirstOrDefault();

                    if (dequeue == null)
                    {
                        return default;
                    }

                    x.Remove(dequeue);

                    return dequeue;
                }
            }

            return default;
        }

        public void SetItems(string keyName, List<T> value)
        {
            if (!keyName.HasValue())
            {
                return;
            }

            value = value ?? new List<T>();
            _lists.AddOrUpdate(keyName, value, (k, v) => value);
        }

        public void ResetItemsFromSource(IEnumerable<T> sourceItems, Func<T, string> hashKeyGetter, Func<T, string> secondHashKeyGetter = null)
        {
            var newLocalItems = new Dictionary<string, List<T>>();

            void addItemToHash(string key, T item)
            {
                if (newLocalItems.ContainsKey(key))
                {
                    newLocalItems[key].Add(item);
                }
                else
                {
                    newLocalItems[key] = new List<T>
                                         {
                                             item
                                         };
                }
            }

            foreach (var sourceItem in sourceItems)
            {
                var hashKey = hashKeyGetter(sourceItem);

                addItemToHash(hashKey, sourceItem);

                if (secondHashKeyGetter != null)
                {
                    var secondHashKey = secondHashKeyGetter(sourceItem);
                    addItemToHash(secondHashKey, sourceItem);
                }
            }

            foreach (var newItem in newLocalItems)
            {
                SetItems(newItem.Key, newItem.Value);
            }
        }

        public void AddUniqueItem(string keyName, T value)
        {
            if (!keyName.HasValue())
            {
                return;
            }

            _sets.AddOrUpdate(keyName, new HashSet<T>
                                       {
                                           value
                                       },
                              (k, v) =>
                              {
                                  v.Add(value);

                                  return v;
                              });
        }

        public bool RemoveUniqueItem(string keyName, T value)
        {
            if (!keyName.HasValue() || !_sets.ContainsKey(keyName))
            {
                return false;
            }

            return _sets[keyName].Remove(value);
        }

        public void RemoveUniqueItems(string keyName, IEnumerable<T> values)
        {
            if (!keyName.HasValue() || !_sets.ContainsKey(keyName))
            {
                return;
            }

            values.Each(v => _sets[keyName].Remove(v));
        }

        public IReadOnlyList<T> PopUniqueItems(string keyName, int countRandomItemsToRemove)
        {
            if (!keyName.HasValue() || !_sets.ContainsKey(keyName))
            {
                return null;
            }

            if (_sets[keyName].Count <= countRandomItemsToRemove)
            {
                _sets.TryRemove(keyName, out var fullSet);

                return fullSet.AsList();
            }

            var returnList = new List<T>(countRandomItemsToRemove);

            for (var i = 0; i <= countRandomItemsToRemove; i++)
            {
                var itemToRemove = _sets[keyName].FirstOrDefault();

                if (itemToRemove == null)
                {
                    _sets.TryRemove(keyName, out _);

                    break;
                }

                _sets[keyName].Remove(itemToRemove);

                returnList.Add(itemToRemove);
            }

            return returnList.AsReadOnly();
        }

        public long CountOfItems(string keyName)
        {
            if (!keyName.HasValue())
            {
                return 0;
            }

            return _lists.TryGetValue(keyName, out var list)
                       ? list.Count
                       : 0;
        }

        public long CountOfUniqueItems(string keyName)
        {
            if (!keyName.HasValue())
            {
                return 0;
            }

            return _sets.TryGetValue(keyName, out var set)
                       ? set.Count
                       : 0;
        }

        public bool Exists(string keyName, T value)
        {
            if (!keyName.HasValue())
            {
                return false;
            }

            return _sets.TryGetValue(keyName, out var set) && set.Contains(value);
        }

        public HashSet<T> GetSetItems(string setKeyName)
        {
            _sets.TryGetValue(setKeyName, out var set);

            return set;
        }
    }
}
