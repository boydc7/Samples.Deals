using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;
using StatsdClient;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Rydr.Api.Core.Services.Internal;

public class DogStatsD : IStats
{
    private readonly bool _isConfigured;

    public DogStatsD()
    {
        var config = RydrEnvironment.GetAppSetting("Stats.StatsD.Server");

        string server = null;
        var port = 0;

        if (config != null && config.IndexOf(":", StringComparison.Ordinal) > 0)
        {
            server = config.Split(':')[0];
            port = Convert.ToInt32(config.Split(':')[1]);
        }
        else if (!string.IsNullOrEmpty(config))
        {
            server = config;
            port = 8125;
        }

        if (string.IsNullOrEmpty(server))
        {
            return;
        }

        DogStatsd.Configure(new StatsdConfig
                            {
                                StatsdServerName = server,
                                StatsdPort = port,
                                ConstantTags = new[]
                                               {
                                                   string.Concat("env:", RydrEnvironment.CurrentEnvironment)
                                               }
                            });

        _isConfigured = true;
    }

    public static DogStatsD Default { get; } = new();

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public bool Timing(string key, long value, params string[] tags)
    {
        if (_isConfigured)
        {
            DogStatsd.Timer(key, value, tags: tags);
        }

        return _isConfigured;
    }

    public bool Increment(string key, int magnitude = 1)
    {
        if (_isConfigured)
        {
            DogStatsd.Increment(key, magnitude);
        }

        return _isConfigured;
    }

    public bool Gauge(string key, long value)
    {
        if (_isConfigured)
        {
            DogStatsd.Gauge(key, value);
        }

        return _isConfigured;
    }

    public async Task<T> MeasureAsync<T>(string key, Func<Task<T>> action)
    {
        if (!_isConfigured)
        {
            return await action();
        }

        var sw = Stopwatch.StartNew();

        T result;

        try
        {
            result = await action();
        }
        finally
        {
            sw.Stop();
            Timing(key, sw.ElapsedMilliseconds);
        }

        return result;
    }

    public async Task<T> MeasureAsync<T>(IEnumerable<string> keys, Func<Task<T>> asyncAction, params string[] tags)
    {
        if (!_isConfigured)
        {
            return await asyncAction();
        }

        T result;

        var sw = Stopwatch.StartNew();

        try
        {
            result = await asyncAction();
        }
        finally
        {
            sw.Stop();

            foreach (var key in keys)
            {
                Timing(key, sw.ElapsedMilliseconds, tags);
            }
        }

        return result;
    }

    public T Measure<T>(string key, Func<T> action)
        => _isConfigured
               ? DogStatsd.Time(action, key)
               : action();

    public T Measure<T>(IEnumerable<string> keys, Func<T> action)
    {
        if (!_isConfigured)
        {
            return action();
        }

        T result;

        var sw = Stopwatch.StartNew();

        try
        {
            result = action();
        }
        finally
        {
            sw.Stop();

            foreach (var key in keys)
            {
                Timing(key, sw.ElapsedMilliseconds);
            }
        }

        return result;
    }

    public void Measure(string key, Action action)
    {
        if (_isConfigured)
        {
            DogStatsd.Time(action, key);
        }
        else
        {
            action();
        }
    }
}
