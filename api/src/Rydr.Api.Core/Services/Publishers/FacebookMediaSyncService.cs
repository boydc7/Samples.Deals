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
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Publishers;
using Rydr.FbSdk;
using Rydr.FbSdk.Enums;
using Rydr.FbSdk.Models;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Publishers
{
    public class FacebookMediaSyncService : BaseMediaSyncService
    {
        private static readonly bool _isSyncEnabled = RydrEnvironment.GetAppSetting("Facebook.Sync.Enabled", true);

        private static readonly Dictionary<string, GenderType> _fbGenderNameMap = new Dictionary<string, GenderType>(StringComparer.OrdinalIgnoreCase)
                                                                                  {
                                                                                      {
                                                                                          "male", GenderType.Male
                                                                                      },
                                                                                      {
                                                                                          "m", GenderType.Male
                                                                                      },
                                                                                      {
                                                                                          "boy", GenderType.Male
                                                                                      },
                                                                                      {
                                                                                          "man", GenderType.Male
                                                                                      },
                                                                                      {
                                                                                          "he", GenderType.Male
                                                                                      },
                                                                                      {
                                                                                          "female", GenderType.Female
                                                                                      },
                                                                                      {
                                                                                          "f", GenderType.Female
                                                                                      },
                                                                                      {
                                                                                          "girl", GenderType.Female
                                                                                      },
                                                                                      {
                                                                                          "woman", GenderType.Female
                                                                                      },
                                                                                      {
                                                                                          "she", GenderType.Female
                                                                                      }
                                                                                  };

        private readonly IPublisherMediaStorageService _publisherMediaStorageService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;

        public FacebookMediaSyncService(IDeferRequestsService deferRequestsService, IPocoDynamo dynamoDb,
                                        IPublisherMediaStorageService publisherMediaStorageService,
                                        IPublisherAccountService publisherAccountService,
                                        IServiceCacheInvalidator serviceCacheInvalidator)
            : base(deferRequestsService, dynamoDb)
        {
            _publisherMediaStorageService = publisherMediaStorageService;
            _publisherAccountService = publisherAccountService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
        }

        public override PublisherType PublisherType => PublisherType.Facebook;

        public override async Task SyncUserDataAsync(SyncPublisherAppAccountInfo appAccount)
        {
            if (!_isSyncEnabled)
            {
                return;
            }

            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(appAccount.PublisherAccountId);

            if (publisherAccount == null || publisherAccount.IsDeleted() ||
                publisherAccount.AccountType != PublisherAccountType.User && publisherAccount.AccountType != PublisherAccountType.FbIgUser)
            { // Only User/IgUser accounts sync user profile data
                return;
            }

            var client = await appAccount.GetOrCreateFbClientAsync();

            // Force a sync every 30-ish hours
            var honorEtag = (DateTimeHelper.UtcNowTs - publisherAccount.LastProfileSyncedOn) <= 110_000;

            var fbUser = await client.GetUserAsync(honorEtag);

            if (fbUser != null)
            {
                await UpdatePublisherAccountAsync(publisherAccount, fbUser);
            }
        }

        public override async Task SyncRecentMediaAsync(SyncPublisherAppAccountInfo appAccount)
        {
            if (!_isSyncEnabled)
            {
                return;
            }

            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(appAccount.PublisherAccountId);

            if (publisherAccount.AccountType.IsUserAccount() && !publisherAccount.AccountType.IsSystemAccount())
            {
                await SyncUserDataAsync(appAccount);

                return;
            }

            if (publisherAccount.AccountType != PublisherAccountType.FbIgUser && publisherAccount.AccountType != PublisherAccountType.Page)
            { // Only IgUser/Page accounts sync user profile dataa
                return;
            }

            switch (publisherAccount.AccountType)
            {
                case PublisherAccountType.FbIgUser:
                    await SyncAllFgIgDataAsync(appAccount, publisherAccount);

                    return;

                default:
                    throw new ArgumentOutOfRangeException($"Unhandled PublisherAccountType [{publisherAccount.AccountType}] in FacebookMediaSync");
            }
        }

        private async Task SyncAllFgIgDataAsync(SyncPublisherAppAccountInfo appAccount, DynPublisherAccount publisherAccount)
        {
            Guard.AgainstArgumentOutOfRange(publisherAccount.AccountType != PublisherAccountType.FbIgUser, "FgIgUser sync can only occur for accounts of FbIgUser type");

            var client = await appAccount.GetOrCreateFbClientAsync();

            // NOTE: purposely not wrapped in a trySyncStep here - if this doesn't work, we jump out entirely
            await SyncFgIgUserDataAsync(publisherAccount, client);

            var mediaStats = (await TrySyncStep(publisherAccount, appAccount, () => SyncFbIgPostsAsync(publisherAccount, client, appAccount), "SyncFbIgPostsAsync")) ?? new List<DynPublisherMediaStat>();

            var mediaCount = mediaStats.Count;

            _log.DebugInfoFormat("  Received [{0}] post stats for PublisherAccount [{1}], PublisherAppId [{2}]", mediaCount, publisherAccount.DisplayName(), appAccount.PublisherAppId);

            mediaStats.AddRange((await TrySyncStep(publisherAccount, appAccount, () => SyncFgIgStoriesAsync(publisherAccount, client, appAccount), "SyncFgIgStoriesAsync")) ?? Enumerable.Empty<DynPublisherMediaStat>());

            _log.DebugInfoFormat("  Received [{0}] story stats for PublisherAccount [{1}], PublisherAppId [{2}]", mediaStats.Count - mediaCount, publisherAccount.DisplayName(), appAccount.PublisherAppId);

            // Finally, store all the medias
            if (!mediaStats.IsNullOrEmpty())
            {
                await _publisherMediaStorageService.StoreAsync(mediaStats);
            }

            await TrySyncStep(publisherAccount, appAccount, () => SyncFgIgUserDailyInsightsAsync(publisherAccount, client), "SyncFgIgUserDailyInsightsAsync");
            await TrySyncStep(publisherAccount, appAccount, () => SyncFgIgUserLifetimeInsightsAsync(publisherAccount, client), "SyncFgIgUserLifetimeInsightsAsync");

            await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(publisherAccount.Id, "publishermedia", "query");
        }

        private async Task<List<DynPublisherMediaStat>> SyncFgIgStoriesAsync(DynPublisherAccount publisherAccount, IFacebookClient client,
                                                                             SyncPublisherAppAccountInfo syncPublisherAppAccountInfo)
        {
            var recentIgStories = await client.GetFbIgAccountStoriesAsync(publisherAccount.AccountId)
                                              .TakeManyToListAsync(take: 500);

            _log.DebugInfoFormat("  SyncFgIgStoriesAsync for PublisherAccount [{0}] received [{1}] stories from Facebook", publisherAccount.DisplayName(), recentIgStories?.Count ?? 0);

            var mediaCreatedMin = DateTimeHelper.UtcNow.Date.AddDays(PublisherMediaValues.DaysBackToKeepMedia).ToUnixTimestamp();
            var liveStoryTimeLimit = DateTimeHelper.UtcNow.AddHours(-25).ToUnixTimestamp();

            // Get dynamo objects that either match the data we pulled from facebook or the most recent limit worth that we have (for if we get nothing back from
            // fb that's either because there is nothing there (and we will have nothing here either) or more likely we already have ETag-matching results
            // here for the request. Stories are only live at fb for 24 hours, so we only have to bother trying to get stats for stories created in the last 24ish hours if we got nothing back
            var dynPublisherStoriesMap = recentIgStories.IsNullOrEmpty()
                                             ? await _dynamoDb.FromQuery<DynPublisherMedia>(dpm => dpm.Id == publisherAccount.PublisherAccountId &&
                                                                                                   Dynamo.BeginsWith(dpm.EdgeId, "00"))
                                                              .Filter(dpm => dpm.DeletedOnUtc == null &&
                                                                             dpm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                             dpm.ContentType == PublisherContentType.Story &&
                                                                             dpm.PublisherType == PublisherType.Facebook &&
                                                                             dpm.MediaCreatedAt >= liveStoryTimeLimit &&
                                                                             dpm.PreBizAccountConversionMediaErrorCount <= 0)
                                                              .ExecAsync()
                                                              .Take(100)
                                                              .ToDictionarySafe(dpm => dpm.MediaId, StringComparer.OrdinalIgnoreCase)
                                               ?? new Dictionary<string, DynPublisherMedia>()
                                             : await _dynamoDb.GetItemsFromAsync<DynPublisherMedia, DynItemMap>(_dynamoDb.GetItemsAsync<DynItemMap>(recentIgStories.Select(f => new DynamoId(publisherAccount.PublisherAccountId,
                                                                                                                                                                                             DynItemMap.BuildEdgeId(DynItemType.PublisherMedia, DynPublisherMedia.BuildRefId(PublisherType.Facebook, f.Id))))),
                                                                                                                m => m.GetMappedDynamoId())
                                                              .Where(dpm => dpm.DeletedOnUtc == null &&
                                                                            dpm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                            dpm.ContentType == PublisherContentType.Story &&
                                                                            dpm.PublisherType == PublisherType.Facebook &&
                                                                            dpm.MediaCreatedAt >= mediaCreatedMin)
                                                              .ToDictionarySafe(dpm => dpm.MediaId, StringComparer.OrdinalIgnoreCase)
                                               ?? new Dictionary<string, DynPublisherMedia>();

            if (recentIgStories.IsNullOrEmpty() && dynPublisherStoriesMap.IsNullOrEmptyRydr())
            { // Received nothing from fb and have nothing here either, nuttin' to do
                return null;
            }

            var dynPublisherMediaStats = new List<DynPublisherMediaStat>(250);
            var fbApiExceptions = new List<FbApiException>();
            var countInsightsAttempted = 0;
            var countInsightsProcessed = 0;
            var skippedStories = 0;

            var mediaEnumerable = recentIgStories.IsNullOrEmpty()
                                      ? dynPublisherStoriesMap.Values
                                                              .OrderBy(m => m.MediaCreatedAt)
                                                              .ThenBy(m => m.Id)
                                      : recentIgStories.OrderBy(f => f.Timestamp.ToDateTime(DateTimeHelper.MinApplicationDate, true))
                                                       .ThenBy(f => f.Id)
                                                       .Select(f =>
                                                               {
                                                                   if (dynPublisherStoriesMap.ContainsKey(f.Id))
                                                                   {
                                                                       var dpm = dynPublisherStoriesMap[f.Id];

                                                                       UpdateDynMediaObject(dpm, f, PublisherContentType.Story);

                                                                       return dpm;
                                                                   }

                                                                   return f.ToDynPublisherMedia(publisherAccount.PublisherAccountId, PublisherContentType.Story);
                                                               });

            // OrderBy the created timestamp to align the edgeIds created with creation time...
            foreach (var dynPublisherMedia in mediaEnumerable)
            {
                if (dynPublisherMedia.PreBizAccountConversionMediaErrorCount >= 15 || dynPublisherMedia.MediaCreatedAt < mediaCreatedMin)
                {
                    skippedStories++;

                    continue;
                }

                // Get and store insight data for this media
                IReadOnlyList<FbIgMediaInsight> insights = null;
                countInsightsAttempted++;

                try
                {
                    // Substep inside syncAll for post insights....
                    if (SyncStepShouldTrySync(syncPublisherAppAccountInfo, "SyncFgIgStoriesAsync.Insights"))
                    {
                        insights = await client.GetFbIgMediaInsightsAsync(dynPublisherMedia.MediaId, dynPublisherMedia.MediaType, true)
                                               .TakeManyToListAsync(take: 1000);

                        if (dynPublisherMedia.PreBizAccountConversionMediaErrorCount > 0)
                        {
                            dynPublisherMedia.PreBizAccountConversionMediaErrorCount = 0;
                            dynPublisherMedia.Dirty();
                        }
                    }
                    else
                    {
                        _log.DebugInfoFormat($"FacebookSyncStep substep SyncFgIgStoriesAsync.Insights being skipped for PublisherAccount [{publisherAccount.DisplayName()}], due to step failure since token update");
                    }
                }
                catch(FbMediaCreatedBeforeBusinessConversion)
                {
                    dynPublisherMedia.PreBizAccountConversionMediaErrorCount++;
                    dynPublisherMedia.Dirty();
                }
                catch(FbApiException fbx)
                {
                    fbApiExceptions.Add(fbx);
                }
                finally
                { // No matter what happens with insight fetches, at least save the media if needed
                    if (dynPublisherMedia.IsDirty && dynPublisherMedia.MediaCreatedAt > mediaCreatedMin)
                    {
                        await _dynamoDb.TryPutItemMappedAsync(dynPublisherMedia, dynPublisherMedia.ReferenceId);
                    }
                }

                if (dynPublisherMedia.IsDirty && dynPublisherMedia.MediaCreatedAt > mediaCreatedMin &&
                    !recentIgStories.IsNullOrEmpty() && !dynPublisherStoriesMap.ContainsKey(dynPublisherMedia.MediaId))
                { // A new piece of media directly from fb...
                    _deferRequestsService.DeferLowPriRequest(new PostPublisherMediaReceived
                                                             {
                                                                 PublisherAccountId = dynPublisherMedia.PublisherAccountId,
                                                                 PublisherMediaId = dynPublisherMedia.PublisherMediaId
                                                             }.WithAdminRequestInfo());
                }

                if (!insights.IsNullOrEmptyReadOnly())
                {
                    countInsightsProcessed++;
                    dynPublisherMediaStats.AddRange(insights.ToDynPublisherMediaStats(dynPublisherMedia, mediaCreatedMin) ?? Enumerable.Empty<DynPublisherMediaStat>());
                }
            }

            if (skippedStories > 0)
            {
                _log.DebugInfoFormat("  SyncFgIgStoriesAsync for [{0}] skipped [{1}] stories older than [{2}] days.",
                                     publisherAccount.DisplayName(), skippedStories, PublisherMediaValues.DaysBackToKeepMedia);
            }

            if (fbApiExceptions.Count > 0)
            {
                var fbAggEx = new FbApiAggregateException(fbApiExceptions);

                // If we successfully received insights for some and we actually received a story from facebook directly, log and continue without throwing an exception
                if (countInsightsProcessed <= 0 &&
                    fbAggEx.Count >= countInsightsAttempted &&
                    countInsightsAttempted > 5)
                { // Received stuff from fb directly, but couldn't process any stats at all...
                    if (fbAggEx.IsApiStepPermissionError)
                    { // If we received stuff and couldn't process any of them at all, fail the step, log, and keep syncing everything else
                        UpdateSyncStepLastFailed(syncPublisherAppAccountInfo, "SyncFgIgStoriesAsync.Insights", false);
                    }
                    else
                    { // Something else, throw
                        throw fbAggEx;
                    }
                }

                _log.Warn($"  Partial failure syncing stories for PublisherAccount [{publisherAccount.DisplayName()}] - failed [{fbApiExceptions.Count}], succeeded [{dynPublisherMediaStats.Count}]. FbAggEx: [{fbAggEx}]");
            }
            else if (countInsightsProcessed > 0)
            {
                UpdateSyncStepLastFailed(syncPublisherAppAccountInfo, "SyncFgIgStoriesAsync.Insights", true);
            }

            return dynPublisherMediaStats;
        }

        private async Task<List<DynPublisherMediaStat>> SyncFbIgPostsAsync(DynPublisherAccount publisherAccount, IFacebookClient client,
                                                                           SyncPublisherAppAccountInfo syncPublisherAppAccountInfo)
        {
            const int minInsightsToProcess = 50;

            // If we've synced this account at all in the last 5.5 days-ish, get 100, otherwise get a bunch
            var isInitialSync = (DateTimeHelper.UtcNowTs - publisherAccount.LastMediaSyncedOn) >= 450000;

            var fbIgMediaLimit = isInitialSync
                                     ? 3000
                                     : 100;

            var recentFbIgMedia = await client.GetFbIgAccountMediaAsync(publisherAccount.AccountId)
                                              .TakeManyToListAsync(take: fbIgMediaLimit);

            _log.DebugInfoFormat("  SyncFbIgPostsAsync for PublisherAccount [{0}] received [{1}] medias from Facebook", publisherAccount.DisplayName(), recentFbIgMedia?.Count ?? 0);

            var mediaCreatedMin = DateTimeHelper.UtcNow.Date.AddDays(PublisherMediaValues.DaysBackToKeepMedia).ToUnixTimestamp();

            // Get dynamo objects that either match the data we pulled from facebook or the most recent limit worth that we have (for if we get nothing back from
            // fb that's either because there is nothing there (and we will have nothing here either) or more likely we already have ETag-matching results
            // here for the request
            var dynPublisherMediaMap = recentFbIgMedia.IsNullOrEmpty()
                                           ? await _dynamoDb.FromQuery<DynPublisherMedia>(dpm => dpm.Id == publisherAccount.PublisherAccountId &&
                                                                                                 Dynamo.BeginsWith(dpm.EdgeId, "00"))
                                                            .Filter(dpm => dpm.DeletedOnUtc == null &&
                                                                           dpm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                           dpm.ContentType == PublisherContentType.Post &&
                                                                           dpm.PublisherType == PublisherType.Facebook &&
                                                                           dpm.MediaCreatedAt >= mediaCreatedMin &&
                                                                           dpm.PreBizAccountConversionMediaErrorCount <= 0)
                                                            .ExecAsync()
                                                            .Take(fbIgMediaLimit)
                                                            .ToDictionarySafe(m => m.MediaId, StringComparer.OrdinalIgnoreCase)
                                             ?? new Dictionary<string, DynPublisherMedia>()
                                           : await _dynamoDb.GetItemsFromAsync<DynPublisherMedia, DynItemMap>(_dynamoDb.GetItemsAsync<DynItemMap>(recentFbIgMedia.Select(f => new DynamoId(publisherAccount.PublisherAccountId,
                                                                                                                                                                                           DynItemMap.BuildEdgeId(DynItemType.PublisherMedia, DynPublisherMedia.BuildRefId(PublisherType.Facebook, f.Id))))),
                                                                                                              m => m.GetMappedDynamoId())
                                                            .Where(dpm => dpm.DeletedOnUtc == null &&
                                                                          dpm.TypeId == (int)DynItemType.PublisherMedia &&
                                                                          dpm.ContentType == PublisherContentType.Post &&
                                                                          dpm.PublisherType == PublisherType.Facebook &&
                                                                          dpm.MediaCreatedAt >= mediaCreatedMin)
                                                            .ToDictionarySafe(dpm => dpm.MediaId, StringComparer.OrdinalIgnoreCase)
                                             ?? new Dictionary<string, DynPublisherMedia>();

            if (recentFbIgMedia.IsNullOrEmpty() && dynPublisherMediaMap.IsNullOrEmptyRydr())
            { // Received nothing from fb and have nothing here either, nuttin' to do
                return null;
            }

            var dynPublisherMediaStats = new List<DynPublisherMediaStat>(fbIgMediaLimit * 2);
            var fbApiExceptions = new List<FbApiException>();
            var countInsightsAttempted = 0;
            var countInsightsProcessed = 0;
            var totalActions = 0L;
            var totalComments = 0L;
            var skippedMedia = 0;

            var mediaEnumerable = recentFbIgMedia.IsNullOrEmpty()
                                      ? dynPublisherMediaMap.Values
                                                            .OrderBy(m => m.MediaCreatedAt)
                                                            .ThenBy(m => m.Id)
                                      : recentFbIgMedia.OrderBy(f => f.Timestamp.ToDateTime(DateTimeHelper.MinApplicationDate, true))
                                                       .ThenBy(f => f.Id)
                                                       .Select(f =>
                                                               {
                                                                   if (dynPublisherMediaMap.ContainsKey(f.Id))
                                                                   {
                                                                       var dpm = dynPublisherMediaMap[f.Id];

                                                                       UpdateDynMediaObject(dpm, f, PublisherContentType.Post);

                                                                       return dpm;
                                                                   }

                                                                   return f.ToDynPublisherMedia(publisherAccount.PublisherAccountId, PublisherContentType.Post);
                                                               });

            // Go through each media pulled - we store media for the last x days or so, but we process insights for a certain
            // number (to get engagement ratings). So we go through and store media based on the time limit, and process stats
            // based on count OR time limit (i.e. if a piece of media is after the time limit, it's stored and calculated - if it
            // is before the time limit but we have't yet counted enough, it is not stored but we get insights to calc)
            // We order by the media timestamp ASCENDING so the edgeIds on the DynPublisherMedia object is basically aligned
            foreach (var dynPublisherMedia in mediaEnumerable)
            {
                if (dynPublisherMedia.PreBizAccountConversionMediaErrorCount >= 15 ||
                    (dynPublisherMedia.MediaCreatedAt < mediaCreatedMin && countInsightsProcessed >= minInsightsToProcess))
                {
                    skippedMedia++;

                    continue;
                }

                countInsightsAttempted++;
                totalActions += dynPublisherMedia.ActionCount;
                totalComments += dynPublisherMedia.CommentCount;

                // Get and store insight data for this media
                IReadOnlyList<FbIgMediaInsight> insights = null;

                try
                { // Substep inside syncAll for post insights....
                    if (SyncStepShouldTrySync(syncPublisherAppAccountInfo, "SyncFbIgPostsAsync.Insights"))
                    {
                        insights = await client.GetFbIgMediaInsightsAsync(dynPublisherMedia.MediaId, dynPublisherMedia.MediaType, false)
                                               .TakeManyToListAsync(take: 3000); // The 3000 here is just a high place holder, we get a set # of stats, like dozens, for lifetime only on this endpoint

                        if (dynPublisherMedia.PreBizAccountConversionMediaErrorCount > 0)
                        {
                            dynPublisherMedia.PreBizAccountConversionMediaErrorCount = 0;
                            dynPublisherMedia.Dirty();
                        }
                    }
                    else
                    {
                        _log.DebugInfoFormat($"FacebookSyncStep substep SyncFbIgPostsAsync.Insights being skipped for PublisherAccount [{publisherAccount.DisplayName()}], due to step failure since token update");
                    }
                }
                catch(FbMediaCreatedBeforeBusinessConversion)
                {
                    dynPublisherMedia.PreBizAccountConversionMediaErrorCount++;
                    dynPublisherMedia.Dirty();
                }
                catch(FbApiException fbx)
                {
                    fbApiExceptions.Add(fbx);
                }
                finally
                { // No matter what happens with insight fetches, at least save the media if needed
                    if (dynPublisherMedia.IsDirty && (isInitialSync || dynPublisherMedia.MediaCreatedAt > mediaCreatedMin))
                    {
                        await _dynamoDb.TryPutItemMappedAsync(dynPublisherMedia, dynPublisherMedia.ReferenceId);
                    }
                }

                if (!isInitialSync &&
                    dynPublisherMedia.IsDirty && dynPublisherMedia.MediaCreatedAt > mediaCreatedMin &&
                    !recentFbIgMedia.IsNullOrEmpty() &&
                    !dynPublisherMediaMap.ContainsKey(dynPublisherMedia.MediaId))
                { // A new piece of media directly from fb...
                    _deferRequestsService.DeferLowPriRequest(new PostPublisherMediaReceived
                                                             {
                                                                 PublisherAccountId = dynPublisherMedia.PublisherAccountId,
                                                                 PublisherMediaId = dynPublisherMedia.PublisherMediaId
                                                             }.WithAdminRequestInfo());
                }

                if (!insights.IsNullOrEmptyReadOnly())
                {
                    countInsightsProcessed++;
                    dynPublisherMediaStats.AddRange(insights.ToDynPublisherMediaStats(dynPublisherMedia, mediaCreatedMin) ?? Enumerable.Empty<DynPublisherMediaStat>());
                }
            }

            if (skippedMedia > 0)
            {
                _log.DebugInfoFormat("  Facebook MediaSync for PublisherAccount [{0}] reached [{1}] media limit and skipped [{2}] medias older than [{3}] days.",
                                     publisherAccount.DisplayName(), minInsightsToProcess, skippedMedia, PublisherMediaValues.DaysBackToKeepMedia);
            }

            if (publisherAccount.Metrics == null ||
                (!publisherAccount.Metrics.ContainsKey(PublisherMetricName.RecentLikes) ||
                 Math.Abs(publisherAccount.Metrics[PublisherMetricName.RecentLikes] - totalActions) >= 1) ||
                (!publisherAccount.Metrics.ContainsKey(PublisherMetricName.RecentComments) ||
                 Math.Abs(publisherAccount.Metrics[PublisherMetricName.RecentComments] - totalComments) >= 1))
            {
                publisherAccount = await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount,
                                                                                              pa =>
                                                                                              {
                                                                                                  if (pa.Metrics == null)
                                                                                                  {
                                                                                                      pa.Metrics = new Dictionary<string, double>();
                                                                                                  }

                                                                                                  pa.Metrics[PublisherMetricName.RecentLikes] = totalActions;
                                                                                                  pa.Metrics[PublisherMetricName.RecentComments] = totalComments;
                                                                                              });
            }

            if (fbApiExceptions.Count > 0)
            {
                var fbAggEx = new FbApiAggregateException(fbApiExceptions);

                // If we successfully received insights for some and we actually received a story from facebook directly, and they all failed,
                // and we tried more than a handfull...
                if (countInsightsProcessed <= 0 &&
                    fbAggEx.Count >= countInsightsAttempted &&
                    countInsightsAttempted > 5)
                { // Couldn't process any stats at all...
                    if (fbAggEx.IsApiStepPermissionError)
                    { // If we received stuff and couldn't process any of them at all, fail the step, log, and keep syncing everything else
                        UpdateSyncStepLastFailed(syncPublisherAppAccountInfo, "SyncFbIgPostsAsync.Insights", false);
                    }
                    else
                    { // Something else, throw
                        throw fbAggEx;
                    }
                }

                _log.Warn($"  Partial failure syncing posts for PublisherAccount [{publisherAccount.DisplayName()}] - failed [{fbApiExceptions.Count}], succeeded [{dynPublisherMediaStats.Count}]. FbAggEx: [{fbAggEx}]");
            }
            else if (countInsightsProcessed > 0)
            {
                UpdateSyncStepLastFailed(syncPublisherAppAccountInfo, "SyncFbIgPostsAsync.Insights", true);
            }

            return dynPublisherMediaStats;
        }

        protected override async Task<List<long>> DoSyncMediaAsync(IEnumerable<string> fbMediaIds, DynPublisherAppAccount publisherAppAccount, bool isCompletionMedia = false)
        {
            var results = new List<long>();

            if (!_isSyncEnabled || fbMediaIds == null)
            {
                return results;
            }

            var client = await publisherAppAccount.GetOrCreateFbClientAsync();

            foreach (var fbMediaId in fbMediaIds)
            {
                FbIgMedia fbIgMedia = null;

                try
                {
                    fbIgMedia = await client.GetFbIgMediaAsync(fbMediaId);
                }
                catch(FbApiException fbx)
                {
                    _log.Exception(fbx);
                }

                DynPublisherMedia dynMedia = null;
                var mediaExisted = false;

                if (fbIgMedia == null)
                {
                    dynMedia = await _dynamoDb.GetItemByRefAsync<DynPublisherMedia>(publisherAppAccount.PublisherAccountId,
                                                                                    DynPublisherMedia.BuildRefId(PublisherType.Facebook, fbMediaId),
                                                                                    DynItemType.PublisherMedia, true, true);

                    mediaExisted = dynMedia != null;
                }
                else
                {
                    (dynMedia, mediaExisted) = await GetDynMediaObjectAsync(fbIgMedia, publisherAppAccount.PublisherAccountId, PublisherContentType.Unknown);
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

        private async Task<(DynPublisherMedia DynMedia, bool Existed)> GetDynMediaObjectAsync(FbIgMedia fbIgMedia, long publisherAccountId, PublisherContentType contentType)
        {
            var dynPublisherMedia = await _dynamoDb.GetItemByRefAsync<DynPublisherMedia>(publisherAccountId,
                                                                                         DynPublisherMedia.BuildRefId(PublisherType.Facebook, fbIgMedia.Id),
                                                                                         DynItemType.PublisherMedia, true, true);

            var existed = dynPublisherMedia != null;

            if (existed)
            {
                UpdateDynMediaObject(dynPublisherMedia, fbIgMedia, contentType);
            }
            else
            {
                dynPublisherMedia = fbIgMedia.ToDynPublisherMedia(publisherAccountId, contentType);
            }

            return (dynPublisherMedia, existed);
        }

        private void UpdateDynMediaObject(DynPublisherMedia dynPublisherMedia, FbIgMedia withFbIgMedia, PublisherContentType contentType)
        {
            if (dynPublisherMedia == null)
            {
                return;
            }

            if (dynPublisherMedia.ActionCount != withFbIgMedia.LikeCount)
            {
                dynPublisherMedia.ActionCount = withFbIgMedia.LikeCount;
                dynPublisherMedia.Dirty();
            }

            if (dynPublisherMedia.CommentCount != withFbIgMedia.CommentsCount)
            {
                dynPublisherMedia.CommentCount = withFbIgMedia.CommentsCount;
                dynPublisherMedia.Dirty();
            }

            if (contentType != PublisherContentType.Unknown &&
                (dynPublisherMedia.ContentType == PublisherContentType.Unknown || dynPublisherMedia.ContentType != contentType))
            {
                dynPublisherMedia.ContentType = contentType;
                dynPublisherMedia.Dirty();
            }

            if (!dynPublisherMedia.IsPermanentMedia &&
                dynPublisherMedia.LastSyncedOn < (DateTimeHelper.UtcNow.AddDays(-20).ToUnixTimestamp()))
            {
                dynPublisherMedia.MediaUrl = withFbIgMedia.MediaUrl;
                dynPublisherMedia.ThumbnailUrl = withFbIgMedia.ThumbnailUrl;
                dynPublisherMedia.LastSyncedOn = DateTimeHelper.UtcNowTs;
                dynPublisherMedia.Dirty();
            }

            dynPublisherMedia.UpdateDateTimeTrackedValues();
        }

        private async Task SyncFgIgUserDataAsync(DynPublisherAccount publisherAccount, IFacebookClient client)
        {
            // Force a sync every 30-ish hours
            var honorEtag = (DateTimeHelper.UtcNowTs - publisherAccount.LastProfileSyncedOn) <= 110_000;

            var fbIgUser = await client.GetFbIgBusinessAccountAsync(publisherAccount.AccountId, honorEtag);

            if (fbIgUser == null)
            {
                return;
            }

            publisherAccount = await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount,
                                                                                          pa =>
                                                                                          {
                                                                                              pa.FullName = fbIgUser.Name.Coalesce(pa.FullName);
                                                                                              pa.Description = fbIgUser.Description.Coalesce(pa.Description);
                                                                                              pa.UserName = fbIgUser.UserName.Coalesce(pa.UserName);
                                                                                              pa.Website = fbIgUser.Website.Coalesce(pa.Website);
                                                                                              pa.ProfilePicture = fbIgUser.ProfilePictureUrl.Coalesce(pa.ProfilePicture);

                                                                                              if (fbIgUser.InstagramId.HasValue())
                                                                                              {
                                                                                                  pa.AlternateAccountId = fbIgUser.InstagramId;
                                                                                              }

                                                                                              if (pa.Metrics == null)
                                                                                              {
                                                                                                  pa.Metrics = new Dictionary<string, double>();
                                                                                              }

                                                                                              pa.Metrics[PublisherMetricName.Media] = fbIgUser.MediaCount;
                                                                                              pa.Metrics[PublisherMetricName.Follows] = fbIgUser.FollowsCount;
                                                                                              pa.Metrics[PublisherMetricName.FollowedBy] = fbIgUser.FollowersCount;
                                                                                          });

            _deferRequestsService.DeferLowPriRequest(new ProcessPublisherAccountProfilePic
                                                     {
                                                         PublisherAccountId = publisherAccount.PublisherAccountId,
                                                         ProfilePicKey = string.Concat("ig/", publisherAccount.IsSoftLinked
                                                                                                  ? publisherAccount.AlternateAccountId
                                                                                                                    .Coalesce(publisherAccount.AccountId)
                                                                                                  : publisherAccount.AccountId)
                                                     }.WithAdminRequestInfo());
        }

        private async Task<bool> SyncFgIgUserLifetimeInsightsAsync(DynPublisherAccount publisherAccount, IFacebookClient client)
        {
            _log.DebugInfoFormat("  Processing lifetime user insights for PublisherAccount [{0}]", publisherAccount.DisplayName());

            var countReceived = 0;
            var statMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            // Take each complex insight and create a separate stat metric for each inner complex value in the KVP
            // returned as the value, creating a separate metric for each of the inner stats
            await foreach (var fbMediaInsightsBatch in client.GetFbIgUserLifetimeInsightsAsync(publisherAccount.AccountId))
            {
                foreach (var fbMediaInsight in fbMediaInsightsBatch.Where(fbmi => fbmi != null &&
                                                                                  !fbmi.Values.IsNullOrEmpty() &&
                                                                                  !fbmi.Values.First().Value.IsNullOrEmptyRydr()))
                {
                    countReceived++;

                    fbMediaInsight.Values.First().Value
                                  .Select(kvp => (Key: string.Concat(fbMediaInsight.Name,
                                                                     "|",
                                                                     kvp.Key.Replace(" ", "_")),
                                                  Value: (double)kvp.Value))
                                  .Each(t => statMap[t.Key] = t.Value);

                    if (countReceived >= 5000)
                    {
                        break;
                    }
                }
            }

            await statMap.ProcessDailyStatsAsync<DynDailyStatSnapshot>(publisherAccount.Id, RecordType.DailyStatSnapshot);

            _log.DebugInfoFormat("    Received [{0}] lifetime insight values, stored [{1}] daily snapshots for PublisherAccount [{2}]",
                                 countReceived, statMap.Count, publisherAccount.DisplayName());

            return true;
        }

        private async Task<bool> SyncFgIgUserDailyInsightsAsync(DynPublisherAccount publisherAccount, IFacebookClient client)
        {
            _log.DebugInfoFormat("  Processing daily user insights for PublisherAccount [{0}]", publisherAccount.DisplayName());

            var minDayToKeep = DateTimeHelper.UtcNow.Date.AddDays(PublisherMediaValues.DaysBackToKeepMedia).ToUnixTimestamp();
            var countReceived = 0;
            var countStored = 0;

            await foreach (var fbMediaInsightsBatch in client.GetFbIgUserDailyInsightsAsync(publisherAccount.AccountId,
                                                                                            publisherAccount.LastMediaSyncedOn > 0
                                                                                                ? 1
                                                                                                : 30))
            {
                foreach (var fbMediaInsight in fbMediaInsightsBatch.Where(fbmi => fbmi != null && !fbmi.Values.IsNullOrEmpty()))
                {
                    countReceived++;

                    foreach (var metricTuple in fbMediaInsight.Values.Select(v => (fbMediaInsight.Name,
                                                                                   Date: v.EndTime
                                                                                          .ToDateTimeNullable(convertToUtcVsGuarding: true)?
                                                                                          .ToUnixTimestamp() ?? 0,
                                                                                   v.Value))
                                                              .Where(t => t.Date > minDayToKeep)
                                                              .GroupBy(t => t.Date)
                                                              .Select(g => (Date: g.Key,
                                                                            Metrics: g.ToDictionary(gm => gm.Name, gm => (double)gm.Value))))
                    {
                        countStored++;

                        await metricTuple.Metrics
                                         .ProcessDailyStatsAsync<DynDailyStat>(publisherAccount.Id, RecordType.DailyStat, metricTuple.Date.ToDateTime());
                    }

                    if (countReceived >= 5000)
                    {
                        break;
                    }
                }
            }

            _log.DebugInfoFormat("    Received [{0}] daily insight values, stored [{1}] daily stats for PublisherAccount [{2}]",
                                 countReceived, countStored, publisherAccount.DisplayName());

            return true;
        }

        private async Task UpdatePublisherAccountAsync(DynPublisherAccount publisherAccount, FbUser fbUser)
        {
            await _publisherAccountService.UpdatePublisherAccountAsync(publisherAccount,
                                                                       pa =>
                                                                       {
                                                                           pa.FullName = fbUser.Name.Coalesce(pa.FullName);
                                                                           pa.UserName = fbUser.ShortName.Coalesce(pa.UserName);

                                                                           if (pa.Email.IsNullOrEmpty() && fbUser.Email.HasValue())
                                                                           {
                                                                               pa.Email = fbUser.Email;
                                                                           }

                                                                           if ((fbUser.Picture?.Data?.Url).HasValue())
                                                                           {
                                                                               pa.ProfilePicture = fbUser.Picture.Data.Url;
                                                                           }

                                                                           if (fbUser.AgeRange != null && (fbUser.AgeRange.Min > 0 || fbUser.AgeRange.Max > 0))
                                                                           {
                                                                               pa.AgeRangeMin = fbUser.AgeRange.Min.Gz(pa.AgeRangeMin);

                                                                               pa.AgeRangeMax = fbUser.AgeRange.Max.Gz(pa.AgeRangeMax).Gz(pa.AgeRangeMin > 20
                                                                                                                                              ? 100
                                                                                                                                              : 20);
                                                                           }

                                                                           if (fbUser.Gender.HasValue())
                                                                           {
                                                                               pa.Gender = _fbGenderNameMap.ContainsKey(fbUser.Gender)
                                                                                               ? _fbGenderNameMap[fbUser.Gender]
                                                                                               : GenderType.Other;
                                                                           }
                                                                       });

            _deferRequestsService.DeferLowPriRequest(new ProcessPublisherAccountProfilePic
                                                     {
                                                         PublisherAccountId = publisherAccount.PublisherAccountId,
                                                         ProfilePicKey = string.Concat("ig/", publisherAccount.IsSoftLinked
                                                                                                  ? publisherAccount.AlternateAccountId
                                                                                                                    .Coalesce(publisherAccount.AccountId)
                                                                                                  : publisherAccount.AccountId)
                                                     }.WithAdminRequestInfo());
        }
    }
}
