using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;
using ServiceStack;

namespace Rydr.FbSdk;

public partial class InstagramBasicClient
{
    public static async Task<IgAccount> GetAccountForToken(string accessToken)
    {
        var fields = GetFieldStringForType<IgAccount>();

        var url = string.Concat(_defaultBaseGraphApiUrl, "me")
                        .AddQueryParam("fields", fields)
                        .AddQueryParam("access_token", accessToken);

        using(var client = new HttpClient())
        {
            var igAccount = await client.GetAsAsync<IgAccount>(url).ConfigureAwait(false);

            return igAccount;
        }
    }

    public async Task<IgAccount> GetMyAccountAsync(bool honorEtag = true)
    {
        var igAccount = await GetAsync<IgAccount>("me",
                                                  new
                                                  {
                                                      fields = GetFieldStringForType<IgAccount>()
                                                  },
                                                  honorEtag);

        return igAccount;
    }
}
