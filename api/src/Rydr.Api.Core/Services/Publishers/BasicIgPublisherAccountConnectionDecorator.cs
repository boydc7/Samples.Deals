using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Publishers;

public class BasicIgPublisherAccountConnectionDecorator : IPublisherAccountConnectionDecorator
{
    private static readonly int _synIntervalMinutes = RydrEnvironment.GetAppSetting("PublisherAccount.SyncIntervalMinutes", 60);
    private static readonly ILog _log = LogManager.GetLogger("BasicIgPublisherAccountConnectionDecorator");

    private readonly IRydrDataService _rydrDataService;
    private IPublisherDataService _instagramPublisherDataService;

    public BasicIgPublisherAccountConnectionDecorator(IRydrDataService rydrDataService)
    {
        _rydrDataService = rydrDataService;
    }

    public async Task DecorateAsync(PublisherAccountConnectInfo publisherAccountConnectInfo)
    {
        if (publisherAccountConnectInfo.IncomingPublisherAccount.Type != PublisherType.Instagram ||
            publisherAccountConnectInfo.IncomingPublisherAccount.AccessToken.IsNullOrEmpty() ||
            publisherAccountConnectInfo.NewPublisherAccount.AccountId.StartsWithOrdinalCi("rydr_"))
        {
            return;
        }

        if (_instagramPublisherDataService == null)
        {
            _instagramPublisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(PublisherType.Instagram.ToString());
        }

        // Do we need to up-convert an incoming BasicIg account connection into an already existing, valid full-api account?
        // OR
        // Do we need to up-convert an existing soft-linked account connection into a basicIg account?
        // OR
        // Do we need to down-convert an existing but invalid/delinked full-api account into an incoming valid BasicIg account?

        // Does this profile have a corresponding writable facebook account that already exists? If so, we use that, though we might convert it from a full-fledged
        // Facebook account down to a basic Instagram account...
        var dynFbPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                             .TryGetPublisherAccountAsync(PublisherType.Facebook,
                                                                                          publisherAccountConnectInfo.IncomingPublisherAccount.AccountId);

        // If we have a matching fb account for this ig profile that is still valid (non-deleted, sync enabled), then that is the account
        // we use, period - we'll still link the token for the ig app below, but until/unless we lose "full" fb access to the given account,
        // we keep using the full-access token to sync with the API (that gives us stats, stories, profile info, etc.).
        // If we have a matching fb account for this ig profile that is NOT valid, we convert it to a basic ig profile...
        if (dynFbPublisherAccount != null && !dynFbPublisherAccount.AccountType.IsUserAccount())
        { // Existing account becomes the fb account no matter what
            publisherAccountConnectInfo.ExistingPublisherAccount = dynFbPublisherAccount;

            var syncTimeThreshold = DateTimeHelper.UtcNow.AddMinutes(-(_synIntervalMinutes * 3000)).Date.ToUnixTimestamp();

            var hasValidTokenLinkage = !dynFbPublisherAccount.IsDeleted() && !dynFbPublisherAccount.IsSoftLinked && !dynFbPublisherAccount.IsSyncDisabled &&
                                       await PublisherExtensions.DefaultPublisherAccountService
                                                                .GetLinkedPublisherAccountsAsync(dynFbPublisherAccount.PublisherAccountId)
                                                                .AnyAsync(p => p != null && !p.IsDeleted() && p.IsTokenAccount());

            if (!hasValidTokenLinkage ||
                (dynFbPublisherAccount.LastMediaSyncedOn > 0 && dynFbPublisherAccount.LastMediaSyncedOn <= syncTimeThreshold &&
                 dynFbPublisherAccount.LastProfileSyncedOn > 0 && dynFbPublisherAccount.LastProfileSyncedOn <= syncTimeThreshold))
            { // Invalid full-link status, DOWN-CONVERT THE existing FB to a basic IG account
                // Move identifiers from the existing one to the new one
                publisherAccountConnectInfo.ConvertExisting = true;

                _log.Info($"  Incoming BasicIg connect request will down-convert existing invalid full-linked PublisherAccount [{dynFbPublisherAccount.DisplayName()}] to basic status");
            }
            else
            { // Valid existing FB full account - remove the token from the incoming publisher (as it is a token for basic IG api access, and we have full api access,
                // so yea, don't want it)...after we save it regardless
                // NOTE: THIS IS NOT A CONVERTEXISTING=true...existing remains, new does not get created basically...it's the existing one
                publisherAccountConnectInfo.NewPublisherAccount = dynFbPublisherAccount;

                await _instagramPublisherDataService.PutAccessTokenAsync(dynFbPublisherAccount.PublisherAccountId,
                                                                         publisherAccountConnectInfo.IncomingPublisherAccount.AccessToken,
                                                                         publisherAccountConnectInfo.IncomingPublisherAccount.AccessTokenExpiresIn);

                publisherAccountConnectInfo.IncomingPublisherAccount.AccessToken = null;

                _log.Info($"  Incoming BasicIg connect request will be up-converted into existing valid full-linked PublisherAccount [{dynFbPublisherAccount.DisplayName()}] to basic status");
            }

            // Update anything that applies to the new version whatever it is...
            publisherAccountConnectInfo.NewPublisherAccount.UserName = publisherAccountConnectInfo.IncomingPublisherAccount.UserName;

            if (!publisherAccountConnectInfo.IncomingPublisherAccount.Metrics.IsNullOrEmptyRydr() &&
                publisherAccountConnectInfo.IncomingPublisherAccount.Metrics.ContainsKey(PublisherMetricName.Media))
            {
                if (publisherAccountConnectInfo.NewPublisherAccount.Metrics.IsNullOrEmptyRydr())
                {
                    publisherAccountConnectInfo.NewPublisherAccount.Metrics = new Dictionary<string, double>();
                }

                publisherAccountConnectInfo.NewPublisherAccount.Metrics[PublisherMetricName.Media] = publisherAccountConnectInfo.IncomingPublisherAccount.Metrics[PublisherMetricName.Media];
            }
        }
        else
        { // No matching fb profile, but is there an existing soft-linked rydr account connection that we'll up-convert into a basic ig account
            var existingRydrSoftPubAcctIds = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<Int64Id>(@"
SELECT  pa.Id AS Id
FROM    PublisherAccounts pa
WHERE   pa.UserName = @UserName
        AND pa.PublisherType IN (1,2)
        AND pa.AccountType = @AccountType
        AND pa.RydrAccountType IN(1,2)
        AND pa.AccountId LIKE 'rydr_%'
        AND pa.DeletedOn IS NULL
LIMIT   1;
",
                                                                                                                 new
                                                                                                                 {
                                                                                                                     publisherAccountConnectInfo.IncomingPublisherAccount.UserName,
                                                                                                                     AccountType = PublisherAccountType.FbIgUser
                                                                                                                 }));

            var existingRydrSoftPubAcctId = existingRydrSoftPubAcctIds?.FirstOrDefault()?.Id ?? 0;

            publisherAccountConnectInfo.ExistingPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                                            .GetPublisherAccountAsync(existingRydrSoftPubAcctId);

            // If we have a match here, that's an existing soft-linked rydr account that should at this point get up-converted to a basic linked account
            if (publisherAccountConnectInfo.ExistingPublisherAccount != null)
            {
                publisherAccountConnectInfo.ConvertExisting = true;

                _log.Info($"  Incoming BasicIg connect request will up-convert existing soft-linked PublisherAccount [{publisherAccountConnectInfo.ExistingPublisherAccount.DisplayName()}] to basic status");
            }
        }

        if (publisherAccountConnectInfo.ConvertExisting)
        {
            publisherAccountConnectInfo.NewPublisherAccount.PublisherAccountId = publisherAccountConnectInfo.ExistingPublisherAccount.PublisherAccountId;
            publisherAccountConnectInfo.NewPublisherAccount.AlternateAccountId = publisherAccountConnectInfo.ExistingPublisherAccount.AlternateAccountId;
            publisherAccountConnectInfo.NewPublisherAccount.AccountId = publisherAccountConnectInfo.IncomingPublisherAccount.AccountId;
            publisherAccountConnectInfo.NewPublisherAccount.PublisherType = PublisherType.Instagram;
            publisherAccountConnectInfo.NewPublisherAccount.EdgeId = publisherAccountConnectInfo.NewPublisherAccount.GetEdgeId();
            publisherAccountConnectInfo.NewPublisherAccount.RydrAccountType = publisherAccountConnectInfo.ExistingPublisherAccount.RydrAccountType;
            publisherAccountConnectInfo.NewPublisherAccount.PageId = publisherAccountConnectInfo.ExistingPublisherAccount.PageId;
            publisherAccountConnectInfo.NewPublisherAccount.FullName = publisherAccountConnectInfo.ExistingPublisherAccount.FullName;
            publisherAccountConnectInfo.NewPublisherAccount.Email = publisherAccountConnectInfo.ExistingPublisherAccount.Email;
            publisherAccountConnectInfo.NewPublisherAccount.Description = publisherAccountConnectInfo.ExistingPublisherAccount.Description;
            publisherAccountConnectInfo.NewPublisherAccount.Metrics = publisherAccountConnectInfo.ExistingPublisherAccount.Metrics;
            publisherAccountConnectInfo.NewPublisherAccount.PrimaryPlaceId = publisherAccountConnectInfo.ExistingPublisherAccount.PrimaryPlaceId;
            publisherAccountConnectInfo.NewPublisherAccount.ProfilePicture = publisherAccountConnectInfo.ExistingPublisherAccount.ProfilePicture;
            publisherAccountConnectInfo.NewPublisherAccount.Website = publisherAccountConnectInfo.ExistingPublisherAccount.Website;
            publisherAccountConnectInfo.NewPublisherAccount.AgeRangeMin = publisherAccountConnectInfo.ExistingPublisherAccount.AgeRangeMin;
            publisherAccountConnectInfo.NewPublisherAccount.AgeRangeMax = publisherAccountConnectInfo.ExistingPublisherAccount.AgeRangeMax;
            publisherAccountConnectInfo.NewPublisherAccount.Gender = publisherAccountConnectInfo.ExistingPublisherAccount.Gender;
        }
    }
}
