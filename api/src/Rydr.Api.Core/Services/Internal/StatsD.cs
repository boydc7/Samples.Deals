using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public class StatsD : IStats
{
    private readonly IUdpClient _client;
    private readonly int _port;
    private readonly string _server;

    public static StatsD Default { get; } = new(WrappedUdpClient.DefaultClient);

    public StatsD(IUdpClient client)
    {
        _client = client;

        if (_server != null)
        {
            return;
        }

        var config = RydrEnvironment.GetAppSetting("Stats.StatsD.Server");

        if (config != null && config.IndexOf(":", StringComparison.Ordinal) > 0)
        {
            _server = config.Split(':')[0];
            _port = Convert.ToInt32(config.Split(':')[1]);
        }
        else if (!string.IsNullOrEmpty(config))
        {
            _server = config;
            _port = 8125;
        }

        if (string.IsNullOrEmpty(_server))
        {
            return;
        }

        if (Regex.IsMatch(_server, @"\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b"))
        {
            return;
        }

        try
        {
            var result = Dns.GetHostAddresses(_server);

            _server = result.Length > 0
                          ? result[0].ToString()
                          : null;
        }
        catch(SocketException)
        {
            _server = null;
        }
    }

    public bool Timing(string key, long value, params string[] tags)
    {
        var message = string.Concat(key, ":", value, "|ms");

        return _client.Initialize(_server, _port) && Send(message);
    }

    public async Task<bool> TimingAsync(string key, long value)
    {
        var message = string.Concat(key, ":", value, "|ms");

        return _client.Initialize(_server, _port) && await SendAsync(message);
    }

    public T Measure<T>(IEnumerable<string> keys, Func<T> action)
    {
        if (!_client.Initialize(_server, _port))
        {
            return action();
        }

        var sw = Stopwatch.StartNew();

        T result;

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

    public async Task<T> MeasureAsync<T>(string key, Func<Task<T>> action)
    {
        if (!_client.Initialize(_server, _port))
        {
            return await action();
        }

        T result;

        var sw = Stopwatch.StartNew();

        try
        {
            result = await action();
        }
        finally
        {
            sw.Stop();
            await TimingAsync(key, sw.ElapsedMilliseconds);
        }

        return result;
    }

    public async Task<T> MeasureAsync<T>(IEnumerable<string> keys, Func<Task<T>> asyncAction, params string[] tags)
    {
        if (!_client.Initialize(_server, _port))
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
                await TimingAsync(key, sw.ElapsedMilliseconds);
            }
        }

        return result;
    }

    public T Measure<T>(string key, Func<T> action)
    {
        if (!_client.Initialize(_server, _port))
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
            Timing(key, sw.ElapsedMilliseconds);
        }

        return result;
    }

    public void Measure(string key, Action action)
    {
        if (!_client.Initialize(_server, _port))
        {
            action();

            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            Timing(key, sw.ElapsedMilliseconds);
        }
    }

    public bool Increment(string key, int magnitude = 1)
    {
        var message = string.Concat(key, ":", magnitude, "|c");

        return _client.Initialize(_server, _port) && Send(message);
    }

    public bool Gauge(string key, long value)
    {
        var message = string.Concat(key, ":", value, "|g");

        return _client.Initialize(_server, _port) && Send(message);
    }

    public void Dispose()
    {
        if (!_client.Initialize(_server, _port))
        {
            return;
        }

        _client?.Close();
    }

    private bool Send(string message) => _client.Send(message);
    private async Task<bool> SendAsync(string message) => await _client.SendAsync(message);
}

public static class Stats
{
    public const string AllApiFired = "All.Fired";
    public const string AllApiResponseTime = "All.ResponseTime";
    public const string AllApiOpenRequests = "All.OpenRequests";

    public static string StatsKey(string statNameSpace, string statName) => string.Concat(statNameSpace, ".", statName);
}

public static class StatsKeySuffix
{
    public const string CacheMiss = "CacheMiss";
    public const string CacheHit = "CacheHit";
    public const string Exception = "Exception";
    public const string Downloaded = "Downloaded";
    public const string Uploaded = "Uploaded";
    public const string Staged = "Staged";
    public const string Duration = "Duration";
    public const string Failed = "Failed";
    public const string Fired = "Fired";
    public const string Throttled = "Throttled";
    public const string Known = "Known";
    public const string Matched = "Matched";
    public const string Processed = "Processed";
    public const string Processing = "Processing";
    public const string Queued = "Queued";
    public const string Archived = "Archived";
    public const string ResponseTime = "ResponseTime";
    public const string Unmatched = "Unmatched";
    public const string NotMatched = "NotMatched";
    public const string Ignored = "Ignored";
}
