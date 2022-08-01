using System;
using System.Collections.Generic;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IPersistentCounterAndListService : ICounterAndListService
    {
        IStatefulCounterAndListService CreateStatefulInstance { get; }
    }

    public interface IStatefulCounterAndListService : ICounterAndListService, IDisposable { }

    public interface ICounterAndListService
    {
        long Increment(string keyName, int incrementBy = 1);
        long Decrement(string keyName, int decrementBy = 1);
        long DecrementNonNegative(string keyName, int decrementBy = 1);
        void Clear(string keyName);
        long GetCounter(string keyName);

        void AddItem(string keyName, string value);
        void RemoveItem(string keyName, string value);
        void AddUniqueItem(string keyName, string value);
        bool RemoveUniqueItem(string keyName, string value);
        void RemoveUniqueItems(string keyName, IEnumerable<string> values);

        HashSet<string> GetSetItems(string setKeyName);
        IReadOnlyList<string> PopUniqueItems(string keyName, int countRandomItemsToRemove);

        long CountOfItems(string keyName);
        long CountOfUniqueItems(string keyName);
        bool Exists(string keyName, string value);

        void EnqueueItem(string keyName, string value, int maxQueueSize = 0);
        string DequeueItem(string keyName);
    }
}
