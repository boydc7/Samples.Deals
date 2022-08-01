using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal
{
    public class NullStatsD : IStats
    {
        public static NullStatsD Default { get; } = new NullStatsD();

        public void Dispose() { }

        public bool Timing(string key, long value, params string[] tags) => false;

        public T Measure<T>(IEnumerable<string> keys, Func<T> action) => action();

        public Task<T> MeasureAsync<T>(IEnumerable<string> keys, Func<T> action)
        {
            var result = action();

            return Task.FromResult(result);
        }

        public T Measure<T>(string key, Func<T> action) => action();

        public void Measure(string key, Action action) => action();

        public Task<T> MeasureAsync<T>(string key, Func<Task<T>> action) => action();

        public Task<T> MeasureAsync<T>(IEnumerable<string> keys, Func<Task<T>> asyncAction, params string[] tags) => asyncAction();

        public bool Increment(string key, int magnitude = 1) => false;

        public bool Gauge(string key, long value) => false;
    }
}
