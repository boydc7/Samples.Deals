using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk;

public partial class FacebookClient
{
    public async IAsyncEnumerable<List<FbBusiness>> GetBusinessesListAsync(string fbAccountId = null, int limit = 100)
    {
        var url = $"{(string.IsNullOrEmpty(fbAccountId) ? "me" : fbAccountId)}/businesses";

        await foreach (var fbBizBatch in GetPagedAsync<FbBusiness>(url, new
                                                                        {
                                                                            fields = GetFieldStringForType<FbBusiness>(),
                                                                            limit
                                                                        }))
        {
            yield return fbBizBatch;
        }
    }

    public async IAsyncEnumerable<List<FbBusinessPage>> GetBusinessClientPagesAsync(string fbBusinessAccountId)
    {
        var param = new
                    {
                        fields = GetFieldStringForType<FbBusinessPage>()
                    };

        var url = $"{fbBusinessAccountId}/client_pages";

        await foreach (var fbBusinessPage in GetPagedAsync<FbBusinessPage>(url, param))
        {
            yield return fbBusinessPage;
        }
    }

    public async IAsyncEnumerable<List<FbBusinessPage>> GetBusinessOwnedPagesAsync(string fbBusinessAccountId)
    {
        var param = new
                    {
                        fields = GetFieldStringForType<FbBusinessPage>()
                    };

        var url = $"{fbBusinessAccountId}/owned_pages";

        await foreach (var fbBusinessPage in GetPagedAsync<FbBusinessPage>(url, param))
        {
            yield return fbBusinessPage;
        }
    }

    public IAsyncEnumerable<List<FbBusinessUser>> GetBusinessUsersAsync(string fbBusinessAccountId, int limit = 100)
        => DoGetBusinessUsersAsync(fbBusinessAccountId, "business_users", limit);

    public IAsyncEnumerable<List<FbBusinessUser>> GetBusinessSystemUsersAsync(string fbBusinessAccountId, int limit = 100)
        => DoGetBusinessUsersAsync(fbBusinessAccountId, "system_users", limit);

    public async Task<bool> InstallAppForBusinessUserAsync(string fbSystemUserId)
    {
        var response = await PostAsync<FbBoolResponse>($"{fbSystemUserId}/applications",
                                                       new
                                                       {
                                                           business_app = AppId,
                                                           access_token = _accessToken
                                                       });

        return response?.Success ?? false;
    }

    public async Task<string> CreateBusinessSystemUserAsync(string fbBusinessAccountId, string name, bool isAdmin)
    {
        var response = await PostAsync<FbId>($"{fbBusinessAccountId}/system_users",
                                             new
                                             {
                                                 name,
                                                 role = isAdmin
                                                            ? "ADMIN"
                                                            : "EMPLOYEE",
                                                 access_token = _accessToken
                                             });

        return response.Id;
    }

    public async Task<FbAccessToken> GenerateBusinessSystemUserAccessTokenAsync(string fbSystemUserId)
    {
        var response = await PostAsync<FbAccessToken>($"{fbSystemUserId}/access_tokens",
                                                      new
                                                      {
                                                          business_app = AppId,
                                                          scope = "business_management", // FacebookAccessToken.RydrAppScopesString,
                                                          appsecret_proof = _appSecretProof,
                                                          access_token = _accessToken
                                                      });

        return response;
    }

    public async Task<string> AssignPageToBusinessSystemUserAsync(string fbPageId, string fbSystemUserId)
    {
        var response = await PostAsync<FbId>($"{fbPageId}/assigned_users",
                                             new
                                             {
                                                 user = fbSystemUserId,
                                                 tasks = "['MANAGE','ANALYZE']",
                                                 access_token = _accessToken
                                             });

        return response.Id;
    }

    protected async IAsyncEnumerable<List<FbBusinessUser>> DoGetBusinessUsersAsync(string fbBusinessAccountId, string firstUserUrlSegment, int limit = 100)
    {
        var fields = _fieldNameMap.GetOrAdd(typeof(FbBusinessUser),
                                            t =>
                                            {
                                                var fieldsForIg = GetFieldStringForType<FbBusinessUser>();

                                                var fbaFields = string.Join(",", typeof(FbBusinessPage).GetAllDataMemberNames());

                                                var fieldsToUse = fbaFields.Replace("assigned_pages",
                                                                                    string.Concat("assigned_pages.limit(2500){", fieldsForIg, "}"));

                                                return fieldsToUse;
                                            });

        await foreach (var fbBizUserBatch in GetPagedAsync<FbBusinessUser>($"{fbBusinessAccountId}/{firstUserUrlSegment}",
                                                                           new
                                                                           {
                                                                               fields,
                                                                               limit
                                                                           }))
        {
            yield return fbBizUserBatch;
        }
    }
}
