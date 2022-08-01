using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Rydr.ActiveCampaign.Configuration;
using Rydr.ActiveCampaign.Enums;
using Rydr.ActiveCampaign.Models;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.ActiveCampaign
{
    public partial class ActiveCampaignClient : IActiveCampaignClient
    {
        protected readonly string _eventTrackingKey;
        protected readonly string _eventTrackingAcctId;
        protected static readonly ILog _log = LogManager.GetLogger("ActiveCampaignClient");

        private readonly HttpClient _httpClient;

        static ActiveCampaignClient()
        {
            ActiveCampaignSdkConfig.Configure();
        }

        public ActiveCampaignClient(string accountName, string apiKey, string eventTrackingKey, string eventTrackingAcctId)
        {
            _eventTrackingKey = eventTrackingKey;
            _eventTrackingAcctId = eventTrackingAcctId;

            var baseApiUrl = string.Concat("https://", accountName, ".api-us1.com/api/3/");

            var handler = new HttpClientHandler
                          {
                              AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                          };

            _httpClient = new HttpClient(handler)
                          {
                              BaseAddress = new Uri(baseApiUrl)
                          };

            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RydrNETAcSdk", "1.0"));
            _httpClient.DefaultRequestHeaders.Add("Api-Token", apiKey);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        protected virtual async Task<T> GetAsync<T>(string path, object filters = null, int limit = 0, int offset = 0)
        {
            var url = GetUrl(path, filters, limit, offset);

            var responseTuple = await GetAsyncInternal<T>(url).ConfigureAwait(false);

            return responseTuple.Response;
        }

        protected virtual async IAsyncEnumerable<IReadOnlyList<T>> GetPagedAsync<TRequest, T>(string path, int limit, object filters = null)
            where TRequest : AcCollectionBase<T>
        {
            if (limit <= 0)
            {
                limit = 50;
            }

            var offset = 0;

            do
            {
                var response = await GetAsync<TRequest>(path, filters, limit, offset).ConfigureAwait(false);

                if (response == null)
                {
                    yield break;
                }

                if (response.Data != null && response.Data.Count > 0)
                {
                    yield return response.Data;
                }

                offset += limit;

                if (response.Meta == null || response.Meta.Total <= offset)
                {
                    yield break;
                }
            } while (offset <= 10000);
        }

        protected string GetUrl(string path, object filters = null, int limit = 0, int offset = 0)
        {
            var paramDictionary = filters?.ToObjectDictionary();

            if (paramDictionary == null || paramDictionary.Count <= 0)
            {
                return path;
            }

            var paramStringBuilder = new StringBuilder(paramDictionary.Count * 4);
            var isFirstParam = true;

            foreach (var queryParam in paramDictionary)
            {
                var paramValue = queryParam.Value?.ToString();

                if (string.IsNullOrEmpty(paramValue))
                {
                    continue;
                }

                paramStringBuilder.Append(isFirstParam
                                              ? "?"
                                              : "&");

                paramStringBuilder.Append(string.Concat("filters[", queryParam.Key, "]"));
                paramStringBuilder.Append("=");
                paramStringBuilder.Append(paramValue.UrlEncode());

                isFirstParam = false;
            }

            if (limit > 0)
            {
                paramStringBuilder.Append(isFirstParam
                                              ? "?"
                                              : "&");

                paramStringBuilder.Append("limit=");
                paramStringBuilder.Append(limit);
            }

            if (offset > 0)
            {
                paramStringBuilder.Append(isFirstParam
                                              ? "?"
                                              : "&");

                paramStringBuilder.Append("offset=");
                paramStringBuilder.Append(offset);
            }

            var url = string.Concat(path, paramStringBuilder.ToString());

            return url;
        }

        protected async Task<(string ToUrl, T Response)> GetAsyncInternal<T>(string toUrl)
        {
            var getResponse = await SendAsync(toUrl, HttpMethod.Get).ConfigureAwait(false);

            ConvertAndThrowAcApiException(getResponse.Exception);

            var response = getResponse.ResponseString.FromJson<T>();

            return (toUrl, response);
        }

        protected virtual async Task<T> PostAsync<T>(string path, object filters = null, T bodyContent = null)
            where T : class
        {
            var url = GetUrl(path, filters);

            var responseTuple = await PostAsyncInternal(url, bodyContent).ConfigureAwait(false);

            return responseTuple.Response;
        }

        protected async Task<(string ToUrl, T Response)> PostAsyncInternal<T>(string toUrl, T bodyContent = null)
            where T : class
        {
            var postResponse = await SendAsync(toUrl, HttpMethod.Post, bodyContent).ConfigureAwait(false);

            ConvertAndThrowAcApiException(postResponse.Exception);

            var response = postResponse.ResponseString.FromJson<T>();

            return (toUrl, response);
        }

        protected virtual async Task<T> PutAsync<T>(string path, object filters = null, T bodyContent = null)
            where T : class
        {
            var url = GetUrl(path, filters);

            var responseTuple = await PutAsyncInternal(url, bodyContent).ConfigureAwait(false);

            return responseTuple.Response;
        }

        protected async Task<(string ToUrl, T Response)> PutAsyncInternal<T>(string toUrl, T bodyContent = null)
            where T : class
        {
            var putResponse = await SendAsync(toUrl, HttpMethod.Put, bodyContent).ConfigureAwait(false);

            ConvertAndThrowAcApiException(putResponse.Exception);

            var response = putResponse.ResponseString.FromJson<T>();

            return (toUrl, response);
        }

        protected virtual async Task DeleteAsync(string path)
        {
            var url = GetUrl(path);

            await DeleteAsyncInternal(url).ConfigureAwait(false);
        }

        protected async Task DeleteAsyncInternal(string toUrl)
        {
            var delResponse = await SendAsync(toUrl, HttpMethod.Delete).ConfigureAwait(false);

            ConvertAndThrowAcApiException(delResponse.Exception);
        }

        private async Task<(AcApiException Exception, string ResponseString)> SendAsync(string toUrl, HttpMethod method, object bodyContent = null)
        {
            AcApiException acApiException = null;
            var responseString = string.Empty;

            using(var httpReqMsg = new HttpRequestMessage(method, new Uri(toUrl, UriKind.RelativeOrAbsolute)))
            {
                if (bodyContent != null)
                {
                    httpReqMsg.Content = new StringContent(bodyContent.ToJson(), Encoding.UTF8);
                }

                using(var httpResponse = await _httpClient.SendAsync(httpReqMsg, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    Exception contentException = null;

                    if (httpResponse.Content != null)
                    {
                        try
                        {
                            responseString = await httpResponse.Content.ReadAsStringAsync();
                        }
                        catch(Exception x)
                        {
                            contentException = x;
                        }
                    }

                    if (contentException != null || !httpResponse.IsSuccessStatusCode)
                    {
                        var acErrors = responseString?.FromJson<AcErrors>();
                        var genericMsg = $"Response status code does not indicate success: [{(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase})]";

                        acApiException = new AcApiException(AcApiException.ToErrorMessage(acErrors, genericMsg, null),
                                                            acErrors,
                                                            new HttpRequestException($"{genericMsg}. \nResponse: [{responseString.Left(500)}]", contentException),
                                                            toUrl);
                    }
                }
            }

            return (acApiException, responseString ?? string.Empty);
        }

        private void ConvertAndThrowAcApiException(AcApiException acx)
        {
            if (acx == null)
            {
                return;
            }

            throw acx;
        }
    }
}
