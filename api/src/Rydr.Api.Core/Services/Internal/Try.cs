using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public static class Try
{
    private static readonly ILog _log = LogManager.GetLogger("Try");

    public static async Task<T> GetAsync<T>(this Func<Task<T>> getter, T defaultValue = default, int maxAttempts = 1)
    {
        try
        {
            var result = await ExecAsync(getter, null, maxAttempts, noLog: true);

            return result;
        }
        catch
        {
            return defaultValue;
        }
    }

    public static T Get<T>(this Func<T> getter, T defaultValue = default, int maxAttempts = 1)
    {
        try
        {
            return Exec(getter, null, maxAttempts, noLog: true);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static bool Get<T>(this Func<T> getter, out T value, T defaultValue = default, int maxAttempts = 1)
    {
        try
        {
            value = Exec(getter, maxAttempts: maxAttempts, noLog: true);

            return true;
        }
        catch
        {
            value = defaultValue;

            return false;
        }
    }

    public static async Task ExecAsync(this Func<Task> block)
    {
        try
        {
            await block();
        }
        catch(Exception x)
        {
            _log.Warn("Try.Exec failed", x);
        }
    }

    public static void Exec(this Action block)
    {
        try
        {
            block();
        }
        catch(Exception x)
        {
            _log.Warn("Try.Exec failed", x);
        }
    }

    public static async Task ExecIgnoreNotFoundAsync(this Func<Task> block)
    {
        try
        {
            await block();
        }
        catch(NullReferenceException)
        {
            // ignored
        }
        catch(RecordNotFoundException)
        {
            // ignored
        }
    }

    public static T Exec<T>(this Func<T> block,
                            Func<Exception, bool> retryIf = null,
                            int maxAttempts = 3,
                            int waitMultiplierMs = 257,
                            bool noLog = false)
    {
        if (retryIf == null)
        {
            retryIf = x => true;
        }

        Exception lastEx = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return block();
            }
            catch(Exception ex) when(retryIf(ex))
            {
                lastEx = ex;
            }

            if (maxAttempts > 1)
            {
                var sleepForMs = attempt * waitMultiplierMs;
                Thread.Sleep(sleepForMs);
            }
        }

        if (!noLog)
        {
            _log.Exception(lastEx, "Could not successfully Try.Exec");
        }

        return default;
    }

    public static async Task<T> ExecAsync<T>(this Func<Task<T>> block,
                                             Func<Exception, bool> retryIf = null,
                                             int maxAttempts = 3,
                                             int waitMultiplierMs = 257,
                                             bool noLog = false)
    {
        retryIf ??= x => true;

        Exception lastEx = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await block();
            }
            catch(Exception ex) when(retryIf(ex))
            {
                lastEx = ex;
            }

            if (maxAttempts > 1)
            {
                var sleepForMs = attempt * waitMultiplierMs;
                Thread.Sleep(sleepForMs);
            }
        }

        if (!noLog)
        {
            _log.Exception(lastEx, "Could not successfully Try.ExecAsync");
        }

        return default;
    }
}
