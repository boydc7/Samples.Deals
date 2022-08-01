using System;
using System.Runtime.CompilerServices;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal
{
    public static class StatsProfilerFactory
    {
        private static Func<IStatsProfiler> _factory;

        public static void SetFactory(Func<IStatsProfiler> factory)
        {
            _factory = factory;
        }

        public static Func<IStatsProfiler> Factory => _factory ??= () => NullStatsProfiler.Instance;

        public static IStatsProfiler Create => Factory();
    }

    public class NullStatsProfiler : IStatsProfiler
    {
        private NullStatsProfiler() { }

        public static NullStatsProfiler Instance { get; } = new NullStatsProfiler();

        public void Start(Type fromType, string customKey = null, [CallerMemberName] string methodName = null) { }
        public void Stop(Type fromType, string customKey = null, [CallerMemberName] string methodName = null) { }
        public T Measure<T>(Type fromType, Func<T> action, string customKey = null, [CallerMemberName] string methodName = null) => action();
        public void Measure(Type fromType, Action action, string customKey = null, [CallerMemberName] string methodName = null) => action();
        public void Log() { }

        public void Dispose() { }
    }
}
