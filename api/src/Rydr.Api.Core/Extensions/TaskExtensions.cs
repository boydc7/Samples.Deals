namespace Rydr.Api.Core.Extensions;

public static class TaskExtensions
{
    public static async Task EachWhileAsync<T>(this IAsyncEnumerable<T> source, Func<T, int, bool> doWhile)
    {
        var index = 0;

        await foreach (var item in source)
        {
            if (!doWhile(item, index))
            {
                return;
            }

            index++;
        }
    }

    public static async Task<T> SingleOrDefaultAsync<T>(this Task<List<T>> sourceTask, Func<T, bool> predicate = null)
    {
        var source = await sourceTask;

        return source is { Count: > 0 }
                   ? predicate == null
                         ? source[0]
                         : source.FirstOrDefault(predicate)
                   : default;
    }

    public static Task<Dictionary<TKey, T>> ToDictionarySafeAsync<T, TKey>(this Task<IEnumerable<T>> source, Func<T, TKey> keySelector)
        => ToDictionarySafeAsync(source, keySelector, v => v);

    public static Task<Dictionary<TKey, TVal>> ToDictionarySafeAsync<T, TKey, TVal>(this Task<IEnumerable<T>> source, Func<T, TKey> keySelector, Func<T, TVal> valSelector)
    {
        var tcs = new TaskCompletionSource<Dictionary<TKey, TVal>>();

        source.ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    tcs.TrySetException(t.Exception.InnerExceptions);
                                }
                                else if (t.IsCanceled)
                                {
                                    tcs.TrySetCanceled();
                                }
                                else
                                {
                                    var map = new Dictionary<TKey, TVal>();

                                    if (t.Result != null)
                                    {
                                        foreach (var sourceValue in t.Result)
                                        {
                                            map[keySelector(sourceValue)] = valSelector(sourceValue);
                                        }
                                    }

                                    tcs.TrySetResult(map);
                                }
                            }, TaskContinuationOptions.ExecuteSynchronously);

        return tcs.Task;
    }

    public static Task<Dictionary<TKey, T>> ToDictionarySafeAsync<T, TKey>(this Task<List<T>> source, Func<T, TKey> keySelector)
        => ToDictionarySafeAsync(source, keySelector, v => v);

    public static Task<Dictionary<TKey, TVal>> ToDictionarySafeAsync<T, TKey, TVal>(this Task<List<T>> source, Func<T, TKey> keySelector, Func<T, TVal> valSelector)
    {
        var tcs = new TaskCompletionSource<Dictionary<TKey, TVal>>();

        source.ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    tcs.TrySetException(t.Exception.InnerExceptions);
                                }
                                else if (t.IsCanceled)
                                {
                                    tcs.TrySetCanceled();
                                }
                                else
                                {
                                    var map = new Dictionary<TKey, TVal>();

                                    if (t.Result != null)
                                    {
                                        foreach (var sourceValue in t.Result)
                                        {
                                            map[keySelector(sourceValue)] = valSelector(sourceValue);
                                        }
                                    }

                                    tcs.TrySetResult(map);
                                }
                            }, TaskContinuationOptions.ExecuteSynchronously);

        return tcs.Task;
    }

    public static Task<List<T>> SelectManyToListAsync<T>(this IAsyncEnumerable<IEnumerable<T>> source,
                                                         int take = 0,
                                                         int skip = 0)
        => SelectManyToListAsync(source, b => b.Select(s => s), take, skip);

    public static async Task<List<TResult>> SelectManyToListAsync<T, TResult>(this IAsyncEnumerable<IEnumerable<T>> source,
                                                                              Func<IEnumerable<T>, IEnumerable<TResult>> linq,
                                                                              int take = 0,
                                                                              int skip = 0)
    {
        if (linq == null)
        {
            throw new ArgumentNullException(nameof(linq));
        }

        var results = new List<TResult>(take is > 0 and <= 1000
                                            ? take
                                            : 0);

        if (take <= 0)
        {
            take = int.MaxValue;
        }

        if (source == null)
        {
            return results;
        }

        var count = 0;

        await foreach (var entityBatch in source)
        {
            results.AddRange(linq(entityBatch.Select(e =>
                                                     {
                                                         count++;

                                                         return e;
                                                     })
                                             .SkipWhile(_ => count < skip)));

            if (results.Count >= take)
            {
                break;
            }
        }

        return results;
    }

    public static async Task<List<T>> TakeManyToListAsync<T>(this IAsyncEnumerable<IEnumerable<T>> source,
                                                             int skip = 0,
                                                             int take = 0,
                                                             Func<T, bool> predicate = null)
    {
        var results = new List<T>(take is > 0 and <= 1000
                                      ? take
                                      : 0);

        if (take <= 0)
        {
            take = int.MaxValue;
        }

        if (source == null)
        {
            return results;
        }

        var count = 0;

        await foreach (var entityBatch in source)
        {
            if (predicate == null)
            {
                results.AddRange(entityBatch.Select(e =>
                                                    {
                                                        count++;

                                                        return e;
                                                    })
                                            .SkipWhile(_ => count < skip));
            }
            else
            {
                results.AddRange(entityBatch.Where(predicate)
                                            .Select(e =>
                                                    {
                                                        count++;

                                                        return e;
                                                    })
                                            .SkipWhile(_ => count < skip));
            }

            if (results.Count >= take)
            {
                break;
            }
        }

        return results;
    }

    public static async Task<T> FirstOrDefaultAsync<T>(this IEnumerable<T> source, Func<T, Task<bool>> asyncPredicate)
    {
        if (source == null)
        {
            return default;
        }

        foreach (var item in source)
        {
            if (await asyncPredicate(item))
            {
                return item;
            }
        }

        return default;
    }

    public static async Task<T> FirstOrDefaultAsync<T>(this IEnumerable<T> source, Func<T, ValueTask<bool>> asyncPredicate)
    {
        if (source == null)
        {
            return default;
        }

        foreach (var item in source)
        {
            if (await asyncPredicate(item))
            {
                return item;
            }
        }

        return default;
    }

    public static async Task<T> FirstOrDefaultAsync<T, TWhere>(this IEnumerable<T> source, Func<T, ValueTask<TWhere>> where,
                                                               Func<TWhere, bool> predicate)
    {
        if (source == null)
        {
            return default;
        }

        foreach (var item in source)
        {
            var result = await where(item);

            if (predicate(result))
            {
                return item;
            }
        }

        return default;
    }
}
