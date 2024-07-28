using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Rydr.FbSdk.Configuration;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Caching;

namespace Rydr.FbSdk;

public abstract class BaseFacebookClient
{
    protected static readonly ConcurrentDictionary<Type, string> _fieldNameMap = new();

    protected readonly string _appSecret;

    private readonly ICacheClient _cacheClient;
    private readonly HttpClient _httpClient;

    protected string _accessToken;

    // ReSharper disable once NotAccessedField.Local
    protected string _appSecretProof;

    protected BaseFacebookClient(string baseGraphUrl, string appId, string appSecret, string accessToken)
    {
        AppId = appId ?? throw new ArgumentNullException(nameof(appId));
        _appSecret = appSecret ?? throw new ArgumentNullException(nameof(appSecret));

        var handler = new HttpClientHandler
                      {
                          AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                      };

        _httpClient = new HttpClient(handler)
                      {
                          BaseAddress = new Uri(baseGraphUrl)
                      };

        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RydrNETFbSdk", "1.0"));

        UpdateAccessToken(accessToken);

        _cacheClient = FacebookSdkConfig.CacheClientFactory?.Invoke();
    }

    public string AppId { get; }

    public async Task<FbDebugToken> DebugTokenAsync(string withAppAccessToken = null)
    {
        if (withAppAccessToken.IsNullOrEmpty())
        {
            withAppAccessToken = string.Concat(AppId, "|", _appSecret);
        }

        var fbDebugToken = await GetAsync<FbDebugToken>("debug_token", new
                                                                       {
                                                                           input_token = _accessToken,
                                                                           access_token = withAppAccessToken
                                                                       }).ConfigureAwait(false);

        return fbDebugToken;
    }

    protected void UpdateAccessToken(string accessToken)
    {
        if (accessToken.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(accessToken));
        }

        _accessToken = accessToken;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        _appSecretProof = _accessToken.IsNullOrEmpty() || _appSecret.IsNullOrEmpty()
                              ? null
                              : ByteExtensions.GenerateFacebookSecretProof(_accessToken, _appSecret);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    protected virtual async Task<T> PostAsync<T>(string path, object parameters = null, string withSecretProof = null)
    {
        var url = GetUrl(path, parameters, withSecretProof);

        var responseTuple = await PostAsyncInternal<T>(url).ConfigureAwait(false);

        return responseTuple.Response;
    }

    protected virtual async Task<T> GetAsync<T>(string path, object parameters = null, bool eTag = false)
    {
        var url = GetUrl(path, parameters);

        var responseTuple = await GetAsyncInternal<T>(url, eTag).ConfigureAwait(false);

        return responseTuple.Response;
    }

    protected virtual async IAsyncEnumerable<List<T>> GetPagedAsync<T>(string initialPath, object parameters = null, bool eTag = false)
    {
        var path = initialPath;

        do
        {
            var response = await GetAsync<FbPagedResult<T>>(path, parameters, eTag).ConfigureAwait(false);

            if (response == null)
            {
                yield break;
            }

            if (response.Data != null && response.Data.Count > 0)
            {
                yield return response.Data;
            }

            if (response.Paging == null)
            {
                yield break;
            }

            path = response.Paging.Next;
        } while (!string.IsNullOrEmpty(path));
    }

    protected virtual IEnumerable<T> GetPaged<T>(string initialPath, object parameters = null)
    {
        var path = initialPath;

        do
        {
            var response = GetAsync<FbPagedResult<T>>(path, parameters).GetAwaiter().GetResult();

            if (response == null)
            {
                yield break;
            }

            if (response.Data != null && response.Data.Count > 0)
            {
                foreach (var entity in response.Data)
                {
                    yield return entity;
                }
            }

            if (response.Paging == null)
            {
                yield break;
            }

            path = response.Paging.Next;
        } while (!string.IsNullOrEmpty(path));
    }

    protected string GetUrl(string path, object parameters = null, string withSecretProof = null)
    {
        var url = DecorateUrlWithIdentifiers(path, withSecretProof);

        var paramDictionary = parameters?.ToObjectDictionary();

        if (paramDictionary == null || paramDictionary.Count <= 0)
        {
            return url;
        }

        var paramStringBuilder = new StringBuilder(paramDictionary.Count * 4);

        foreach (var queryParam in paramDictionary)
        {
            var paramValue = queryParam.Value?.ToString();

            if (string.IsNullOrEmpty(paramValue))
            {
                continue;
            }

            paramStringBuilder.Append("&");
            paramStringBuilder.Append(queryParam.Key);
            paramStringBuilder.Append("=");
            paramStringBuilder.Append(paramValue.UrlEncode());
        }

        url = string.Concat(url, paramStringBuilder.ToString());

        return url;
    }

    protected async Task<(string ToUrl, T Response)> PostAsyncInternal<T>(string toUrl)
    {
        var postResponse = await SendAsync(toUrl, HttpMethod.Post).ConfigureAwait(false);

        ConvertAndThrowFbApiException(postResponse.Exception);

        var response = postResponse.ResponseString.FromJson<T>();

        return (toUrl, response);
    }

    protected async Task<(string ToUrl, T Response)> GetAsyncInternal<T>(string toUrl, bool eTag = false)
    {
        var getResponse = await SendAsync(toUrl, HttpMethod.Get, eTag).ConfigureAwait(false);

        ConvertAndThrowFbApiException(getResponse.Exception);

        var response = getResponse.ResponseString.FromJson<T>();

        return (toUrl, response);
    }

    private async Task<(FbApiException Exception, string ResponseString)> SendAsync(string toUrl, HttpMethod method, bool eTag = false)
    {
        FbApiException fbApiException = null;
        var responseString = string.Empty;
        string responseETag = null;

        eTag = eTag && method == HttpMethod.Get && !FacebookSdkConfig.ETagDisabled;

        var lastEtagKey = eTag && _cacheClient != null
                              ? string.Concat("FbEtag|", toUrl.ToShaBase64())
                              : null;

        var lastEtag = !string.IsNullOrEmpty(lastEtagKey)
                           ? _cacheClient.Get<string>(lastEtagKey)
                           : null;

        using(var httpReqMsg = new HttpRequestMessage(method, new Uri(toUrl, UriKind.RelativeOrAbsolute)))
        {
            if (!string.IsNullOrEmpty(lastEtag))
            {
                httpReqMsg.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(lastEtag));
            }

            using(var httpResponse = await _httpClient.SendAsync(httpReqMsg, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                Exception contentException = null;

                if (httpResponse.StatusCode != HttpStatusCode.NotModified &&
                    httpResponse.Content != null)
                {
                    try
                    {
                        responseString = await httpResponse.Content.ReadAsStringAsync();
                    }
                    catch(Exception x)
                    {
                        contentException = x;
                    }

                    responseETag = httpResponse.Headers.ETag?.Tag;
                }

                if (contentException != null ||
                    (httpResponse.StatusCode != HttpStatusCode.NotModified &&
                     !httpResponse.IsSuccessStatusCode))
                {
                    var fbError = responseString?.FromJson<FbErrorResponse>();
                    var genericMsg = $"Response status code does not indicate success: [{(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase})]";

                    fbApiException = new FbApiException(fbError?.Error?.Message ?? genericMsg,
                                                        fbError?.Error,
                                                        new HttpRequestException($"{genericMsg}. \nResponse: [{responseString.Left(500)}]", contentException),
                                                        toUrl);
                }
            }
        }

        if (eTag && _cacheClient != null && !string.IsNullOrEmpty(responseETag))
        {
            _cacheClient.Set(lastEtagKey, responseETag, TimeSpan.FromHours(50));
        }

        return (fbApiException, responseString ?? string.Empty);
    }

    protected static string GetFieldStringForType<T>() => GetFieldStringForType(typeof(T));

    protected static string GetFieldStringForType(Type type)
        => _fieldNameMap.GetOrAdd(type, t => string.Join(",", t.GetAllDataMemberNames()));

    private void ConvertAndThrowFbApiException(FbApiException fbx)
    {
        if (fbx == null)
        {
            return;
        }

        // Media Posted Before Business Account Conversion
        if (fbx.FbError?.ErrorUserTitle != null &&
            fbx.FbError.ErrorUserTitle.IndexOf("Posted Before Business Account Conversion", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            throw new FbMediaCreatedBeforeBusinessConversion(fbx);
        }

        throw fbx;
    }

    protected string DecorateUrlWithIdentifiers(string urlToDecorate, string withSecretProof = null)
        => DecorateUrlWithIdentifiers(urlToDecorate, AppId, _appSecret, appSecretProof: withSecretProof ?? _appSecretProof);

    public static string DecorateUrlWithIdentifiers(string urlToDecorate, string appId, string appSecret,
                                                    string scopes = null, string redirectUrl = null,
                                                    string accessToken = null, string appSecretProof = null)
    {
        var url = urlToDecorate.AddQueryParam("client_id", appId)
                               .AddQueryParam("return_ssl_resources", true);

        if (!appSecretProof.IsNullOrEmpty())
        {
            url = url.AddQueryParam("appsecret_proof", appSecretProof);
        }

        if (!string.IsNullOrEmpty(appSecret))
        {
            url = url.AddQueryParam("client_secret", appSecret);
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            url = url.AddQueryParam("access_token", accessToken);
        }

        if (!string.IsNullOrEmpty(scopes))
        {
            url = url.AddQueryParam("scope", scopes);
        }

        if (!string.IsNullOrEmpty(redirectUrl))
        {
            url = url.AddQueryParam("redirect_uri", redirectUrl);
        }

        return url;
    }
}
