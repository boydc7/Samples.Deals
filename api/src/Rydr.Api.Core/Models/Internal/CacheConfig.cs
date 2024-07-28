using System.Collections.Concurrent;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Services.Internal;

namespace Rydr.Api.Core.Models.Internal;

public class CacheConfig
{
    public const string InvariantSessionKey = "invariant-rydrsession";

    private static readonly ConcurrentDictionary<string, CacheConfig> _configMap = new();

    private static readonly CacheConfig _nullConfig = null;

    private static readonly CacheConfig _defaultConfig = new()
                                                         {
                                                             DurationSeconds = 60 * 20
                                                         };

    public static CacheConfig FrameworkConfig { get; } = new()
                                                         {
                                                             DurationSeconds = 60 * 60 * 2
                                                         };

    public static CacheConfig LongConfig { get; } = new()
                                                    {
                                                        DurationSeconds = 60 * 60 * 24 * 15
                                                    };

    public static CacheConfig FromSeconds(int seconds) => new()
                                                          {
                                                              DurationSeconds = seconds
                                                          };

    public static CacheConfig FromMinutes(int minutes) => new()
                                                          {
                                                              DurationSeconds = minutes * 60
                                                          };

    public static CacheConfig FromHours(int hours) => new()
                                                      {
                                                          DurationSeconds = hours * 60 * 60
                                                      };

    public static CacheConfig FromDays(int days) => new()
                                                    {
                                                        DurationSeconds = days * 24 * 60 * 60
                                                    };

    public int DurationSeconds { get; set; }
    public int MinutesPastMidnight { get; set; }
    public bool UseLocalCache { get; set; }

    public static CacheConfig GetConfig(string typeName, string backupTypeName = null)
        => GetOrCreateConfig(typeName) ?? GetOrCreateConfig(backupTypeName);

    private static CacheConfig GetOrCreateConfig(string typeName)
    {
        if (!typeName.HasValue())
        {
            return _nullConfig;
        }

        var key = string.Concat("Caching.", typeName);

        var config = _configMap.GetOrAdd(key,
                                         k =>
                                         {
                                             var cacheConfigDurationOnlyEntry = RydrEnvironment.AppSettings.Get(k, int.MinValue);

                                             if (cacheConfigDurationOnlyEntry > int.MinValue)
                                             {
                                                 return cacheConfigDurationOnlyEntry <= 0
                                                            ? _defaultConfig
                                                            : new CacheConfig
                                                              {
                                                                  DurationSeconds = cacheConfigDurationOnlyEntry
                                                              };
                                             }

                                             var cacheConfigEntries = Try.Get(() => RydrEnvironment.AppSettings.GetDictionary(k));

                                             if (cacheConfigEntries == null)
                                             {
                                                 return _nullConfig;
                                             }

                                             var cc = new CacheConfig
                                                      {
                                                          DurationSeconds = cacheConfigEntries.ContainsKey("DurationSeconds")
                                                                                ? cacheConfigEntries["DurationSeconds"].ToInteger()
                                                                                : 0,
                                                          MinutesPastMidnight = cacheConfigEntries.ContainsKey("MinutesPastMidnight")
                                                                                    ? cacheConfigEntries["MinutesPastMidnight"].ToInteger()
                                                                                    : 0
                                                      };

                                             // If there is an entry but nothing specified, use the default, otherwise use what was specified
                                             return cc.DurationSeconds > 0 || cc.MinutesPastMidnight > 0
                                                        ? cc
                                                        : _defaultConfig;
                                         });

        return config;
    }
}

public static class CacheExpiry
{
    public static bool HasValidExpiry(this CacheConfig config)
        => config != null && GetCacheExpireTime(config.DurationSeconds, config.MinutesPastMidnight).HasValue;

    public static TimeSpan? GetCacheExpireTime(int durationSeconds, int minutesAfterMidnightUtc)
    {
        if (minutesAfterMidnightUtc > 0)
        {
            return GetCacheExpireTime(minutesAfterMidnightUtc);
        }

        if (durationSeconds > 0)
        {
            return new TimeSpan(0, 0, durationSeconds);
        }

        return null;
    }

    public static TimeSpan GetCacheExpireTime(int minutesAfterMidnightUtc, DateTime? nowUtc = null)
    {
        var nowUtcValue = nowUtc ?? DateTimeHelper.UtcNow;

        var expireAtToday = nowUtcValue.Date.AddMinutes(minutesAfterMidnightUtc);

        if (nowUtcValue < expireAtToday)
        {
            // Current date/time is before the expire at time for today (i.e. today's expire at time
            // has not come and gone yet), so return a timespan that represents the time until
            // today's expire at point from now
            return expireAtToday - nowUtcValue;
        }

        // Current date/time is after the expire at time for today, so we cache until tomorrow's
        // expire at point, hence return a timespan indicating the time until tomorrow's expire point
        var expireAtTomorrow = nowUtcValue.Date.AddDays(1).AddMinutes(minutesAfterMidnightUtc);

        return expireAtTomorrow - nowUtcValue;
    }
}
