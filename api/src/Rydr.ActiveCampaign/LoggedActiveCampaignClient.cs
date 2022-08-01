using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack;

namespace Rydr.ActiveCampaign
{
    public class LoggedActiveCampaignClient : ActiveCampaignClient
    {
        private long _sequence = 10001;

        public LoggedActiveCampaignClient(string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId)
            : base(accountName, apiKey, eventTrackingKey, eventTrackingAcctId) { }

        protected override async Task<T> GetAsync<T>(string path, object filters = null, int forceLimit = 0, int forceOffset = 0)
        {
            var seq = LogRequest("GET", path, filters);

            T response = default;

            var url = GetUrl(path, filters);

            try
            {
                var responseTuple = await GetAsyncInternal<T>(url).ConfigureAwait(false);

                response = responseTuple.Response;
            }
            finally
            {
                LogResponse("GET", response, seq, $"RequestUrl: [{url}]");
            }

            return response;
        }

        protected override async IAsyncEnumerable<IReadOnlyList<T>> GetPagedAsync<TRequest, T>(string path, int limit, object filters = null)
        {
            var seq = LogRequest("GET", path, filters);

            var count = 0;

            await foreach (var item in base.GetPagedAsync<TRequest, T>(path, limit, filters).ConfigureAwait(false))
            {
                yield return item;

                count++;
            }

            LogResponse("GET", new
                               {
                                   PagedResponse = true,
                                   Type = typeof(T).Name,
                                   ResultCount = count
                               }, seq);
        }

        protected override async Task<T> PostAsync<T>(string path, object filters = null, T bodyContent = null)
            where T : class
        {
            var seq = LogRequest("POST", path, filters);

            T response = default;

            var url = GetUrl(path, filters);

            try
            {
                var responseTuple = await PostAsyncInternal(url, bodyContent).ConfigureAwait(false);

                response = responseTuple.Response;
            }
            finally
            {
                LogResponse("POST", response, seq, $"RequestUrl: [{url}]");
            }

            return response;
        }

        protected override async Task<T> PutAsync<T>(string path, object filters = null, T bodyContent = null)
            where T : class
        {
            var seq = LogRequest("PUT", path, filters);

            T response = default;

            var url = GetUrl(path, filters);

            try
            {
                var responseTuple = await PutAsyncInternal(url, bodyContent).ConfigureAwait(false);

                response = responseTuple.Response;
            }
            finally
            {
                LogResponse("PUT", response, seq, $"RequestUrl: [{url}]");
            }

            return response;
        }

        protected override async Task DeleteAsync(string path)
        {
            var seq = LogRequest("DELETE", path, null);

            var url = GetUrl(path);

            try
            {
                await DeleteAsyncInternal(url).ConfigureAwait(false);
            }
            finally
            {
                LogResponse<object>("DELETE", null, seq, $"RequestUrl: [{url}]");
            }
        }

        private long LogRequest(string verb, string path, object parameters)
        {
            var paramJsv = parameters == null
                               ? "<null>"
                               : parameters.ToJsv();

            var log = string.Concat("path=[", path, "], params=[", paramJsv, "]");

            return Log($"REQUEST - {verb}", log);
        }

        private void LogResponse<T>(string verb, T response, long sequence, string info = null)
            => Log($"RESPONSE - {verb}", string.Concat(info ?? string.Empty, "\n", response?.ToJsv().Left(1000)) ?? "NO RESPONSE", sequence);

        private long Log(string method, string log, long sequence = 0)
        {
            var logSequence = sequence > 0
                                  ? sequence
                                  : Interlocked.Increment(ref _sequence);

            _log.DebugFormat("|{0}|{1}|: {2}", logSequence, method, log);

            return logSequence;
        }
    }
}
