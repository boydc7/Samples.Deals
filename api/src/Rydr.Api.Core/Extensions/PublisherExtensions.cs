using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Extensions;

public static class PublisherExtensions
{
    public static readonly HashSet<string> RydrSystemPublisherAccountIds = new(StringComparer.Ordinal)
                                                                           {
                                                                               "101852684618114" // rydrFbBizMgr System User
                                                                           };

    private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();
    private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();

    public static readonly IPublisherAccountService DefaultPublisherAccountService = RydrEnvironment.Container.Resolve<IPublisherAccountService>();

    public static bool IsRydrSystemPublisherAccount(this DynPublisherAccount publisherAccount)
        => publisherAccount != null && RydrSystemPublisherAccountIds.Contains(publisherAccount.AccountId);

    public static bool IsRydrSoftLinkedAccount(this DynPublisherAccount publisherAccount)
        => publisherAccount != null && publisherAccount.IsSoftLinked;

    public static string ToRydrSoftLinkedAssociationId(this DynPublisherAccount publisherAccount)
        => string.Concat("rydr_softlinkassociation_", publisherAccount.PublisherAccountId.ToStringInvariant());

    public static async Task<DynPublisherAccount> TryGetAnyExistingPublisherAccountAsync(this IPublisherAccountService service, PublisherType publisherType, string publisherId)
    {
        // Proper match?
        var matchingAccount = await service.TryGetPublisherAccountAsync(publisherType, publisherId);

        if (matchingAccount != null)
        {
            return matchingAccount;
        }

        // No proper match, handle differently depending on if the publisherType requested is writable or not...
        var nonWritableType = publisherType.NonWritableAlternateAccountType();

        if (nonWritableType == PublisherType.Unknown)
        { // Primary is non-writable, get the writable alternative(s)...
            var writableType = publisherType.WritableAlternateAccountType();

            if (writableType != PublisherType.Unknown)
            { // Full match on a proper writable alternative
                // OR
                // Match to a soft-linked writable alternative
                matchingAccount = await service.TryGetPublisherAccountAsync(writableType, publisherId)
                                  ??
                                  await service.TryGetPublisherAccountAsync(writableType, PublisherTransforms.ToRydrSoftLinkedAccountId(publisherType, publisherId));
            }
        }
        else
        { // Primary is writable, look for non-writable alternatives....
            // Basic linked to non-writable alternative (i.e. instagram to facebook...
            // OR
            // Soft linked
            matchingAccount = await service.TryGetPublisherAccountAsync(nonWritableType, publisherId)
                              ??
                              await service.TryGetPublisherAccountAsync(publisherType, PublisherTransforms.ToRydrSoftLinkedAccountId(nonWritableType, publisherId));
        }

        return matchingAccount;
    }

    public static long ToPublisherAccountId<T>(this T source)
        where T : IHasPublisherAccountId, IHasUserAuthorizationInfo
        => source.PublisherAccountId.Gz(source.RequestPublisherAccountId);

    public static bool IsTokenAccount(this DynPublisherAccount publisherAccount)
        => publisherAccount != null && publisherAccount.AccountType.IsUserAccount() && publisherAccount.RydrAccountType.HasFlag(RydrAccountType.TokenAccount);

    public static bool IsSystemAccount(this DynPublisherAccount publisherAccount)
        => publisherAccount != null && (publisherAccount.AccountType.IsSystemAccount() || publisherAccount.RydrAccountType.HasFlag(RydrAccountType.Admin));

    public static Task<DynPublisherApp> GetPublisherAppAsync(this IPocoDynamo dynamoDb, long publisherAppId, bool includeDeleted = false)
        => dynamoDb.GetItemByRefAsync<DynPublisherApp>(publisherAppId, DynItemType.PublisherApp, includeDeleted);

    public static Task<DynPublisherAppAccount> TryGetPublisherAppAccountAsync(this IPocoDynamo dynamoDb, long publisherAccountId, long publisherAppId)
        => GetPublisherAppAccountAsync(dynamoDb, publisherAccountId, publisherAppId, true, true);

    public static async Task<DynPublisherAppAccount> GetPublisherAppAccountAsync(this IPocoDynamo dynamoDb, long publisherAccountId, long publisherAppId,
                                                                                 bool includeDeleted = false, bool ignoreRecordNotFound = false)
    {
        var publisherAppAccount = await dynamoDb.GetItemAsync<DynPublisherAppAccount>(publisherAccountId, publisherAppId.ToEdgeId());

        if ((publisherAppAccount == null && !ignoreRecordNotFound) ||
            (publisherAppAccount != null && !includeDeleted && !publisherAppAccount.IsValid()))
        {
            throw new RecordNotFoundException();
        }

        return publisherAppAccount;
    }

    public static bool HasScope(this DynPublisherAppAccount source, string scope)
        => source != null && !source.PubAccessTokenScopes.IsNullOrEmpty() && source.PubAccessTokenScopes.Contains(scope);

    public static bool HasManagePagesScope(this DynPublisherAppAccount source)
        => HasScope(source, FacebookAccessToken.ScopeManagePages);

    public static bool HasBusinessManagementScope(this DynPublisherAppAccount source)
        => HasScope(source, FacebookAccessToken.ScopeBusinessManagement);

    public static async Task<DynPublisherAppAccount> GetDefaultPublisherAppAccountAsync(this IPocoDynamo dynamoDb, DynPublisherAccount tokenPublisherAccount)
    {
        if (tokenPublisherAccount == null || !tokenPublisherAccount.IsTokenAccount())
        {
            return null;
        }

        var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(tokenPublisherAccount.PublisherType.ToString());

        var publisherApp = await publisherDataService.GetDefaultPublisherAppAsync();

        var publisherAppAccount = await TryGetPublisherAppAccountAsync(dynamoDb, tokenPublisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

        return publisherAppAccount;
    }

    public static async Task<DynPublisherAppAccount> GetPublisherAppAccountOrDefaultAsync(this IPocoDynamo dynamoDb, long forPublisherAccountId,
                                                                                          long publisherAppId = 0, long workspaceId = 0,
                                                                                          long tokenPublisherAccountId = 0)
    {
        var publisherAccount = await DefaultPublisherAccountService.GetPublisherAccountAsync(forPublisherAccountId);

        var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(publisherAccount.PublisherType.ToString());

        var publisherApp = await publisherDataService.GetPublisherAppOrDefaultAsync(publisherAppId, workspaceId: workspaceId);

        var existingPublisherAppAccount = await TryGetPublisherAppAccountAsync(dynamoDb, publisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

        // PublisherAppAccounts are responsible for providing tokens for use with sync - however, many publisher accounts are NOT token-based
        // accounts, and are instead linked under one or more token account(s) that can be used to sync the account with. If a specific token
        // account was requested, use that one - otherwise, get the one that is most usable

        if (publisherAccount.IsTokenAccount())
        { // The account itself is a token account, always use it
            return existingPublisherAppAccount;
        }

        var isBasicWithValidToken = publisherAccount.IsBasicLink && existingPublisherAppAccount != null &&
                                    !existingPublisherAppAccount.IsShadowAppAccont && existingPublisherAppAccount.PubAccessToken.HasValue();

        // If a basic account with a valid token, we try to get a full-fledged linked account to full token account first...if we cannot find one,
        // we will eventually return this. one...
        var publisherAppAccount = isBasicWithValidToken || existingPublisherAppAccount == null
                                      ? new DynPublisherAppAccount
                                        {
                                            PublisherAppId = publisherApp.PublisherAppId,
                                            PublisherAccountId = publisherAccount.PublisherAccountId,
                                            DynItemType = DynItemType.PublisherAppAccount
                                        }
                                      : existingPublisherAppAccount;

        // Anything below here infers that the appAccount returned will be a shadow account - set that, clear any existing token (should not be there anyhow),
        // and by default disable the sync for current use
        publisherAppAccount.IsShadowAppAccont = true;
        publisherAppAccount.PubAccessToken = null;
        publisherAppAccount.IsSyncDisabled = true;

        async Task<bool> tokenizeAppAccount(DynPublisherAccount tokenAccount, bool perfectOnly = false)
        {
            if (tokenAccount == null || tokenAccount.IsDeleted() || !tokenAccount.IsTokenAccount())
            {
                return false;
            }

            // Get the token account's publisherapp account for the same appId
            var tokenPublisherAppAccount = await GetPublisherAppAccountOrDefaultAsync(dynamoDb, tokenAccount.PublisherAccountId,
                                                                                      publisherAppAccount.PublisherAppId,
                                                                                      workspaceId,
                                                                                      tokenAccount.PublisherAccountId);

            if (tokenPublisherAppAccount == null || !tokenPublisherAppAccount.IsValid())
            {
                return false;
            }

            if (publisherAppAccount.PubAccessToken.IsNullOrEmpty() ||
                publisherAppAccount.FailuresSinceLastSuccess > tokenPublisherAppAccount.FailuresSinceLastSuccess ||
                (publisherAppAccount.FailuresSinceLastSuccess == tokenPublisherAppAccount.FailuresSinceLastSuccess &&
                 publisherAppAccount.TokenLastUpdated < tokenPublisherAppAccount.TokenLastUpdated))
            {
                publisherAppAccount.IsShadowAppAccont = true;
                publisherAppAccount.PubAccessToken = tokenPublisherAppAccount.PubAccessToken;
                publisherAppAccount.PubAccessTokenScopes = tokenPublisherAppAccount.PubAccessTokenScopes;
                publisherAppAccount.TokenLastUpdated = tokenPublisherAppAccount.TokenLastUpdated;
                publisherAppAccount.IsSyncDisabled = tokenPublisherAppAccount.IsSyncDisabled;
                publisherAppAccount.ForUserId = tokenPublisherAppAccount.ForUserId;
            }

            return !perfectOnly || (tokenPublisherAppAccount.FailuresSinceLastSuccess <= 0 && !tokenPublisherAppAccount.IsSyncDisabled);
        }

        if (tokenPublisherAccountId > 0 && forPublisherAccountId != tokenPublisherAccountId)
        { // Specific account requested, get it and return it no matter what else...even if invalid...
            var specificTokenPublisherAccount = await DefaultPublisherAccountService.TryGetPublisherAccountAsync(tokenPublisherAccountId);

            await tokenizeAppAccount(specificTokenPublisherAccount);

            return publisherAppAccount;
        }

        if (workspaceId > 0)
        { // Try the default token-account for the workspace provided, if valid, use it
            var workspaceTokenAccount = await WorkspaceService.DefaultWorkspaceService
                                                              .TryGetDefaultPublisherAccountAsync(workspaceId);

            if (await tokenizeAppAccount(workspaceTokenAccount))
            {
                return publisherAppAccount;
            }
        }

        // Try to find a perfect account for use
        await foreach (var linkedTokenAccount in DefaultPublisherAccountService.GetLinkedPublisherAccountsAsync(publisherAccount.PublisherAccountId)
                                                                               .Where(pa => pa.IsTokenAccount() && !pa.IsSyncDisabled)
                                                                               .OrderBy(pa => pa.FailuresSinceLastSuccess))
        {
            if (await tokenizeAppAccount(linkedTokenAccount, true))
            {
                return publisherAppAccount;
            }
        }

        // If we made it this far, we don't have a perfect match or anything else default-y that is a better option, so use whatever we got
        if (isBasicWithValidToken)
        {
            return existingPublisherAppAccount;
        }

        Guard.AgainstRecordNotFound(publisherAppAccount.IsDeleted() || publisherAppAccount.PubAccessToken.IsNullOrEmpty(),
                                    $"Could not locate a valid token-based account to sync media for [{publisherAccount.DisplayName()}]");

        return publisherAppAccount;
    }

    public static (long Engagements, long Impressions, long Saves, long Views, long Reach, long Actions, long Comments, long Replies) GetRatingStats(this DynPublisherMediaStat stat)
    {
        if (stat == null || stat.Stats.IsNullOrEmpty())
        {
            return (0, 0, 0, 0, 0, 0, 0, 0);
        }

        var engagements = 0L;
        var impressions = 0L;
        var saves = 0L;
        var views = 0L;
        var reach = 0L;
        var actions = 0L;
        var comments = 0L;
        var replies = 0L;

        foreach (var statItem in stat.Stats)
        {
            // Equality compares
            if (statItem.Value > actions && FbIgInsights.ActionsName.EqualsOrdinalCi(statItem.Name))
            {
                actions = statItem.Value;
            }
            else if (statItem.Value > comments && FbIgInsights.CommentsName.EqualsOrdinalCi(statItem.Name))
            {
                comments = statItem.Value;
            }
            else if (statItem.Value > replies && FbIgInsights.RepliesName.EqualsOrdinalCi(statItem.Name))
            {
                replies = statItem.Value;
            }

            // Contains compares
            if (statItem.Value > engagements && FbIgInsights.EngageStatNames.Contains(statItem.Name))
            {
                engagements = statItem.Value;
            }

            if (statItem.Value > impressions && FbIgInsights.ImpressionStatNames.Contains(statItem.Name))
            {
                impressions = statItem.Value;
            }

            if (statItem.Value > saves && FbIgInsights.SaveStatNames.Contains(statItem.Name))
            {
                saves = statItem.Value;
            }

            if (statItem.Value > views && FbIgInsights.ViewStatNames.Contains(statItem.Name))
            {
                views = statItem.Value;
            }

            if (statItem.Value > reach && FbIgInsights.ReachStatNames.Contains(statItem.Name))
            {
                reach = statItem.Value;
            }
        }

        return (engagements, impressions, saves, views, reach, actions, comments, replies);
    }

    public static long GetStat(this DynPublisherMediaStat stat, string statName)
    {
        if (stat == null || stat.Stats.IsNullOrEmpty())
        {
            return 0;
        }

        return (stat.Stats
                    .FirstOrDefault(s => s.Name.EqualsOrdinalCi(statName))?
                    .Value).Gz(0);
    }

    public static async Task ProcessDailyStatsAsync<T>(this Dictionary<string, double> metrics, long publisherAccountId,
                                                       RecordType statRecordType, DateTime? day = null)
        where T : DynDailyStatBase, new()
    {
        if (metrics.IsNullOrEmpty())
        {
            return;
        }

        if (!day.HasValue || day.Value <= DateTimeHelper.MinApplicationDate || day.Value >= DateTimeHelper.MaxApplicationDate)
        {
            day = DateTimeHelper.UtcNow;
        }

        var dayTs = day.Value.Date.ToUnixTimestamp();
        var dayStatEdgeId = DynDailyStatBase.BuildEdgeId<T>(dayTs);

        var dailyStat = await _dynamoDb.GetItemAsync<T>(publisherAccountId, dayStatEdgeId);

        if (dailyStat == null)
        {
            dailyStat = new T
                        {
                            Id = publisherAccountId,
                            EdgeId = dayStatEdgeId,
                            AssociatedType = RecordType.PublisherAccount,
                            ExpiresAt = dayTs + (60 * 60 * 24 * 65),
                            Stats = new Dictionary<string, DailyStatValue>()
                        };
        }

        dailyStat.UpdateDateTimeTrackedValues();
        dailyStat.PublisherAccountId = publisherAccountId;

        var needsPut = false;

        foreach (var metric in metrics)
        {
            DailyStatValue dailyStatValue = null;

            if (dailyStat.Stats.ContainsKey(metric.Key))
            { // If we already have a a metric stored for this key but the incoming metric value is 0 or a value less than we care to track, nothing to do
                if (metric.Value < 0.000001)
                {
                    continue;
                }

                dailyStatValue = dailyStat.Stats[metric.Key];
            }
            else
            {
                dailyStatValue = new DailyStatValue();
            }

            // If nothing needs to be updated, skip along
            if (Math.Abs(dailyStatValue.Value - metric.Value) < 0.000001 &&
                dailyStatValue.MaxValue >= metric.Value &&
                (metric.Value <= 0 || (dailyStatValue.MinValue > 0 && dailyStatValue.MinValue <= metric.Value)))
            {
                continue;
            }

            needsPut = true;
            dailyStatValue.Measurements++;
            dailyStatValue.Value = metric.Value;

            if (dailyStatValue.MinValue <= 0 || dailyStatValue.Value < dailyStatValue.MinValue)
            {
                dailyStatValue.MinValue = dailyStatValue.Value;
            }

            if (dailyStatValue.Value > dailyStatValue.MaxValue)
            {
                dailyStatValue.MaxValue = dailyStatValue.Value;
            }

            dailyStat.Stats[metric.Key] = dailyStatValue;
        }

        // Writes are expensive, so we try to avoid it if not needed for higher-volume write areas
        if (needsPut)
        {
            await _dynamoDb.PutItemAsync(dailyStat);

            _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                 {
                                                     CompositeIds = new List<DynamoItemIdEdge>
                                                                    {
                                                                        new(dailyStat.Id, dailyStat.EdgeId)
                                                                    },
                                                     Type = statRecordType
                                                 });
        }
    }
}
