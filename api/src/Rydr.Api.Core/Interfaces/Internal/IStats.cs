using System.Runtime.CompilerServices;

namespace Rydr.Api.Core.Interfaces.Internal;

public interface IStats : IDisposable
{
    bool Timing(string key, long value, params string[] tags);
    bool Increment(string key, int magnitude = 1);
    bool Gauge(string key, long value);
    Task<T> MeasureAsync<T>(string key, Func<Task<T>> action);
    Task<T> MeasureAsync<T>(IEnumerable<string> keys, Func<Task<T>> action, params string[] tags);
    T Measure<T>(string key, Func<T> action);
    T Measure<T>(IEnumerable<string> keys, Func<T> action);
    void Measure(string key, Action action);
}

public interface IStatsProfiler : IDisposable
{
    void Start(Type fromType, string customKey = null, [CallerMemberName] string methodName = null);
    void Stop(Type fromType, string customKey = null, [CallerMemberName] string methodName = null);
    T Measure<T>(Type fromType, Func<T> action, string customKey = null, [CallerMemberName] string methodName = null);
    void Measure(Type fromType, Action action, string customKey = null, [CallerMemberName] string methodName = null);
    void Log();
}
