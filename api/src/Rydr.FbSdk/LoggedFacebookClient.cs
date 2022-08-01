using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.FbSdk
{
    public class LoggedFacebookClient : FacebookClient
    {
        private readonly ILog _log;

        private long _sequence = 10001;

        public LoggedFacebookClient(string appId, string appSecret, string accessToken)
            : base(appId, appSecret, accessToken)
        {
            _log = LogManager.GetLogger(GetType());
        }

        protected override async Task<T> GetAsync<T>(string path, object parameters = null, bool eTag = false)
        {
            var seq = LogRequest(path, parameters);

            T response = default;

            var url = GetUrl(path, parameters);

            try
            {
                var responseTuple = await GetAsyncInternal<T>(url, eTag).ConfigureAwait(false);

                response = responseTuple.Response;
            }
            finally
            {
                LogResponse(response, seq, $"RequestUrl: [{url}]");
            }

            return response;
        }

        protected override async Task<T> PostAsync<T>(string initialPath, object parameters = null, string withSecretProof = null)
        {
            var seq = LogRequest(initialPath, parameters);

            T response = default;

            var url = GetUrl(initialPath, parameters, withSecretProof);

            try
            {
                var responseTuple = await PostAsyncInternal<T>(url).ConfigureAwait(false);

                response = responseTuple.Response;
            }
            finally
            {
                LogResponse(response, seq, $"RequestUrl: [{url}]");
            }

            return response;
        }

        protected override async IAsyncEnumerable<List<T>> GetPagedAsync<T>(string initialPath, object parameters = null, bool eTag = false)
        {
            var seq = LogRequest(initialPath, parameters);

            var count = 0;

            await foreach (var item in base.GetPagedAsync<T>(initialPath, parameters, eTag).ConfigureAwait(false))
            {
                yield return item;

                count++;
            }

            LogResponse(new
                        {
                            PagedResponse = true,
                            Type = typeof(T).Name,
                            ResultCount = count
                        }, seq);
        }

        protected override IEnumerable<T> GetPaged<T>(string initialPath, object parameters = null)
        {
            var seq = LogRequest(initialPath, parameters);

            var count = 0;

            foreach (var item in base.GetPaged<T>(initialPath, parameters))
            {
                yield return item;

                count++;
            }

            LogResponse(new
                        {
                            PagedResponse = true,
                            Type = typeof(T).Name,
                            ResultCount = count
                        }, seq);
        }

        private long LogRequest(string path, object parameters)
        {
            var paramJsv = parameters == null
                               ? "<null>"
                               : parameters.ToJsv();

            var log = string.Concat("path=[", path, "], params=[", paramJsv, "]");

            return Log("REQUEST", log);
        }

        private void LogResponse<T>(T response, long sequence, string info = null)
            => Log("RESPONSE", string.Concat(info ?? string.Empty, "\n", response?.ToJsv().Left(1000)) ?? "NO RESPONSE", sequence);

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
