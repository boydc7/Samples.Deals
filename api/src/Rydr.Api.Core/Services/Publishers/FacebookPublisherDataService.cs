using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Publishers;

public class FacebookPublisherDataService : BasePublisherDataService
{
    private readonly string _defaultFacebookAppId;
    private DynPublisherApp _defaultFacebookApp;

    public FacebookPublisherDataService(IPocoDynamo dynamoDb,
                                        IAuthorizationService authorizationService,
                                        IEncryptionService encryptionService,
                                        IRequestStateManager requestStateManager,
                                        IPublisherAccountService publisherAccountService)
        : base(dynamoDb, authorizationService, encryptionService, requestStateManager, publisherAccountService)
    {
        // ReSharper disable once NotResolvedInText
        _defaultFacebookAppId = RydrEnvironment.GetAppSetting("Facebook.DefaultAppId") ?? throw new ArgumentNullException("Facebook.DefaultAppId");
    }

    public override PublisherType PublisherType => PublisherType.Facebook;

    public override async Task<DynPublisherApp> GetDefaultPublisherAppAsync()
        => _defaultFacebookApp ??= await _dynamoDb.GetItemByEdgeIntoAsync<DynPublisherApp>(DynItemType.PublisherApp,
                                                                                           DynPublisherApp.BuildEdgeId(PublisherType.Facebook, _defaultFacebookAppId));

    public Task<IPublisherAccessToken> GetAccessTokenAsync(string appId, string appSecret, string redirectUrl, string code)
        => FacebookClient.GetAccessTokenAsync(appId, appSecret, redirectUrl, code)
                         .Then(fb => (IPublisherAccessToken)new BasicPublisherAccessToken
                                                            {
                                                                AccessToken = fb.AccessToken,
                                                                Expires = fb.Expires,
                                                                TokenType = fb.TokenType
                                                            });

    protected override async Task<List<PublisherMedia>> DoGetRecentMediaAsync(DynPublisherAccount forAccount, DynPublisherAppAccount withAppAccount, int limit = 50)
    {
        if (withAppAccount == null || withAppAccount.IsDeleted())
        {
            return new List<PublisherMedia>();
        }

        var client = await withAppAccount.GetOrCreateFbClientAsync();

        var recentPosts = await client.GetFbIgAccountMediaAsync(forAccount.AccountId)
                                      .TakeManyToListAsync(take: limit);

        var recentStories = await client.GetFbIgAccountStoriesAsync(forAccount.AccountId)
                                        .TakeManyToListAsync(take: limit);

        return (recentPosts ?? Enumerable.Empty<FbIgMedia>()).Select(fbm => fbm.ToPublisherMedia(PublisherContentType.Post, forAccount.PublisherAccountId))
                                                             .Concat((recentStories ?? Enumerable.Empty<FbIgMedia>()).Select(fbs => fbs.ToPublisherMedia(PublisherContentType.Story, forAccount.PublisherAccountId)))
                                                             .AsList();
    }

    protected override async Task<bool> ValidateAndDecorateAppAccountAsync(DynPublisherAppAccount appAccount, string rawAccessToken = null)
    {
        var client = await appAccount.GetOrCreateFbClientAsync(rawAccessToken);

        var fbDebug = await client.DebugTokenAsync();

#if LOCALDEBUG
        if (fbDebug?.Data == null ||
            !fbDebug.Data.AppId.EqualsOrdinal(client.AppId) ||
            !fbDebug.Data.IsValid ||
            fbDebug.IsExpired())
        {
            return true;
        }
#endif

        Guard.AgainstInvalidData(fbDebug?.Data == null ||
                                 !fbDebug.Data.AppId.EqualsOrdinal(client.AppId) ||
                                 !fbDebug.Data.IsValid ||
                                 fbDebug.IsExpired(),
                                 $"Fb client token invalid - code [{appAccount.PublisherAccountId}|{appAccount.PublisherAppId}]");

        if (fbDebug.Data.ExpiresAt > DateTimeHelper.MinApplicationDateTs)
        {
            var longLivedToken = await client.TryExchangeAccessTokenAsync(rawAccessToken);

            if (longLivedToken.HasValue())
            {
                fbDebug = await client.DebugTokenAsync();

                Guard.AgainstInvalidData(fbDebug?.Data == null ||
                                         !fbDebug.Data.AppId.EqualsOrdinal(client.AppId) ||
                                         !fbDebug.Data.IsValid ||
                                         fbDebug.IsExpired(),
                                         $"Fb client exchanged token invalid - code [{appAccount.PublisherAccountId}|{appAccount.PublisherAppId}]");

                appAccount.PubAccessToken = await _encryptionService.Encrypt64Async(longLivedToken);
            }
        }

        appAccount.ExpiresAt = fbDebug.Data.ExpiresAt > DateTimeHelper.MinApplicationDateTs
                                   ? fbDebug.Data.ExpiresAt
                                   : DateTimeHelper.MaxApplicationDateTs;

        appAccount.PubTokenType = fbDebug.Data.Type.ToString();
        appAccount.ForUserId = fbDebug.Data.UserId.ToStringInvariant();
        appAccount.PubAccessTokenScopes = fbDebug.Data.Scopes.AsHashSet(StringComparer.OrdinalIgnoreCase);

        await PublisherMediaSyncService.SyncUserDataAsync(new SyncPublisherAppAccountInfo(appAccount));

        return true;
    }
}
