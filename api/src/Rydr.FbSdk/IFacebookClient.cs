using Rydr.Api.Dto.Interfaces;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk;

public interface IFacebookClient : IDisposable
{
    string AppId { get; }

    // Auth
    Task<FbDebugToken> DebugTokenAsync(string appAccessToken = null);
    Task<FbUser> GetUserAsync(bool honorEtag = true);
    Task<string> TryExchangeAccessTokenAsync(string exchangeAccessToken = null);
    Task<bool> InstallAppOnFacebookPageAsync(string pageId);
    Task<FbValidateAccessResponse> ValidateAccessToFbIgAccountAsync(string fbIgAccountId, string byAppScopedUserId = null);

    // Accounts
    IAsyncEnumerable<List<FbAccount>> GetAccountsAsync(string forFbAccountId = null, int pageLimit = 50);

    // Businesses
    IAsyncEnumerable<List<FbBusiness>> GetBusinessesListAsync(string fbAccountId = null, int limit = 100);
    IAsyncEnumerable<List<FbBusinessUser>> GetBusinessUsersAsync(string fbBusinessAccountId, int limit = 100);
    IAsyncEnumerable<List<FbBusinessUser>> GetBusinessSystemUsersAsync(string fbBusinessAccountId, int limit = 100);
    Task<bool> InstallAppForBusinessUserAsync(string fbSystemUserId);
    Task<string> CreateBusinessSystemUserAsync(string fbBusinessAccountId, string name, bool isAdmin);
    Task<FbAccessToken> GenerateBusinessSystemUserAccessTokenAsync(string fbSystemUserId);
    Task<string> AssignPageToBusinessSystemUserAsync(string fbPageId, string fbSystemUserId);
    IAsyncEnumerable<List<FbBusinessPage>> GetBusinessOwnedPagesAsync(string fbBusinessAccountId);
    IAsyncEnumerable<List<FbBusinessPage>> GetBusinessClientPagesAsync(string fbBusinessAccountId);

    // Ig info
    Task<FbIgBusinessAccount> GetFbIgBusinessAccountAsync(string fbAccountId, bool honorEtag = true);
    IAsyncEnumerable<IEnumerable<FbAccount>> GetFbIgBusinessAccountsAsync(string fbAccountId, int pageLimit = 50);

    // Media
    Task<FbIgMedia> GetFbIgMediaAsync(string fbMediaId);
    IAsyncEnumerable<List<FbIgMedia>> GetFbIgAccountMediaAsync(string fbIgBusinessAccountId, int pageLimit = 50);
    IAsyncEnumerable<List<FbIgMedia>> GetFbIgAccountStoriesAsync(string fbIgBusinessAccountId, int pageLimit = 50);
    IAsyncEnumerable<List<FbIgMediaComment>> GetFbIgMediaCommentsAsync(string fbMediaId, int pageLimit = 50);
    IAsyncEnumerable<List<FbIgMediaInsight>> GetFbIgMediaInsightsAsync(string fbMediaId, string mediaType, bool isStory, int pageLimit = 50);
    IAsyncEnumerable<List<FbIgMediaInsight>> GetFbIgUserDailyInsightsAsync(string fbIgBusinessAccountId, int daysBack = 0, int pageLimit = 50);
    IAsyncEnumerable<List<FbComplexIgMediaInsight>> GetFbIgUserLifetimeInsightsAsync(string fbIgBusinessAccountId, int pageLimit = 50);

    // Search
    IAsyncEnumerable<List<FbPlaceInfo>> SearchPlacesAsync(string query, IGeoQuery geoQuery = null);
}
