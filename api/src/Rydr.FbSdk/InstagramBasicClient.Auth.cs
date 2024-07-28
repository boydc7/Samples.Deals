using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;
using ServiceStack;

namespace Rydr.FbSdk;

public partial class InstagramBasicClient
{
    public static string BaseAuthDialogUrl { get; }

    public static async Task<IgAccessTokenResponse> GetAccessTokenAsync(string appId, string appSecret, string redirectUrl, string code)
    {
        using(var client = new HttpClient())
        using(var httpReqMsg = new HttpRequestMessage(HttpMethod.Post, new Uri(string.Concat(BaseAuthDialogUrl, "oauth/access_token"), UriKind.Absolute)))
        {
            httpReqMsg.Content = new FormUrlEncodedContent(new[]
                                                           {
                                                               new KeyValuePair<string, string>("client_id", appId), new KeyValuePair<string, string>("client_secret", appSecret), new KeyValuePair<string, string>("code", code), new KeyValuePair<string, string>("grant_type", "authorization_code"), new KeyValuePair<string, string>("redirect_uri", redirectUrl)
                                                           });

            using(var httpResponse = await client.SendAsync(httpReqMsg, HttpCompletionOption.ResponseHeadersRead)
                                                 .ConfigureAwait(false))
            {
                var responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                var igAccessToken = responseContent.FromJson<IgAccessTokenResponse>();

                return igAccessToken;
            }
        }
    }

    public static async Task<IgLongLivedAccessTokenResponse> GetLongLivedAccessTokenAsync(string appSecret, string shortToken)
    {
        var url = string.Concat(_defaultBaseGraphApiUrl, "access_token")
                        .AddQueryParam("grant_type", "ig_exchange_token")
                        .AddQueryParam("client_secret", appSecret)
                        .AddQueryParam("access_token", shortToken);

        using(var client = new HttpClient())
        {
            var longLivedToken = await client.GetAsAsync<IgLongLivedAccessTokenResponse>(url).ConfigureAwait(false);

            return longLivedToken;
        }
    }

    public static async Task<IgLongLivedAccessTokenResponse> RefreshLongLivedAccessTokenAsync(string existingLongLivedToken)
    {
        var url = string.Concat(_defaultBaseGraphApiUrl, "refresh_access_token")
                        .AddQueryParam("grant_type", "ig_refresh_token")
                        .AddQueryParam("access_token", existingLongLivedToken);

        using(var client = new HttpClient())
        {
            var longLivedToken = await client.GetAsAsync<IgLongLivedAccessTokenResponse>(url).ConfigureAwait(false);

            return longLivedToken;
        }
    }

    public async Task<IgLongLivedAccessTokenResponse> RefreshLongLivedAccessTokenAsync()
    {
        var refreshedToken = await GetAsync<IgLongLivedAccessTokenResponse>("refresh_access_token",
                                                                            new
                                                                            {
                                                                                grant_type = "ig_refresh_token",
                                                                                access_token = _accessToken
                                                                            });

        if (refreshedToken != null && refreshedToken.IsValid())
        {
            UpdateAccessToken(refreshedToken.AccessToken);
        }

        return refreshedToken;
    }
}
