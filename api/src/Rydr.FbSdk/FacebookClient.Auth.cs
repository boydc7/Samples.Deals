using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Text;

namespace Rydr.FbSdk
{
    public partial class FacebookClient
    {
        public static async Task<FbAccessToken> GetAccessTokenAsync(string appId, string appSecret, string redirectUrl, string code)
        {
            var url = DecorateUrlWithIdentifiers(string.Concat(_defaultBaseGraphApiUrl, "oauth/access_token")
                                                       .AddQueryParam("code", code),
                                                 appId, appSecret, redirectUrl: redirectUrl);

            using(var client = new HttpClient())
            {
                _log.DebugFormat("Facebook GetAccessTokenAsync URL: [{0}]", url);

                var fbAccessToken = await client.GetAsAsync<FbAccessToken>(url).ConfigureAwait(false);

                return fbAccessToken;
            }
        }

        public async Task<string> TryExchangeAccessTokenAsync(string exchangeAccessToken = null)
        {
            var url = DecorateUrlWithIdentifiers(string.Concat(_defaultBaseGraphApiUrl, "oauth/access_token")
                                                       .AddQueryParam("grant_type", "fb_exchange_token")
                                                       .AddQueryParam("fb_exchange_token", exchangeAccessToken ?? _accessToken),
                                                 AppId, _appSecret);

            using(var client = new HttpClient())
            {
                _log.DebugFormat("Facebook ExchangeAccessTokenAsync URL: [{0}]", url);

                try
                {
                    var fbAccessToken = await client.GetAsAsync<FbAccessToken>(url).ConfigureAwait(false);

                    if (!(fbAccessToken?.AccessToken).IsNullOrEmpty())
                    {
                        UpdateAccessToken(fbAccessToken.AccessToken);

                        return _accessToken;
                    }
                }
                catch(Exception x) when(LogIgnoreException(x))
                {
                    throw;
                }
            }

            return null;
        }

        private bool LogIgnoreException(Exception x)
        {
            _log.Warn("Error logged ignored", x);

            return false;
        }

        public static string GetAuthenticateUrl(string appId, string appSecret, string redirectUrl, string scopes = null)
            => DecorateUrlWithIdentifiers(_baseAuthDialogUrl, appId, null, scopes, redirectUrl);

        public async Task<FbValidateAccessResponse> ValidateAccessToFbIgAccountAsync(string fbIgAccountId, string byAppScopedUserId = null)
        {
            var response = new FbValidateAccessResponse();

            void processFbApiException(FbApiException fbx)
            {
                response.RequiresReAuthentication = fbx.RequiresOAuthRefresh;
                response.IsTransientError = fbx.IsTransient && !response.RequiresReAuthentication;
            }

            try
            { // Try to get the fbIg account info directly...
                var fbIgBusinessAccount = await GetFbIgBusinessAccountAsync(fbIgAccountId).ConfigureAwait(false);

                // Null is ok, as that's an etag http not modified - if we get a successful response from this, we're done
                response.Unauthorized = fbIgBusinessAccount != null && !string.Equals(fbIgBusinessAccount.Id, fbIgAccountId,
                                                                                      StringComparison.OrdinalIgnoreCase);

                return response;
            }
            catch(FbApiException fbx)
            {
                processFbApiException(fbx);
            }

            // If we get to here, weren't able to get the account requested directly
            if (response.IsTransientError || response.RequiresReAuthentication)
            { // Transient or complete oauth/auth required problem, nothing else we can do
                return response;
            }

            // Non transient, non re-auth required exception - see if we can determine the pages this token has access to
            var userId = byAppScopedUserId;

            // If no app-scoped userId was passed to use, try and get it from the token
            if (userId.IsNullOrEmpty())
            {
                try
                {
                    var debugToken = await DebugTokenAsync().ConfigureAwait(false);

                    // Should get a valid response...
                    if (debugToken?.Data == null)
                    {
                        response.IsTransientError = true;
                    }
                    else
                    {
                        userId = debugToken.Data.UserId.ToString(CultureInfo.InvariantCulture);

                        response.RequiresReAuthentication = !debugToken.Data.IsValid ||
                                                            (debugToken.Data.ExpiresAt > 0 &&
                                                             debugToken.Data.ExpiresAt <= DateTime.UtcNow.ToUnixTime());
                    }
                }
                catch(FbApiException fbx)
                {
                    processFbApiException(fbx);
                }
            }

            // Weren't able to get or do not have a userId
            if (response.IsTransientError || response.RequiresReAuthentication || userId.IsNullOrEmpty())
            {
                return response;
            }

            // See if this user still has this account in their available fbIgBizAccounts list...
            try
            {
                await foreach (var fbIgBusinessBatchEnumerable in GetFbIgBusinessAccountsAsync(userId).ConfigureAwait(false))
                {
                    if (fbIgBusinessBatchEnumerable != null &&
                        fbIgBusinessBatchEnumerable.Any(fba => fba.Id.Equals(fbIgAccountId) ||
                                                               fba.InstagramBusinessAccount.Id.Equals(fbIgAccountId)))
                    { // Found it, have accesss...
                        return response;
                    }
                }

                // If we get all the way here successfully without finding anything, need to delink
                response.Unauthorized = true;
            }
            catch(FbApiException fbx)
            {
                processFbApiException(fbx);
            }

            return response;
        }

        public async Task<FbUser> GetUserAsync(bool honorEtag = true)
        {
            var user = await GetAsync<FbUser>("me",
                                              new
                                              {
                                                  fields = string.Join(",", typeof(FbUser).GetAllDataMemberNames()
                                                                                          .Where(n => !n.Equals("id", StringComparison.OrdinalIgnoreCase)))
                                              },
                                              honorEtag);

            return user;
        }
    }
}
