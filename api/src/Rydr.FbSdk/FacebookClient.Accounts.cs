using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk;

public partial class FacebookClient
{
    public async IAsyncEnumerable<List<FbAccount>> GetAccountsAsync(string forFbAccountId = null, int pageLimit = 50)
    {
        var fields = _fieldNameMap.GetOrAdd(typeof(FbAccount),
                                            t =>
                                            {
                                                var fieldsForIg = GetFieldStringForType<FbIgBusinessAccount>();

                                                var fbaFields = string.Join(",", typeof(FbAccount).GetAllDataMemberNames());

                                                var fieldsToUse = fbaFields.Replace("instagram_business_account",
                                                                                    string.Concat("instagram_business_account{", fieldsForIg, "}"));

                                                return fieldsToUse;
                                            });

        var url = $"{(string.IsNullOrEmpty(forFbAccountId) ? "me" : forFbAccountId)}/accounts";

        var param = new
                    {
                        fields,
                        limit = pageLimit
                    };

        // DO NOT use etags here, as we only go get accounts anytime we're looking for things to compare/return to users/etc.
        await foreach (var accounts in GetPagedAsync<FbAccount>(url, param).ConfigureAwait(false))
        {
            yield return accounts;
        }
    }
}
