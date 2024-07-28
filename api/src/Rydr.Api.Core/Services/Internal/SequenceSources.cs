using System.Collections.Concurrent;
using ServiceStack;

namespace Rydr.Api.Core.Services.Internal;

public class InMemorySequenceSource : ISequenceSource
{
    private readonly ConcurrentDictionary<string, long> _sequenceMap = new(StringComparer.OrdinalIgnoreCase);

    private InMemorySequenceSource() { }

    public static ISequenceSource Instance { get; } = Create();

    public static InMemorySequenceSource Create()
        => new();

    public void InitSchema() { }

    public long Increment(string key, long amount = 1)
        => _sequenceMap.AddOrUpdate(key, amount, (k, x) => x + amount);

    public void Reset(string key, long startingAt = 0)
        => _sequenceMap.AddOrUpdate(key, startingAt, (k, x) => startingAt);

    public long Peek(string key)
        => _sequenceMap.TryGetValue(key, out var existing)
               ? existing
               : 0;
}

public class BufferedSequenceSource : ISequenceSource
{
    private readonly ConcurrentDictionary<string, long> _maxLocalMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _minUsedMap = new(StringComparer.OrdinalIgnoreCase);

    private readonly ISequenceSource _distributedSource;
    private readonly int _bufferSize;
    private readonly InMemorySequenceSource _inMemorySource = InMemorySequenceSource.Create();
    private readonly object _lockObject = new();

    public BufferedSequenceSource(ISequenceSource distributedSource, int bufferSize = 100)
    {
        _distributedSource = distributedSource;
        _bufferSize = bufferSize;
    }

    public void InitSchema() { }

    public long MinSequenceUsed<T>() => MinSequenceUsed(typeof(T).FullName);

    public long MinSequenceUsed(string key)
        => _minUsedMap.TryGetValue(key, out var mu)
               ? mu
               : 0;

    public long Increment(string key, long amount = 1)
    {
        var existingMax = 0L;

        lock(_lockObject)
        {
            var currentLocalValue = _inMemorySource.Peek(key);

            if (currentLocalValue > 0 &&
                _maxLocalMap.TryGetValue(key, out existingMax) &&
                currentLocalValue + amount <= existingMax)
            {
                return LocalIncrement(key, amount);
            }

            // No buffering really will occur here when the amount > _bufferSize, but that's what we want by design anyhow, as in
            // that case we don't need to buffer anyhow, just let the remote store manage it (i.e. someone is doing something in bulk)
            var bufferSize = Math.Max(_bufferSize, amount);

            var newRemoteSequence = _distributedSource.Increment(key, bufferSize);

            // This is essentially the # before the next sequence that is usable (as we make a call to increment to get the next one to use)
            var resetLocalSequenceTo = newRemoteSequence - bufferSize;

            try
            {
                _maxLocalMap.AddOrUpdate(key, newRemoteSequence, (k, x) => newRemoteSequence);
                _inMemorySource.Reset(key, resetLocalSequenceTo);
            }
            catch
            { // Reset back to starting state across the board
                _maxLocalMap.AddOrUpdate(key, existingMax, (k, x) => existingMax);

                Try.Exec(() => _inMemorySource.Reset(key, currentLocalValue));

                throw;
            }

            return LocalIncrement(key, amount);
        }
    }

    public void Reset(string key, long startingAt = 0)
    { // NOTE: Purposely not resetting remote source here...
        lock(_lockObject)
        {
            _maxLocalMap.AddOrUpdate(key, startingAt, (k, x) => startingAt);
            _inMemorySource.Reset(key, startingAt);
        }
    }

    private long LocalIncrement(string key, long amount)
    {
        var toReturn = _inMemorySource.Increment(key, amount);

        _minUsedMap.AddOrUpdate(key, toReturn, (k, x) => toReturn < x
                                                             ? toReturn
                                                             : x);

        return toReturn;
    }
}
