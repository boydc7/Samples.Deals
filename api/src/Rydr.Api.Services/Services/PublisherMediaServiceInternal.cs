using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Enums;
using ServiceStack;
using ServiceStack.Caching;

#pragma warning disable 162

namespace Rydr.Api.Services.Services
{
    public class PublisherInternalMediaService : BaseInternalOnlyApiService
    {
        private static readonly bool _sendRelinkNotifications = RydrEnvironment.GetAppSetting("Notifications.SendRelinks", false);

        private const string _postSyncKeyCategory = "postsyncrecentpublisheraccountmedia";

        private static readonly int _synIntervalMinutes = RydrEnvironment.GetAppSetting("PublisherAccount.SyncIntervalMinutes", 60);

        // Multiply by 45 here purposely instead of 60...we want 3/4 the normal sync interval as the limit on how often it can actually programatically fire
        private static readonly int _syncIntervalLimit = RydrEnvironment.GetAppSetting("PublisherAccount.SyncIntervalMinutes", 60) * 45;

        private readonly IDistributedLockService _distributedLockService;
        private readonly ICacheClient _cacheClient;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IServerNotificationService _serverNotificationService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IDealRequestService _dealRequestService;
        private readonly IDealService _dealService;
        private readonly IPersistentCounterAndListService _counterAndListService;
        private readonly IPublisherMediaSingleStorageService _publisherMediaSingleStorageService;
        private readonly IOpsNotificationService _opsNotificationService;
        private readonly IMapItemService _mapItemService;
        private readonly IFileStorageService _fileStorageService;

        public PublisherInternalMediaService(IDistributedLockService distributedLockService, ICacheClient cacheClient,
                                             IDeferRequestsService deferRequestsService, IServerNotificationService serverNotificationService,
                                             IPublisherAccountService publisherAccountService, IDealRequestService dealRequestService,
                                             IDealService dealService, IPersistentCounterAndListService counterAndListService,
                                             IPublisherMediaSingleStorageService publisherMediaSingleStorageService,
                                             IOpsNotificationService opsNotificationService, IMapItemService mapItemService,
                                             IFileStorageService fileStorageService)
        {
            _distributedLockService = distributedLockService;
            _cacheClient = cacheClient;
            _deferRequestsService = deferRequestsService;
            _serverNotificationService = serverNotificationService;
            _publisherAccountService = publisherAccountService;
            _dealRequestService = dealRequestService;
            _dealService = dealService;
            _counterAndListService = counterAndListService;
            _publisherMediaSingleStorageService = publisherMediaSingleStorageService;
            _opsNotificationService = opsNotificationService;
            _mapItemService = mapItemService;
            _fileStorageService = fileStorageService;
        }

        public async Task Post(PostPublisherMediaStatsReceived request)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemAsync<DynPublisherMedia>(request.PublisherAccountId, request.PublisherMediaId.ToEdgeId());

            if (dynPublisherMedia == null)
            {
                return;
            }

            var dynPublisherMediaStat = dynPublisherMedia.ToDynPublisherMediaStat(request.Stats);

            await _publisherMediaSingleStorageService.StoreAsync(dynPublisherMediaStat);

            if (dynPublisherMedia.PreBizAccountConversionMediaErrorCount > 0)
            {
                dynPublisherMedia.PreBizAccountConversionMediaErrorCount = 0;

                await _dynamoDb.TryPutItemMappedAsync(dynPublisherMedia, dynPublisherMedia.ReferenceId);
            }
        }

        public async Task Post(PostPublisherMediaReceived request)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemAsync<DynPublisherMedia>(request.PublisherAccountId, request.PublisherMediaId.ToEdgeId());

            if (dynPublisherMedia == null || dynPublisherMedia.IsDeleted() || dynPublisherMedia.Caption.IsNullOrEmpty())
            {
                return;
            }

            var dynPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

            if (dynPublisherAccount == null || dynPublisherAccount.IsDeleted())
            {
                return;
            }

            // Ensure a map exists for this media and reference
            if (!(await _mapItemService.MapExistsAsync(dynPublisherMedia.PublisherAccountId,
                                                       DynItemMap.BuildEdgeId(dynPublisherMedia.DynItemType, dynPublisherMedia.ReferenceId))))
            {
                await _mapItemService.PutMapAsync(new DynItemMap
                                                  {
                                                      Id = dynPublisherMedia.PublisherAccountId,
                                                      EdgeId = DynItemMap.BuildEdgeId(dynPublisherMedia.DynItemType, dynPublisherMedia.ReferenceId),
                                                      MappedItemEdgeId = dynPublisherMedia.EdgeId
                                                  });
            }

            await CheckMediaForPotentialDealCompletionAsync(dynPublisherMedia, dynPublisherAccount);
        }

        public async Task<LongIdResponse> Post(PostPublisherMediaUpsert request)
        {
            // Right now haven't built the part to take a Rydr media, store a file, set the url, etc...so for now, if there's not a url for a rydr media, just return
            if (request.Model.PublisherType != PublisherType.Rydr && request.Model.MediaUrl.IsNullOrEmpty())
            {
                return 0L.ToLongIdResponse();
            }

            if (request.Model.PublisherType == PublisherType.Rydr)
            {
                var rydrFileId = request.Model.MediaId.ToLong();

                if (rydrFileId > 0)
                {
                    await _fileStorageService.ConfirmUploadAsync(rydrFileId);
                }
            }

            var publisherAccountId = request.Model.PublisherAccountId.Gz(request.RequestPublisherAccountId);

            var existingDynMedia = request.Model.Id > 0
                                       ? await _dynamoDb.GetItemAsync<DynPublisherMedia>(publisherAccountId, request.Model.Id.ToEdgeId())
                                       : await _dynamoDb.GetItemByRefAsync<DynPublisherMedia>(publisherAccountId, DynPublisherMedia.BuildRefId(request.Model.PublisherType,
                                                                                                                                               request.Model.MediaId), DynItemType.PublisherMedia, true, true);

            if (existingDynMedia == null)
            { // Just a new one
                var newPublisherMedia = await request.Model.ToDynPublisherMediaAsync(forceNoPut: true);

                await _dynamoDb.PutItemAsync(newPublisherMedia);

                return newPublisherMedia.PublisherMediaId.ToLongIdResponse();
            }

            // Existing one to update
            await _dynamoDb.UpdateFromExistingAsync(existingDynMedia, x => request.Model.ToDynPublisherMediaAsync(x, true), request);

            return existingDynMedia.PublisherMediaId.ToLongIdResponse();
        }

        [RequiredRole("Admin")]
        public async Task Post(PublisherAccountSyncEnable request)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

            publisherAccount.FailuresSinceLastSuccess = 0;
            publisherAccount.IsSyncDisabled = false;
            publisherAccount.DeletedBy = null;
            publisherAccount.DeletedOn = null;

            await _dynamoDb.PutItemTrackDeferAsync(publisherAccount, RecordType.PublisherAccount);

            await _publisherAccountService.PutAccessTokenAsync(publisherAccount.PublisherAccountId, null, publisherAccount.PublisherType);
        }

        [RequiredRole("Admin")]
        public async Task Delete(PublisherAccountSyncEnable request)
            => await DisablePublisherAccountSyncAsync(request.PublisherAccountId);

        [RequiredRole("Admin")]
        public async Task Post(PostSyncRecentPublisherAccountMedia request)
        {
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId);

#if LOCALDEBUG
            _log.WarnFormat("SKIPPING LOCALDEBUG PostSyncRecentPublisherAccountMedia for PublisherAccountId [{0}].", publisherAccount.DisplayName());

            return;
#endif

            if (publisherAccount == null || publisherAccount.IsDeleted())
            { // Delete the account, associations, job, etc...
                await DoDeleteAccountAsync(request.PublisherAccountId);

                _log.WarnFormat("Invalid PublisherAccountId [{0}] - deleting account and disabling sync", request.PublisherAccountId);

                return;
            }

            if (publisherAccount.IsRydrSoftLinkedAccount() && !publisherAccount.IsBasicLink)
            {
                _log.DebugInfoFormat("No media syncing possible for RydrSoftLinked PublisherAccountId [{0}] - disabling sync", request.PublisherAccountId);

                await DisablePublisherAccountSyncAsync(publisherAccount.PublisherAccountId);

                return;
            }

            if (request.PublisherAppId > 0)
            {
                var publisherApp = await _dynamoDb.GetPublisherAppAsync(request.PublisherAppId, true);

                if (publisherApp == null || publisherApp.IsDeleted())
                {
                    _log.WarnFormat("Invalid PublisherAppId [{0}] - disabling sync for that appId. Attempted for PublisherAccount [{1}]", request.PublisherAppId, publisherAccount.DisplayName());

                    return;
                }
            }

            if (publisherAccount.IsSyncDisabled && !request.Force)
            { // Nothing more to do
                await DisablePublisherAccountSyncAsync(publisherAccount.PublisherAccountId);

                _log.WarnFormat("Sync is disabled for PublisherAccount [{0}] - exiting", publisherAccount.DisplayName());

                return;
            }

            // If this is a token-based account, sync it directly here itself (and then kick-off a sync for all linked non-token accounts later)
            // If not a token-based account, sync this account with one of the token-based accounts it is linked from
            var linkedPublisherAccounts = await _publisherAccountService.GetLinkedPublisherAccountsAsync(publisherAccount.PublisherAccountId)
                                                                        .Take(250)
                                                                        .ToList(250);

            int? syncResult = null;
            var syncAttempts = 0;

            foreach (var linkedPublisherAccount in linkedPublisherAccounts?.OrderBy(p => p.FailuresSinceLastSuccess)
                                                                          .ThenByDescending(p => p.LastMediaSyncedOn) ?? Enumerable.Empty<DynPublisherAccount>())
            {
                if (linkedPublisherAccount.PublisherAccountId == publisherAccount.PublisherAccountId ||
                    linkedPublisherAccount.IsRydrSoftLinkedAccount() ||
                    linkedPublisherAccount.PublisherType != publisherAccount.PublisherType)
                {
                    continue;
                }

                // In this main loop we only continue with linked token accounts, so if we come upon a linked account that is NOT a token account,
                // just push it onto the sync queue, and let it try to sync using any token accounts that it is linked from...
                if (!linkedPublisherAccount.IsTokenAccount())
                { // Not a token account, the account being synced is linked to this account. Send this linked account to the q for syncing directly
                    var postMediaSyncAssociatedAccount = new PostSyncRecentPublisherAccountMedia
                                                         {
                                                             WithWorkspaceId = request.WithWorkspaceId,
                                                             PublisherAccountId = linkedPublisherAccount.PublisherAccountId,
                                                             PublisherAppId = request.PublisherAppId,
                                                             Force = request.Force
                                                         }.WithAdminRequestInfo();

                    _deferRequestsService.PublishMessage(postMediaSyncAssociatedAccount);

                    continue;
                }

                var dynPublisherAppAccount = await GetAppAccountForSyncTokenAccountsAsync(publisherAccount, linkedPublisherAccount,
                                                                                          request.PublisherAppId, request.WithWorkspaceId);

                if (dynPublisherAppAccount == null ||
                    (!request.Force && (linkedPublisherAccount.IsSyncDisabled || dynPublisherAppAccount.IsSyncDisabled)))
                {
                    continue;
                }

                // Token account - the account being synced is linked from this account as a valid token source, so try to sync it with this token source
                syncResult = await TrySyncPublisherAccountAsync(dynPublisherAppAccount, linkedPublisherAccount, publisherAccount, request.Force);

                syncAttempts++;

                if (syncResult != 0)
                { // Success if > 0, hard stop if < 0...either way, break...
                    break;
                }
            }

            // If this is a basic linked account (i.e. Instagram Basic API linked) and it wasn't synced by a proper token account above,
            // try to sync it with a basic token
            if (!publisherAccount.IsTokenAccount() && publisherAccount.IsBasicLink &&
                (syncAttempts <= 0 || (syncResult.HasValue && syncResult <= 0)))
            {
                var publisherDataService = RydrEnvironment.Container.ResolveNamed<IPublisherDataService>(publisherAccount.PublisherType.ToString());

                var publisherApp = await publisherDataService.GetPublisherAppOrDefaultAsync(request.PublisherAppId, AccessIntent.ReadOnly, request.WithWorkspaceId);
                var publisherAppAccount = await _dynamoDb.TryGetPublisherAppAccountAsync(publisherAccount.PublisherAccountId, publisherApp.PublisherAppId);

                if (publisherAppAccount != null && !publisherAppAccount.IsSyncDisabled && publisherAppAccount.PubAccessToken.HasValue())
                {
                    syncResult = await TrySyncPublisherAccountAsync(publisherAppAccount, publisherAccount, publisherAccount, request.Force);
                    syncAttempts++;
                }
            }

            if (!publisherAccount.IsTokenAccount() && syncAttempts <= 0)
            { // Tried to sync for a non-token account that apparentely has no linked token accounts to even attempt sync with - nothing much we can do there,
                await DisablePublisherAccountSyncAsync(publisherAccount.PublisherAccountId);

                return;
            }

            // If this is a non-token account and we were not able to attempt sync with anything, that means this account is basically unable to sync at the moment
            // for some reason (likely not connected to any token accounts any longer, doesn't have a valid access token, etc.)
            if (!request.Force && syncAttempts > 0 && syncResult.HasValue && syncResult.Value == 0)
            { // Tried to sync at least once, but didn't get a successful result
                var lastSynced = publisherAccount.LastMediaSyncedOn.ToDateTime();

                if ((_dateTimeProvider.UtcNow - lastSynced).TotalMinutes >= (_synIntervalMinutes * 3))
                {
                    await _opsNotificationService.TrySendApiNotificationAsync($"Media Sync Unsuccessful 3x - {publisherAccount.DisplayName()}",
                                                                              $@"<https://instagram.com/{publisherAccount.UserName}|IG : {publisherAccount.UserName}>
<https://app.datadoghq.com/logs?live=true&query=%40pubId%3A{publisherAccount.PublisherAccountId}|Publisher Logs ({publisherAccount.PublisherAccountId})>
Sync Disabled : {(publisherAccount.IsSyncDisabled ? "Yes" : "No")}
Attempts : {syncAttempts}
    Linked Accts : {linkedPublisherAccounts?.Count ?? 0}
Failures : {publisherAccount.FailuresSinceLastSuccess}
Last Synced : {lastSynced.ToSqlString()}
    Hours Ago : {Math.Round((_dateTimeProvider.UtcNow - lastSynced).TotalHours, 2)}

<https://app.datadoghq.com/logs?live=true&query=%40dto%3APostSyncRecentPublisherAccountMedia|View All Media Sync Logs>");
                }
            }

            // Sync this account if it is a token account
            if (publisherAccount.IsTokenAccount())
            {
                var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountOrDefaultAsync(publisherAccount.PublisherAccountId, request.PublisherAppId, request.WithWorkspaceId);

                if (publisherAppAccount != null && !publisherAppAccount.IsSyncDisabled)
                {
                    // If the appAccount here has no token, disable the entire publisher, as this is a token account...the publsher is the appAccount basically
                    if (publisherAppAccount.PubAccessToken.IsNullOrEmpty())
                    {
                        await DisablePublisherAccountSyncAsync(publisherAccount.PublisherAccountId);
                    }
                    else
                    {
                        await TrySyncPublisherAccountAsync(publisherAppAccount, publisherAccount, publisherAccount, request.Force);
                    }
                }
            }
        }

        private async Task<DynPublisherAppAccount> GetAppAccountForSyncTokenAccountsAsync(DynPublisherAccount syncPublisherAccount, DynPublisherAccount syncWithTokenAccount,
                                                                                          long publisherAppId, long workspaceId, bool forceCheck = false)
        {
            // Token account must be one, sync account must not be
            Guard.AgainstArgumentOutOfRange(syncPublisherAccount.IsTokenAccount(), "syncPublisherAccount must not be a TokenAccount");
            Guard.AgainstArgumentOutOfRange(!syncWithTokenAccount.IsTokenAccount(), "syncWithTokenAccount must be a TokenAccount");

            var publisherAppAccount = await _dynamoDb.GetPublisherAppAccountOrDefaultAsync(syncPublisherAccount.PublisherAccountId, publisherAppId, workspaceId,
                                                                                           syncWithTokenAccount.PublisherAccountId);

            // Ensure the app account is actually valid for use
            if (publisherAppAccount == null || publisherAppAccount.PubAccessToken.IsNullOrEmpty() || (!forceCheck && publisherAppAccount.IsSyncDisabled))
            {
                await DisableSyncAsync(publisherAppAccount, syncWithTokenAccount);

                return null;
            }

            // Ensure the token'd account still has access to the linked account at the publisher (i.e. fb)
            if (syncWithTokenAccount.PublisherType != PublisherType.Facebook || syncPublisherAccount.AccountType != PublisherAccountType.FbIgUser)
            {
                return publisherAppAccount;
            }

            var haveAccess = await VerifyFbIgAccessForSyncByAsync(syncPublisherAccount, syncWithTokenAccount, publisherAppAccount, workspaceId);

            return haveAccess
                       ? publisherAppAccount
                       : null;
        }

        private async Task<bool> VerifyFbIgAccessForSyncByAsync(DynPublisherAccount syncPublisherAccount, DynPublisherAccount syncWithTokenAccount,
                                                                DynPublisherAppAccount publisherAppAccount, long workspaceId, bool forceCheck = false)
        {
            var lastVerifiedFbAccessKey = string.Concat(_postSyncKeyCategory, "_fbaccess_", syncWithTokenAccount.PublisherAccountId, "|", syncPublisherAccount.PublisherAccountId);

            var lastSynced = _cacheClient.TryGet<Int64Id>(lastVerifiedFbAccessKey);

            var syncedMinutesAgo = (_dateTimeProvider.UtcNowTs - (lastSynced?.Id ?? 0)) / 60;

            if (!forceCheck && syncedMinutesAgo < (_synIntervalMinutes * 5))
            {
                return true;
            }

            var response = true;

            var fbClient = await publisherAppAccount.GetOrCreateFbClientAsync();

            var validateReponse = await fbClient.ValidateAccessToFbIgAccountAsync(syncPublisherAccount.AccountId, publisherAppAccount.ForUserId);

            if (syncWithTokenAccount.IsRydrSystemPublisherAccount() && !validateReponse.VerifiedAccess)
            { // System account - if the account attempting sync was/is a soft-linked account, do nothing here - do not delink, do not fail...just cannot
                // sync with this account (as we dont have a token for it). We dont delink it however as we let our biz workspace manage soft-linked accounts
                if (syncPublisherAccount.IsRydrSoftLinkedAccount() ||
                    await _mapItemService.MapExistsAsync(syncWithTokenAccount.PublisherAccountId,
                                                         syncPublisherAccount.ToRydrSoftLinkedAssociationId()))
                {   // Do not delink or anything, but we do not have access to sync anything
                    return false;
                }
            }

            if (validateReponse.Unauthorized)
            { // The non-token account is no longer linked to / permissioned into the token account in question, delink it from all workspaces
                // the token account is linked to, and of course from each other
                _log.WarnFormat("VerifyFbIgAccess returned Unauthorized - Access to FbIg PublisherAccount [{0}] has been revoked/removed from token PublisherAccount [{1}] - delinking the accounts", syncPublisherAccount.DisplayName(), syncWithTokenAccount.DisplayName());

                _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                   {
                                                       FromWorkspaceId = 0,
                                                       FromPublisherAccountId = syncWithTokenAccount.PublisherAccountId,
                                                       ToPublisherAccountId = syncPublisherAccount.PublisherAccountId
                                                   }.WithAdminRequestInfo());

                if (workspaceId > 0)
                {
                    _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                       {
                                                           FromWorkspaceId = workspaceId,
                                                           FromPublisherAccountId = syncWithTokenAccount.PublisherAccountId,
                                                           ToPublisherAccountId = syncPublisherAccount.PublisherAccountId
                                                       }.WithAdminRequestInfo());
                }

                response = false;
            }
            else if (validateReponse.RequiresReAuthentication)
            {
                _log.WarnFormat("VerifyFbIgAccess returned RequiresReAuthentication - token PublisherAccount [{0}] must re-authenticate and generate a new access token for use to continue, disabling sync", syncWithTokenAccount.DisplayName());

                // Requires re-authentication to continue using this
                await DisablePublisherAccountSyncAsync(syncWithTokenAccount.PublisherAccountId);

                response = false;
            }

            // Update the cached timestamp on fb verification
            if (validateReponse.VerifiedAccess)
            {
                await _cacheClient.TrySetAsync(Int64Id.FromValue(_dateTimeProvider.UtcNowTs), lastVerifiedFbAccessKey, CacheConfig.LongConfig);

                if (syncWithTokenAccount.IsRydrSystemPublisherAccount())
                { // If we get to here where we've verified access, and this is a rydr system account, that means we're properly linked to this page, remove
                    // the soft-linked association if one eixsts
                    var map = new DynItemMap
                              {
                                  Id = syncWithTokenAccount.PublisherAccountId,
                                  EdgeId = syncPublisherAccount.ToRydrSoftLinkedAssociationId()
                              };

                    if (await _mapItemService.MapExistsAsync(map.Id, map.EdgeId))
                    {
                        await _mapItemService.DeleteMapAsync(map.Id, map.EdgeId);
                    }
                }
            }

            return response;
        }

        private async Task<int> TrySyncPublisherAccountAsync(DynPublisherAppAccount withPublisherAppAccount, DynPublisherAccount tokenPublisherAccount,
                                                             DynPublisherAccount syncPublisherAccount, bool forceSync)
        {
            Guard.AgainstInvalidData(syncPublisherAccount.PublisherAccountId, withPublisherAppAccount.PublisherAccountId, "SyncPublisherAccount.Id and WithPublisherAppAccount.PublisherAccountId should match");
            Guard.AgainstInvalidData(tokenPublisherAccount.PublisherType != syncPublisherAccount.PublisherType, "tokenPublisherAccount.PublisherType and tokenPublisherAppAccount.PublisherType should match");

            if (!forceSync)
            {
                if (tokenPublisherAccount.IsSyncDisabled || withPublisherAppAccount.IsSyncDisabled)
                {
                    _log.InfoFormat("  Sync is currently disabled for Token PublisherAccount [{0}], syncPublisherAccount of [{1}], exiting",
                                    tokenPublisherAccount.DisplayName(), syncPublisherAccount.DisplayName());

                    return 0;
                }

                if (withPublisherAppAccount.FailuresSinceLastSuccess >= 10 && (_dateTimeProvider.UtcNowTs - withPublisherAppAccount.LastFailedOn) < (_synIntervalMinutes * 60 * 10))
                {
                    _log.InfoFormat("  Not syncing for PublisherAccount [{0}], FailuresSinceLastSuccess > 10, only syncing at 10x interval", syncPublisherAccount.DisplayName());

                    return 0;
                }
            }

            if (!CanSyncPublisherAccountId(syncPublisherAccount.PublisherAccountId, forceSync))
            {
                return -1;
            }

            using(var lockItem = _distributedLockService.TryGetKeyLock(syncPublisherAccount.PublisherAccountId.ToStringInvariant(), _postSyncKeyCategory, _syncIntervalLimit - 90))
            {
                if (lockItem == null)
                {
                    _log.WarnFormat("Media sync already in progress for PublisherAccount [{0}] - exiting attempt.", syncPublisherAccount.DisplayName());

                    return -1;
                }

                try
                {
                    _log.DebugInfoFormat("Starting SyncRecentMediaAsync for PublisherAccount [{0}], with TokenPublisherAccount of [{1}], publisherAppId [{2}]", syncPublisherAccount.DisplayName(), tokenPublisherAccount.DisplayName(), withPublisherAppAccount.PublisherAppId);

                    var publisherMediaSyncService = RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(tokenPublisherAccount.PublisherType.ToString());

                    var syncPublisherAppAccountInfo = new SyncPublisherAppAccountInfo(withPublisherAppAccount);

                    await publisherMediaSyncService.SyncRecentMediaAsync(syncPublisherAppAccountInfo);

                    var updateJob = withPublisherAppAccount.FailuresSinceLastSuccess > 0 || syncPublisherAccount.FailuresSinceLastSuccess > 0 ||
                                    tokenPublisherAccount.FailuresSinceLastSuccess > 0 || tokenPublisherAccount.IsSyncDisabled ||
                                    withPublisherAppAccount.IsSyncDisabled || syncPublisherAccount.IsSyncDisabled;

                    // Clear any failure flags, update timestamps for the appAccount object, store step info
                    await _dynamoDb.PutItemTrackedInterlockedAsync(withPublisherAppAccount, d =>
                                                                                            {
                                                                                                d.FailuresSinceLastSuccess = 0;
                                                                                                d.LastFailedOn = 0;
                                                                                                d.IsSyncDisabled = false;
                                                                                                d.SyncStepsLastFailedOn = syncPublisherAppAccountInfo.SyncStepsLastFailedOn;
                                                                                                d.SyncStepsFailCount = syncPublisherAppAccountInfo.SyncStepsFailCount;

                                                                                                if (d.IsShadowAppAccont)
                                                                                                {
                                                                                                    d.PubAccessToken = null;
                                                                                                }
                                                                                                else if (syncPublisherAppAccountInfo.TokenUpdated)
                                                                                                {
                                                                                                    d.TokenLastUpdated = withPublisherAppAccount.TokenLastUpdated;
                                                                                                    d.PubAccessToken = withPublisherAppAccount.PubAccessToken;
                                                                                                    d.ExpiresAt = withPublisherAppAccount.ExpiresAt;
                                                                                                }
                                                                                            });

                    syncPublisherAccount = await _publisherAccountService.UpdatePublisherAccountAsync(syncPublisherAccount,
                                                                                                      dp =>
                                                                                                      {
                                                                                                          dp.IsSyncDisabled = false;
                                                                                                          dp.FailuresSinceLastSuccess = 0;
                                                                                                          dp.LastMediaSyncedOn = _dateTimeProvider.UtcNowTs;
                                                                                                      });

                    if (syncPublisherAccount.PublisherAccountId != tokenPublisherAccount.PublisherAccountId)
                    {
                        tokenPublisherAccount = await _publisherAccountService.UpdatePublisherAccountAsync(tokenPublisherAccount,
                                                                                                           dp =>
                                                                                                           {
                                                                                                               dp.IsSyncDisabled = false;
                                                                                                               dp.FailuresSinceLastSuccess = 0;
                                                                                                               dp.LastMediaSyncedOn = _dateTimeProvider.UtcNowTs;
                                                                                                           });
                    }

                    if (updateJob)
                    {
                        await publisherMediaSyncService.AddOrUpdateMediaSyncAsync(syncPublisherAccount.Id);
                    }

                    UpdateLastSync(syncPublisherAccount.PublisherAccountId);

                    _log.DebugInfoFormat("Completed SyncRecentMediaAsync successfully for PublisherAccount [{0}], PublisherAppId [{1}], TokenPublisherAccount of [{2}]",
                                         syncPublisherAccount.DisplayName(), withPublisherAppAccount.PublisherAppId, tokenPublisherAccount.DisplayName());

                    return 1;
                }
                catch(FbApiException fbx) when(fbx.IsPermissionError)
                {
                    if (fbx.RequiresOAuthRefresh)
                    { // For an oauth refresh, the token account has to be disabled...
                        await DisablePublisherAccountSyncAsync(tokenPublisherAccount.PublisherAccountId);
                    }
                    else
                    { // Otherwise, the sync/app account only - verify access if possible here
                        await Try.ExecAsync(() => VerifyFbIgAccessForSyncByAsync(syncPublisherAccount, tokenPublisherAccount, withPublisherAppAccount, 0, true));

                        await DisableSyncAsync(withPublisherAppAccount, tokenPublisherAccount);
                    }

                    _log.Exception(fbx, $"FbApiException permission error, PublisherAccountSync has been disabled for PublisherAccount [{syncPublisherAccount.DisplayName()}] - [{withPublisherAppAccount.PublisherAppId}|{tokenPublisherAccount.PublisherAccountId}]");
                }
                catch(FbApiException fbx) when(fbx.IsTransient)
                {
                    _log.Warn($"SyncRecentMediaAsync transient failure for PublisherAccount [{syncPublisherAccount.DisplayName()}], TokenPublisherAccount of [{tokenPublisherAccount.DisplayName()}]", fbx);

                    await _dynamoDb.PutItemTrackedInterlockedAsync(syncPublisherAccount, dpa => dpa.LastMediaSyncTransientFailureOn = _dateTimeProvider.UtcNowTs);
                }
                catch(Exception x)
                {
                    await Try.ExecAsync(async () =>
                                        {
                                            await _dynamoDb.PutItemTrackedInterlockedAsync(withPublisherAppAccount, dpa =>
                                                                                                                    {
                                                                                                                        dpa.FailuresSinceLastSuccess++;
                                                                                                                        dpa.LastFailedOn = _dateTimeProvider.UtcNowTs;
                                                                                                                    });

                                            await _dynamoDb.PutItemTrackedInterlockedDeferAsync(syncPublisherAccount,
                                                                                                dp => dp.FailuresSinceLastSuccess++,
                                                                                                RecordType.PublisherAccount);

                                            if (syncPublisherAccount.PublisherAccountId != tokenPublisherAccount.PublisherAccountId)
                                            {
                                                await _dynamoDb.PutItemTrackedInterlockedDeferAsync(tokenPublisherAccount,
                                                                                                    dp => dp.FailuresSinceLastSuccess++,
                                                                                                    RecordType.PublisherAccount);
                                            }
                                        });

                    _log.Exception(x, $"PostSyncRecentPublisherAccountMedia for PublisherAccount [{syncPublisherAccount.DisplayName()}] - [{withPublisherAppAccount.PublisherAppId}|{tokenPublisherAccount.PublisherAccountId}]");
                }

                return 0;
            }
        }

        private async Task DoDeleteAccountAsync(long publisherAccountId)
        {
            await DisablePublisherAccountSyncAsync(publisherAccountId);

            _deferRequestsService.DeferRequest(new DeletePublisherAccountInternal
                                               {
                                                   PublisherAccountId = publisherAccountId
                                               }.WithAdminRequestInfo());
        }

        private bool CanSyncPublisherAccountId(long publisherAccountId, bool force = false)
        {
            var lastSyncKey = GetLastSyncKey(publisherAccountId);

            // Only actually sync every allowable interval
            if (force)
            {
                return true;
            }

            var lastSynced = _cacheClient.TryGet<Int64Id>(lastSyncKey);

            if (lastSynced == null)
            {
                return true;
            }

            var syncedSecondsAgo = _dateTimeProvider.UtcNowTs - lastSynced.Id;

            if (syncedSecondsAgo > _syncIntervalLimit)
            {
                return true;
            }

            _log.DebugInfoFormat("Media sync performed only [{0}] seconds ago for PublisherAccountId [{1}] - exiting attempt.", syncedSecondsAgo, publisherAccountId);

            return false;
        }

        private string GetLastSyncKey(long publisherAccountId)
            => string.Concat(_postSyncKeyCategory, "_lastsync_", publisherAccountId);

        private void UpdateLastSync(long publisherAccountId)
        {
            var lastSyncKey = GetLastSyncKey(publisherAccountId);

            _cacheClient.TrySet(Int64Id.FromValue(_dateTimeProvider.UtcNowTs), lastSyncKey, CacheConfig.LongConfig);
        }

        private async Task DisablePublisherAccountSyncAsync(long publisherAccountId)
        {
            RemoveRecurringJob(publisherAccountId);

            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null)
            {
                return;
            }

            if (publisherAccount.IsSyncDisabled || publisherAccount.IsDeleted())
            {
                _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                     {
                                                         CompositeIds = new List<DynamoItemIdEdge>
                                                                        {
                                                                            new DynamoItemIdEdge(publisherAccount.Id, publisherAccount.EdgeId)
                                                                        },
                                                         Type = RecordType.PublisherAccount
                                                     });

                return;
            }

            _log.Warn($"Sync Disabled for PublisherAccount [{publisherAccount.DisplayName()}]");

            await _dynamoDb.PutItemTrackedInterlockedDeferAsync(publisherAccount,
                                                                pa =>
                                                                {
                                                                    pa.FailuresSinceLastSuccess++;
                                                                    pa.IsSyncDisabled = true;
                                                                },
                                                                RecordType.PublisherAccount);

            await NotifySyncDisabledAsync(publisherAccount);
        }

        private async Task DisableSyncAsync(DynPublisherAppAccount dynPublisherAppAccount, DynPublisherAccount specificTokenAccount)
        {
            if (dynPublisherAppAccount == null || specificTokenAccount == null)
            {
                return;
            }

            var isFirstFailure = false;

            await _dynamoDb.PutItemTrackedInterlockedAsync(dynPublisherAppAccount, paa =>
                                                                                   {
                                                                                       isFirstFailure = paa.FailuresSinceLastSuccess <= 0;

                                                                                       if (isFirstFailure)
                                                                                       { // Don't need to increment the fail count here, that is handled when things actually fail
                                                                                           // This can be called to simply ensure an account is disabled as well
                                                                                           paa.FailuresSinceLastSuccess++;
                                                                                       }

                                                                                       paa.LastFailedOn = _dateTimeProvider.UtcNowTs;

                                                                                       // Shadow accounts cannot be permanently disabled, they rely on the token of linked non-shadow accounts
                                                                                       paa.IsSyncDisabled = !paa.IsShadowAppAccont;

                                                                                       if (paa.IsShadowAppAccont)
                                                                                       {
                                                                                           paa.PubAccessToken = null;
                                                                                       }
                                                                                   });

            await _dynamoDb.PutItemTrackedInterlockedDeferAsync(specificTokenAccount,
                                                                pa =>
                                                                { // We also update the failures on last success for the token account used, helps with picking the best
                                                                    // one to use for syncing later
                                                                    pa.FailuresSinceLastSuccess++;
                                                                },
                                                                RecordType.PublisherAccount);

            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(dynPublisherAppAccount.PublisherAccountId);

            if (dynPublisherAppAccount.FailuresSinceLastSuccess <= 1 && specificTokenAccount.FailuresSinceLastSuccess <= 1 && isFirstFailure)
            {
                _log.Warn($"PublisherAppAccount Sync Disabled for PublisherAccount [{publisherAccount?.DisplayName() ?? "Unknown"}] with PublisherAppId [{dynPublisherAppAccount.PublisherAppId}]");
            }
        }

        private async Task NotifySyncDisabledAsync(DynPublisherAccount dynPublisherAccount, DynPublisherAccount tokenAccount = null)
        {
            if (dynPublisherAccount == null)
            {
                return;
            }

            var tokenAccountString = tokenAccount == null
                                         ? string.Empty
                                         : $@"
With specific token account : {tokenAccount.DisplayName()}
";

//             await _opsNotificationService.TrySendApiNotificationAsync($"Sync Disabled for PublisherAccount - {dynPublisherAccount.DisplayName()}",
//                                                                       $@"<https://app.datadoghq.com/logs?live=true&query=%40pubId%3A{dynPublisherAccount.PublisherAccountId}|Publisher Logs ({dynPublisherAccount.PublisherAccountId})>
// Is Token Account : {(dynPublisherAccount.IsTokenAccount() ? "Yes" : "No")}
// Failures : {dynPublisherAccount.FailuresSinceLastSuccess}
// {tokenAccountString}
//
// <https://app.datadoghq.com/logs?live=true&query=%40dto%3APostSyncRecentPublisherAccountMedia|View All Media Sync Logs>");

            if (!_sendRelinkNotifications)
            {
                return;
            }

            var notifyTitle = dynPublisherAccount.IsTokenAccount()
                                  ? $"Account {dynPublisherAccount.UserName} needs to be re-authenticated."
                                  : $"Account [{dynPublisherAccount.UserName}] needs to be re-linked.";

            var notifyMessage = dynPublisherAccount.IsTokenAccount()
                                    ? $"Please login to account {dynPublisherAccount.UserName} again to continue receiving stats."
                                    : $"Please re-link account [{dynPublisherAccount.UserName}] in your Rydr account to continue receiving stats.";

            await _serverNotificationService.NotifyAsync(new ServerNotification
                                                         {
                                                             From = null,
                                                             To = dynPublisherAccount.ToPublisherAccountInfo(),
                                                             ForRecord = new RecordTypeId(RecordType.PublisherAccount, dynPublisherAccount.PublisherAccountId),
                                                             ServerNotificationType = ServerNotificationType.AccountAttention,
                                                             Title = notifyTitle,
                                                             Message = notifyMessage
                                                         });
        }

        private void RemoveRecurringJob(long publisherAccountId)
            => _deferRequestsService.RemoveRecurringJob(PostSyncRecentPublisherAccountMedia.GetRecurringJobId(publisherAccountId));

        private async Task CheckMediaForPotentialDealCompletionAsync(DynPublisherMedia dynPublisherMedia, DynPublisherAccount influencerPublisherAccount)
        {
            // If this publisher is a creator, and they have any open dealRequests with business(es), see if the media we synced is a likely completion media for the request
            if (!influencerPublisherAccount.RydrAccountType.HasFlag(RydrAccountType.Influencer))
            {
                return;
            }

            // Go through each open request for this influencer and see if the content of the media contains any keywords, mentions, hashtags related directly to the business
            // the influencer has the open deal with or the requested content in the given deal...
            var notifyInProgressDealIds = new HashSet<long>();
            var notifyRedeemedDealIds = new HashSet<long>();

            await foreach (var dynDealRequest in _dealRequestService.GetPublisherAccountRequestsAsync(influencerPublisherAccount.PublisherAccountId, DealEnumHelpers.CompletableDealRequestStatuses))
            {
                var dynDeal = await _dealService.GetDealAsync(dynDealRequest.DealPublisherAccountId, dynDealRequest.DealId);

                if (dynDeal == null || dynDeal.IsDeleted() || !dynDeal.DealStatus.IsInfluencerCompletable())
                {
                    _deferRequestsService.DeferDealRequest(new DeleteDealRequestInternal
                                                           {
                                                               DealId = dynDealRequest.DealId,
                                                               PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                               Reason = "Deal was removed by business"
                                                           });

                    continue;
                }

                if (notifyRedeemedDealIds.Contains(dynDeal.DealId))
                {
                    continue;
                }

                var bizPublisherAccount = await _publisherAccountService.GetPublisherAccountAsync(dynDeal.PublisherAccountId);

                if (dynPublisherMedia.Caption.ContainsAnyOf(bizPublisherAccount.UserName, bizPublisherAccount.FullName))
                {
                    if (dynDealRequest.RequestStatus == DealRequestStatus.Redeemed)
                    {
                        notifyRedeemedDealIds.Add(dynDeal.DealId);
                        notifyInProgressDealIds.Remove(dynDeal.DealId);
                    }
                    else if (!notifyRedeemedDealIds.Contains(dynDeal.DealId))
                    {
                        notifyInProgressDealIds.Add(dynDeal.DealId);
                    }

                    continue;
                }

                if (dynDeal.ReceiveHashtagIds.IsNullOrEmpty())
                {
                    continue;
                }

                foreach (var receiveHashtagId in dynDeal.ReceiveHashtagIds)
                {
                    var hashtag = await _dynamoDb.GetItemByRefAsync<DynHashtag>(receiveHashtagId, receiveHashtagId.ToStringInvariant(), DynItemType.Hashtag, ignoreRecordNotFound: true);

                    if (hashtag == null)
                    {
                        continue;
                    }

                    if (dynPublisherMedia.Caption.Contains(hashtag.Name))
                    {
                        if (dynDealRequest.RequestStatus == DealRequestStatus.Redeemed)
                        {
                            notifyRedeemedDealIds.Add(dynDeal.DealId);
                            notifyInProgressDealIds.Remove(dynDeal.DealId);
                        }
                        else if (!notifyRedeemedDealIds.Contains(dynDeal.DealId))
                        {
                            notifyInProgressDealIds.Add(dynDeal.DealId);
                        }

                        break;
                    }
                }
            }

            if (notifyRedeemedDealIds.IsNullOrEmpty() && notifyInProgressDealIds.IsNullOrEmpty())
            {
                return;
            }

            var notifyDealRecordId = notifyRedeemedDealIds.Count == 1
                                         ? notifyRedeemedDealIds.Single()
                                         : notifyInProgressDealIds.Count == 1
                                             ? notifyInProgressDealIds.Single()
                                             : 0;

            using(var counterService = _counterAndListService.CreateStatefulInstance)
            {
                var notifyKey = string.Concat("urn:completionmediadetected.", influencerPublisherAccount.PublisherAccountId);
                Exception ex = null;

                try
                {
                    if (notifyDealRecordId > 0 && counterService.Exists(notifyKey, notifyDealRecordId.ToStringInvariant()))
                    {
                        return;
                    }

                    await _serverNotificationService.NotifyAsync(new ServerNotification
                                                                 {
                                                                     To = influencerPublisherAccount.ToPublisherAccountInfo(),
                                                                     ForRecord = notifyDealRecordId > 0
                                                                                     ? new RecordTypeId(RecordType.Deal, notifyDealRecordId)
                                                                                     : null,
                                                                     ServerNotificationType = ServerNotificationType.DealCompletionMediaDetected,
                                                                     Title = string.Concat(dynPublisherMedia.ContentType.ToString(), " detected"),
                                                                     Message = string.Concat(dynPublisherMedia.ContentType.ToString(), " detected. Tap here to complete your Pact")
                                                                 });
                }
                catch(Exception x)
                {
                    ex = x;

                    throw;
                }
                finally
                {
                    Try.Exec(() =>
                             {
                                 var notifiedDealCount = counterService.CountOfUniqueItems(notifyKey);

                                 if (notifiedDealCount > 350)
                                 {
                                     counterService.PopUniqueItems(notifyKey, (int)((notifiedDealCount - 300) + 150 + notifyRedeemedDealIds.Count + notifyInProgressDealIds.Count));
                                 }
                             });

                    if (ex == null)
                    {
                        notifyRedeemedDealIds.Each(did => counterService.AddUniqueItem(notifyKey, did.ToStringInvariant()));
                        notifyInProgressDealIds.Each(did => counterService.AddUniqueItem(notifyKey, did.ToStringInvariant()));
                    }
                }
            }
        }
    }
}
