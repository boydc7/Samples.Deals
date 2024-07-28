using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public class LoggedStatsProfiler : IStatsProfiler
{
    private const string _totalKey = "--TOTAL--";

    private readonly ILog _log;
    private readonly ConcurrentDictionary<string, TimerInfo> _timers = new(StringComparer.OrdinalIgnoreCase);

    public LoggedStatsProfiler()
    {
        _log = LogManager.GetLogger(GetType());

        Start(_totalKey);
    }

    public void Start(Type fromType, string customKey = null, [CallerMemberName] string methodName = null)
        => Start(GetKey(fromType, customKey, methodName));

    public void Stop(Type fromType, string customKey = null, [CallerMemberName] string methodName = null)
        => Stop(GetKey(fromType, customKey, methodName));

    public void Dispose() => Log();

    public T Measure<T>(Type fromType, Func<T> action, string customKey = null, [CallerMemberName] string methodName = null)
        => Measure(GetKey(fromType, customKey, methodName), action);

    public void Measure(Type fromType, Action action, string customKey = null, [CallerMemberName] string methodName = null)
        => Measure(GetKey(fromType, customKey, methodName), action);

    private string GetKey(Type forType, string customKey, string methodName)
        => string.Concat(forType.Name, ".", methodName, ":", customKey);

    private class TimerInfo
    {
        public Stopwatch Timer { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public DateTime CreatedOn { get; set; }
        public int Count { get; set; }
    }

    private TimerInfo GetOrCreateTimer(string key)
    {
        var timerInfo = _timers.GetOrAdd(key, new TimerInfo
                                              {
                                                  Timer = Stopwatch.StartNew(),
                                                  Count = 0,
                                                  CreatedOn = DateTimeHelper.UtcNow
                                              });

        return timerInfo;
    }

    private TimerInfo GetTimer(string key)
    {
        _timers.TryGetValue(key, out var timerInfo);

        return timerInfo;
    }

    private void Stop(string key)
    {
        if (key.EqualsOrdinalCi(_totalKey))
        {
            return;
        }

        try
        {
            var timer = GetTimer(key);

            if (!timer.Timer.IsRunning)
            {
                return;
            }

            timer.Timer.Stop();
        }
        catch(Exception x)
        {
            _log.Exception(x);
        }
    }

    private void Start(string key)
    {
        try
        {
            var timer = GetOrCreateTimer(key);

            timer.Timer.Start();
            timer.Count++;
        }
        catch(Exception x)
        {
            _log.Exception(x);
        }
    }

    private void StopAll()
    {
        try
        {
            foreach (var timer in _timers.Where(t => !t.Key.EqualsOrdinalCi(_totalKey) &&
                                                     t.Value.Timer.IsRunning))
            {
                timer.Value.Timer.Stop();
            }

            if (_timers.TryGetValue(_totalKey, out var totalTimer) && totalTimer.Timer.IsRunning)
            {
                totalTimer.Timer.Stop();
            }
        }
        catch(Exception x)
        {
            _log.Exception(x);
        }
    }

    private void Measure(string key, Action block)
    {
        try
        {
            Start(key);
            block();
        }
        finally
        {
            Stop(key);
        }
    }

    private T Measure<T>(string key, Func<T> block)
    {
        try
        {
            Start(key);

            return block();
        }
        finally
        {
            Stop(key);
        }
    }

    public void Log()
    {
        // Stop everything
        StopAll();

        if (_timers.Count <= 1)
        {
            return;
        }

        _log.Info("-".PadRight(100, '-'));
        _log.Info("Summary of StatsProfiler");
        _log.Info("-".PadRight(100, '-'));

        foreach (var t in _timers.OrderByDescending(kvp => kvp.Value.Timer.ElapsedTicks))
        {
            var time = $"[{(int)Math.Floor(t.Value.Timer.Elapsed.TotalMinutes)}:{t.Value.Timer.Elapsed.Seconds}.{t.Value.Timer.Elapsed.Milliseconds}]".PadRight(12, ' ');
            var executions = $"{t.Value.Count}".PadLeft(5, ' ');

            _log.Info($"  {time} over [{executions} ] executions --- {t.Key}");
        }

        _log.Info("-".PadRight(100, '-'));
    }
}
