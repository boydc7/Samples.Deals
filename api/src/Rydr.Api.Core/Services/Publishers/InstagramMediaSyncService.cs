using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Publishers
{
    public class InstagramMediaSyncService : BaseMediaSyncService
    {
        private const long _thirtyHoursInSeconds = 60 * 60 * 30;
        private static readonly bool _isSyncEnabled = RydrEnvironment.GetAppSetting("Instagram.Sync.Enabled", true);

        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly IEncryptionService _encryptionService;

        public InstagramMediaSyncService(IDeferRequestsService deferRequestsService, IPocoDynamo dynamoDb,
                                         IPublisherAccountService publisherAccountService,
                                         IServiceCacheInvalidator serviceCacheInvalidator,
                                         IEncryptionService encryptionService)
            : base(deferRequestsService, dynamoDb)
        {
            _publisherAccountService = publisherAccountService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
            _encryptionService = encryptionService;
        }

        public override PublisherType PublisherType => PublisherType.Instagram;

        public override async Task SyncUserDataAsync(SyncPublisherAppAccountInfo appAccount)
        {
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(appAccount.PublisherAccountId);

            var client = await GetOrCreateCheckedClientAsync(appAccount);

            await DoSyncUserDataAsync(publisherAccount, client);
        }

        public override async Task SyncRecentMediaAsync(SyncPublisherAppAccountInfo appAccount)
        {
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(appAccount.PublisherAccountId);

            var client = await GetOrCreateCheckedClientAsync(appAccount);

            await DoSyncUserDataAsync(publisherAccount, client);

            await TrySyncStep(publisherAccount, appAccount, () => SyncBasicIgMediaAsync(publisherAccount, client), "SyncBasicIgMediaAsync");
        }

        protected override async Task<List<long>> DoSyncMediaAsync(IEnumerable<string> igMediaIds, DynPublisherAppAccount publisherAppAccount, bool isCompletionMedia = false)
        {
            var results = new List<long>();

            if (!_isSyncEnabled || igMediaIds == null)
            {
                return results;
            }

            var client = await publisherAppAccount.GetOrCreateIgBasicClientAsync();

            foreach (var igMediaId in igMediaIds)
            {
                IgMedia igMedia = null;

                try
                {
                    igMedia = await client.GetBasicIgMediaAsync(igMediaId);
                }
                catch(FbApiException fbx)
                {
                    _log.Exception(fbx);
                }

                DynPublisherMedia dynMedia = null;
                var mediaExisted = false;

                if (igMedia == null)
                {
                    // NOTE: THIS IS CORRECTLY USING PubliserType.Facebook FOR THE PUBLISHERTYPE of the media - IT IS A FACEBOOK MEDIA, and the identifiers
                    // as it swaps from fb/ig and back are the same...
                    dynMedia = await _dynamoDb.GetItemByRefAsync<DynPublisherMedia>(publisherAppAccount.PublisherAccountId,
                                                                                    DynPublisherMedia.BuildRefId(PublisherType.Facebook, igMediaId),
                                                                                    DynItemType.PublisherMedia, true, true);

                    mediaExisted = dynMedia != null;
                }
                else
                {
                    (dynMedia, mediaExisted) = await GetDynMediaObjectAsync(igMedia, publisherAppAccount.PublisherAccountId);
                }

                if (dynMedia == null)
                {
                    continue;
                }

                // If the media is new, or needs to be updated to match completion status, do so
                if (!mediaExisted ||
                    (isCompletionMedia && (!dynMedia.IsCompletionMedia || dynMedia.ExpiresAt.GetValueOrDefault() > 0)))
                {
                    if (isCompletionMedia)
                    {
                        dynMedia.IsCompletionMedia = true;
                        dynMedia.ExpiresAt = null;
                    }

                    await _dynamoDb.TryPutItemMappedAsync(dynMedia, dynMedia.ReferenceId);
                }

                results.Add(dynMedia.PublisherMediaId);
            }

            return results;
        }

        private async Task<bool> SyncBasicIgMediaAsync(DynPublisherAccount publisherAccount, IInstagramBasicClient client)
        {
            if (!_isSyncEnabled || publisherAccount == null || publisherAccount.IsDeleted() ||
                publisherAccount.AccountType != PublisherAccountType.FbIgUser)
            {
                return false;
            }

            // If we've synced this account at all in the last 5.5 days-ish, get 100, otherwise get a bunch
            var isInitialSync = (DateTimeHelper.UtcNowTs - publisherAccount.LastMediaSyncedOn) >= 450000;

            var igMediaLimit = isInitialSync
                                   ? 3000
                                   : 100;

            var recentIgMedia = await client.GetBasicIgAccountMediaAsync()
                                            .SelectManyToListAsync(igMediaLimit);

            _log.DebugInfoFormat("  SyncBasicIgMediaAsync for PublisherAccount [{0}] received [{1}] medias from Facebook (IgMedia)", publisherAccount.DisplayName(), recentIgMedia?.Count ?? 0);

            if (recentIgMedia.IsNullOrEmpty())
            {
                return false;
            }

            var mediaSyncedMin = DateTimeHelper.UtcNow.Date.AddDays(-20).ToUnixTimestamp();
            var mediaCreatedMin = DateTimeHelper.UtcNow.Date.AddDays(PublisherMediaValues.DaysBackToKeepMedia).ToUnixTimestamp();
            var skippedMedia = 0;

            // Get dynamo objects that match the data we pulled from facebook (igbasic)
            // NOTE: THIS IS CORRECTLY USING PubliserType.Facebook FOR THE PUBLISHERTYPE of the media - IT IS A FACEBOOK MEDIA, and the identifiers
            // as it swaps from fb/ig and back are the same...
            var dynPublisherMediaMap = await _dynamoDb.GetItemsFromAsync<DynPublisherMedia, DynItemMap>(_dynamoDb.GetItemsAsync<DynItemMap>(recentIgMedia.Select(f => new DynamoId(publisherAccount.PublisherAccountId,
                                                                                                                                                                                   DynItemMap.BuildEdgeId(DynItemType.PublisherMedia, DynPublisherMedia.BuildRefId(PublisherType.Facebook, f.Id))))),
                                                                                                        m => m.GetMappedDynamoId())
                                                      .Where(dpm => dpm.DeletedOnUtc == null &&
                                                                    dpm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                    dpm.ContentType == PublisherContentType.Post &&
                                                                    dpm.PublisherType == PublisherType.Facebook &&
                                                                    dpm.MediaCreatedAt >= mediaCreatedMin)
                                                      .ToDictionarySafe(dpm => dpm.MediaId, StringComparer.OrdinalIgnoreCase)
                                       ?? new Dictionary<string, DynPublisherMedia>();

            foreach (var igMedia in recentIgMedia)
            {
                var mediaTimestamp = igMedia.Timestamp.ToDateTime(DateTimeHelper.MinApplicationDate, true).ToUnixTimestamp();

                if (mediaTimestamp < mediaCreatedMin)
                {
                    skippedMedia++;

                    continue;
                }

                DynPublisherMedia dynPublisherMedia = null;

                if (dynPublisherMediaMap.ContainsKey(igMedia.Id))
                {   // Already have the media, skip it if we've synced it somewhat recently, update the urls if not
                    dynPublisherMedia = dynPublisherMediaMap[igMedia.Id];

                    if (dynPublisherMedia.IsPermanentMedia || dynPublisherMedia.LastSyncedOn > mediaSyncedMin)
                    {
                        skippedMedia++;

                        continue;
                    }

                    // Update media urls....
                    var igMediaToSync = (igMedia.MediaUrl.IsNullOrEmpty()
                                             ? await client.GetBasicIgMediaAsync(igMedia.Id)
                                             : null) ?? igMedia;

                    if (igMediaToSync.MediaUrl.HasValue())
                    {
                        dynPublisherMedia.MediaUrl = igMediaToSync.MediaUrl;
                        dynPublisherMedia.ThumbnailUrl = igMediaToSync.ThumbnailUrl
                                                                      .Coalesce(igMedia.ThumbnailUrl)
                                                                      .Coalesce(dynPublisherMedia.ThumbnailUrl);
                        dynPublisherMedia.LastSyncedOn = DateTimeHelper.UtcNowTs;

                        await _dynamoDb.PutItemAsync(dynPublisherMedia);
                    }
                }
                else
                {   // Convert the igMedia for storage
                    var igMediaToSync = (igMedia.MediaUrl.IsNullOrEmpty()
                                             ? await client.GetBasicIgMediaAsync(igMedia.Id)
                                             : null) ?? igMedia;

                    dynPublisherMedia = igMediaToSync.ToDynPublisherMedia(publisherAccount.PublisherAccountId);

                    await _dynamoDb.TryPutItemMappedAsync(dynPublisherMedia, dynPublisherMedia.ReferenceId);
                }

                if (!isInitialSync)
                { // A new piece of media directly from fb (igbasic), process away
                    _deferRequestsService.DeferLowPriRequest(new PostPublisherMediaReceived
                                                             {
                                                                 PublisherAccountId = dynPublisherMedia.PublisherAccountId,
                                                                 PublisherMediaId = dynPublisherMedia.PublisherMediaId
                                                             }.WithAdminRequestInfo());
                }
            }

            if (skippedMedia > 0)
            {
                _log.DebugInfoFormat("  BasicIg MediaSync for PublisherAccount [{0}] skipped [{1}] medias that either already exist or are older than [{2}] days.",
                                     publisherAccount.DisplayName(), skippedMedia, PublisherMediaValues.DaysBackToKeepMedia);
            }

            await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(publisherAccount.Id, "publishermedia", "query");

            return true;
        }

        private async Task DoSyncUserDataAsync(DynPublisherAccount publisherAccount, IInstagramBasicClient client)
        {
            if (!_isSyncEnabled || publisherAccount == null || publisherAccount.IsDeleted() ||
                publisherAccount.AccountType != PublisherAccountType.FbIgUser)
            {
                return;
            }

            // Force a sync every 30-ish hours
            var honorEtag = (DateTimeHelper.UtcNowTs - publisherAccount.LastProfileSyncedOn) <= 110_000;

            var igUser = await client.GetMyAccountAsync(honorEtag);

            if (igUser != null)
            {
                await UpdatePublisherAccountAsync(publisherAccount, igUser);
            }
        }

        private async Task<(DynPublisherMedia DynMedia, bool Existed)> GetDynMediaObjectAsync(IgMedia igMedia, long publisherAccountId)
        {
            // NOTE: THIS IS CORRECTLY USING PubliserType.Facebook FOR THE PUBLISHERTYPE of the media - IT IS A FACEBOOK MEDIA, and the identifiers
            // as it swaps from fb/ig and back are the same...
            var dynPublisherMedia = await _dynamoDb.GetItemByRefAsync<DynPublisherMedia>(publisherAccountId,
                                                                                         DynPublisherMedia.BuildRefId(PublisherType.Facebook, igMedia.Id),
                                                                                         DynItemType.PublisherMedia, true, true);

            var existed = dynPublisherMedia != null;

            if (!existed)
            {
                dynPublisherMedia = igMedia.ToDynPublisherMedia(publisherAccountId);
            }

            return (dynPublisherMedia, existed);
        }

        private async Task<IInstagramBasicClient> GetOrCreateCheckedClientAsync(SyncPublisherAppAccountInfo syncPublisherAppAccountInfo)
        {
            var client = await syncPublisherAppAccountInfo.GetOrCreateIgBasicClientAsync();

            // If the current token is newer than the refresh-able age, use it...otherwise, refresh it and store
            var utcNow = DateTimeHelper.UtcNowTs;

            if (syncPublisherAppAccountInfo.PublisherAppAccount == null ||
                syncPublisherAppAccountInfo.PublisherAppAccount.TokenLastUpdated >= (utcNow - _thirtyHoursInSeconds))
            {
                return client;
            }

            var refreshedToken = await client.RefreshLongLivedAccessTokenAsync();

            if (refreshedToken != null && refreshedToken.IsValid())
            {
                syncPublisherAppAccountInfo.PublisherAppAccount.TokenLastUpdated = utcNow;
                syncPublisherAppAccountInfo.PublisherAppAccount.PubAccessToken = await _encryptionService.Encrypt64Async(refreshedToken.AccessToken);

                syncPublisherAppAccountInfo.PublisherAppAccount.ExpiresAt = refreshedToken.ExpiresInSeconds > 0
                                                                                ? utcNow + refreshedToken.ExpiresInSeconds
                                                                                : 0;

                syncPublisherAppAccountInfo.EncryptedAccessToken = syncPublisherAppAccountInfo.PublisherAppAccount.PubAccessToken;
                syncPublisherAppAccountInfo.RawAccessToken = refreshedToken.AccessToken;

                syncPublisherAppAccountInfo.TokenUpdated = true;
            }

            return client;
        }

        private async Task UpdatePublisherAccountAsync(DynPublisherAccount publisherAccount, IgAccount igUser)
            => await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount,
                                                                          pa =>
                                                                          {
                                                                              pa.UserName = igUser.UserName.Coalesce(pa.UserName);

                                                                              pa.Metrics ??= new Dictionary<string, double>();

                                                                              pa.Metrics[PublisherMetricName.Media] = igUser.MediaCount;
                                                                          });
    }
}
