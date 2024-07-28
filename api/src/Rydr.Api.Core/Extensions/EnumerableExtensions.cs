using System.Collections;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Extensions;

public static class EnumerableExtensions
{
    public static TValue GetValueOrDefaultSafe<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
        => source != null && source.ContainsKey(key)
               ? source[key]
               : default;

    public static async Task<HashSet<TOut>> SelectManyDistinctAsync<TIn, TOut>(this IAsyncEnumerable<TIn> source,
                                                                               Func<TIn, IEnumerable<TOut>> selector,
                                                                               IEqualityComparer<TOut> comparer = null)
    {
        var set = new HashSet<TOut>(comparer);

        await foreach (var item in source)
        {
            foreach (var outItem in selector(item))
            {
                set.Add(outItem);
            }
        }

        return set;
    }

    public static async Task Each<T>(this IAsyncEnumerable<T> source, Action<T> block)
    {
        await foreach (var item in source)
        {
            block(item);
        }
    }

    public static async Task EachAsync<T>(this IAsyncEnumerable<T> source, Func<T, Task> block)
    {
        await foreach (var item in source)
        {
            await block(item);
        }
    }

    public static async Task<IReadOnlyList<T>> ToListReadOnly<T>(this IAsyncEnumerable<T> source, int hintSize = 0)
    {
        var result = await ToList(source, hintSize);

        return result.AsReadOnly();
    }

    public static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> source, int hintSize = 0)
    {
        if (source == null)
        {
            return null;
        }

        var results = new List<T>(hintSize);

        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    public static async Task<HashSet<T>> ToHashSet<T>(this IAsyncEnumerable<T> source, IEqualityComparer<T> comparer = null)
    {
        if (source == null)
        {
            return null;
        }

        var results = new HashSet<T>(comparer);

        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    public static IReadOnlyList<T> AsListReadOnly<T>(this IEnumerable<T> source)
        => source?.AsList()?.AsReadOnly();

    public static T ToNullIfEmpty<T>(this T source)
        where T : class, ICollection
        => source == null || source.Count <= 0
               ? null
               : source;

    public static Task<Dictionary<TKey, T>> ToDictionarySafe<T, TKey>(this IAsyncEnumerable<T> source, Func<T, TKey> keySelector,
                                                                      IEqualityComparer<TKey> comparer = null)
        => ToDictionarySafe(source, keySelector, v => v, comparer);

    public static async Task<Dictionary<TKey, TVal>> ToDictionarySafe<T, TKey, TVal>(this IAsyncEnumerable<T> source, Func<T, TKey> keySelector,
                                                                                     Func<T, TVal> valSelector, IEqualityComparer<TKey> comparer = null)
    {
        var map = new Dictionary<TKey, TVal>(comparer);

        if (source == null)
        {
            return map;
        }

        await foreach (var sourceValue in source)
        {
            map[keySelector(sourceValue)] = valSelector(sourceValue);
        }

        return map;
    }

    public static Task<Dictionary<TKey, List<T>>> ToDictionaryManySafe<T, TKey>(this IAsyncEnumerable<T> source, Func<T, TKey> keySelector,
                                                                                IEqualityComparer<TKey> comparer = null)
        => ToDictionaryManySafe(source, keySelector, v => v, comparer);

    public static async Task<Dictionary<TKey, List<TVal>>> ToDictionaryManySafe<T, TKey, TVal>(this IAsyncEnumerable<T> source, Func<T, TKey> keySelector,
                                                                                               Func<T, TVal> valSelector, IEqualityComparer<TKey> comparer = null)
    {
        var map = new Dictionary<TKey, List<TVal>>(comparer);

        if (source == null)
        {
            return map;
        }

        await foreach (var sourceValue in source)
        {
            var key = keySelector(sourceValue);

            if (map.ContainsKey(key))
            {
                map[key].Add(valSelector(sourceValue));
            }
            else
            {
                map[key] = new List<TVal>
                           {
                               valSelector(sourceValue)
                           };
            }
        }

        return map;
    }

    public static Dictionary<TKey, T> ToDictionarySafe<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
        => ToDictionarySafe(source, keySelector, v => v);

    public static Dictionary<TKey, TVal> ToDictionarySafe<T, TKey, TVal>(this IEnumerable<T> source, Func<T, TKey> keySelector,
                                                                         Func<T, TVal> valSelector, IEqualityComparer<TKey> comparer = null)
    {
        var map = new Dictionary<TKey, TVal>(comparer);

        if (source != null)
        {
            foreach (var sourceValue in source)
            {
                map[keySelector(sourceValue)] = valSelector(sourceValue);
            }
        }

        return map;
    }

    public static List<T> CreateOrAdd<T>(this List<T> source, T value)
    {
        if (source == null)
        {
            return new List<T>
                   {
                       value
                   };
        }

        source.Add(value);

        return source;
    }

    public static IEnumerable<object> AsObjectEnumerable(this IEnumerable basicEnumerable)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var obj in basicEnumerable)
        {
            yield return obj;
        }
    }

    public static IEnumerable<TAs> SafeCast<TAs>(this IEnumerable<object> source)
        where TAs : class
        => SafeCast<TAs, object>(source ?? Enumerable.Empty<object>());

    public static IEnumerable<TAs> SafeCast<TAs, TFrom>(this IEnumerable<TFrom> source)
        where TAs : class
    {
        if (source == null)
        {
            return Enumerable.Empty<TAs>();
        }

        return source.Select(s => s == null
                                      ? null
                                      : s as TAs);
    }

    public static IEnumerable<string> ToEdgeIdSuffixes(this IEnumerable<string> edgeIds)
        => edgeIds?.Select(e => e.ToEdgeIdSuffix()) ?? Enumerable.Empty<string>();

    public static IEnumerable<T> SafeUnion<T>(this IEnumerable<T> source, IEnumerable<T> second, IEqualityComparer<T> comparer = null)
        => (source ?? Enumerable.Empty<T>()).Union(second ?? Enumerable.Empty<T>(), comparer);

    public static (T Min, T Max) MinMax<T>(this IEnumerable<T> source, T defaultMin = default, T defaultMax = default)
    {
        if (source == null)
        {
            return (defaultMin, defaultMax);
        }

        var sourceCount = 0;

        var min = defaultMin;
        var max = defaultMax;

        var comparer = Comparer<T>.Default;

        foreach (var sourceValue in source)
        {
            sourceCount++;

            if (sourceCount == 1)
            {
                min = sourceValue;
                max = sourceValue;

                continue;
            }

            if (comparer.Compare(sourceValue, min) < 0)
            {
                min = sourceValue;
            }

            if (comparer.Compare(sourceValue, max) > 0)
            {
                max = sourceValue;
            }
        }

        return (min, sourceCount > 1
                         ? max
                         : defaultMax);
    }

    public static HashSet<T> AsHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
    {
        switch (source)
        {
            case null:

                return null;
            case HashSet<T> h:

                return h;
            default:

                return comparer == null
                           ? new HashSet<T>(source)
                           : new HashSet<T>(source, comparer);
        }
    }

    public static List<T> NullIfEmpty<T>(this List<T> source)
        => source == null || source.Count <= 0
               ? null
               : source;

    public static IReadOnlyList<T> NullIfEmpty<T>(this IReadOnlyList<T> source)
        => source == null || source.Count <= 0
               ? null
               : source;

    public static HashSet<T> NullIfEmpty<T>(this HashSet<T> source)
        => source == null || source.Count <= 0
               ? null
               : source;

    public static bool SafeContains<T>(this HashSet<T> source, T item)
        => !source.IsNullOrEmpty() && source.Contains(item);

    public static bool IsNullOrEmptyRydr<T>(this ICollection<T> source)
        => source == null || source.Count <= 0;

    public static bool IsNullOrEmpty<T>(this ICollection<T> source)
        => source == null || source.Count <= 0;

    public static bool IsNullOrEmptyReadOnly<T>(this IReadOnlyCollection<T> source)
        => source == null || source.Count <= 0;

    public static IEnumerable<ICollection<T>> ToBatchesOf<T>(this IEnumerable<T> source, int batchSize, bool serial = false, bool distinct = false)
    {
        if (source == null)
        {
            yield break;
        }

        if (batchSize > 500000)
        {
            batchSize = 500000;
        }

        var batch = distinct
                        ? new HashSet<T>(batchSize)
                        : (ICollection<T>)new List<T>(batchSize);

        foreach (var item in source)
        {
            batch.Add(item);

            if (batch.Count < batchSize)
            {
                continue;
            }

            yield return batch;

            if (serial)
            {
                batch.Clear();
            }
            else
            {
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public static async IAsyncEnumerable<List<T>> ToBatchesOfAsync<T>(this IAsyncEnumerable<T> source, int batchSize, bool serial = false)
    {
        if (source == null)
        {
            yield break;
        }

        if (batchSize > 500000)
        {
            batchSize = 500000;
        }

        var batch = new List<T>(batchSize);

        await foreach (var item in source)
        {
            batch.Add(item);

            if (batch.Count < batchSize)
            {
                continue;
            }

            yield return batch;

            if (serial)
            {
                batch.Clear();
            }
            else
            {
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    public static bool Match<TKey, TValue>(this IDictionary<TKey, TValue> source, IDictionary<TKey, TValue> other,
                                           IEqualityComparer<TValue> valueComparer = null)
    {
        if (source == null || other == null)
        {
            return false;
        }

        if (source.Count != other.Count)
        {
            return false;
        }

        valueComparer ??= EqualityComparer<TValue>.Default;

        foreach (var (key, value) in source)
        {
            if (!other.ContainsKey(key) || !valueComparer.Equals(value, other[key]))
            {
                return false;
            }
        }

        return true;
    }

    public static IEnumerable<IEnumerable<T>> ToLazyBatchesOf<T>(this IEnumerable<T> source, int batchSize, Func<T, bool> predicate = null)
    {
        if (source == null)
        {
            yield break;
        }

        using(var sourceEnumerator = source.GetEnumerator())
        {
            while (sourceEnumerator.MoveNext())
            {
                yield return TakeFromEnumeratorInternal(sourceEnumerator, batchSize, predicate);
            }
        }
    }

    private static IEnumerable<T> TakeFromEnumeratorInternal<T>(IEnumerator<T> source, int take, Func<T, bool> predicate = null)
    {
        var yielded = 0;

        do
        {
            if (predicate != null && !predicate(source.Current))
            {
                continue;
            }

            yield return source.Current;

            yielded++;
        } while (yielded < take && source.MoveNext());
    }
}
