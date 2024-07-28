using Rydr.Api.Core.Configuration;
using ServiceStack;

namespace Rydr.Api.Core.Services.Internal;

public static class Sequences
{
    private static ISequenceSource _instance;
    public const string GlobalSequenceKey = "_RydrGlobalSequence_";

    public static ISequenceSource Provider => _instance ??= RydrEnvironment.Container.Resolve<ISequenceSource>().ToBufferedSource(250);

    public static long Next<T>()
        => Next<T>(Provider);

    public static long Next<T>(this ISequenceSource sequenceSource)
        => Next(sequenceSource, typeof(T).FullName);

    public static long Next(string sequenceKey)
        => Next(Provider, sequenceKey);

    public static long Next(this ISequenceSource sequenceSource, string sequenceKey)
        => sequenceSource.Increment(sequenceKey);

    public static long Peek<T>(this ISequenceSource sequenceSource)
        => Peek(sequenceSource, typeof(T).FullName);

    public static long Peek(this ISequenceSource sequenceSource, string sequenceKey)
        => sequenceSource.Increment(sequenceKey, 0L);

    public static long Peek(this ISequenceSource sequenceSource)
        => sequenceSource.Increment(GlobalSequenceKey, 0L);

    public static long Next()
        => Next(Provider);

    public static long Next(this ISequenceSource sequenceSource)
        => sequenceSource.Increment(GlobalSequenceKey);

    public static BufferedSequenceSource ToBufferedSource(this ISequenceSource sequenceSource, int bufferSize = 100)
        => sequenceSource is BufferedSequenceSource bufferedSequenceSource
               ? bufferedSequenceSource
               : new BufferedSequenceSource(sequenceSource, bufferSize);

    public static IEnumerable<long> NextRange<T>(this ISequenceSource sequenceSource, int noOfSequences)
        => NextRange(sequenceSource, typeof(T).FullName, noOfSequences);

    public static IEnumerable<long> NextRange(this ISequenceSource sequenceSource, string key, int noOfSequences)
    {
        var endSequence = sequenceSource.Increment(key, noOfSequences);
        var startSequence = endSequence - noOfSequences + 1L;

        for (var x = startSequence; x <= endSequence; x++)
        {
            yield return x;
        }
    }
}
