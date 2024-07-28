using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack.Caching;
using ServiceStack.Messaging;
using ServiceStack.OrmLite.Dapper;
using ServiceStack.Redis;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Rydr.Api.Services.Services;

public class MonitorServiceAdmin : BaseAdminApiService
{
    private readonly bool _useRedisMq = RydrEnvironment.GetAppSetting("Messaging.UseRedisMq", false);

    private static readonly TimeSpan _lastModifiedPeriodBuffer = TimeSpan.FromMinutes(BaseMediaSyncService.PublisherAccountSyncIntervalMinutes + 100);
    private static readonly TimeSpan _lastSyncPeriodBuffer = TimeSpan.FromMinutes((BaseMediaSyncService.PublisherAccountSyncIntervalMinutes * 2) + 15);

    private static readonly IPublisherMediaSyncService _facebookPublisherMediaSyncService = RydrEnvironment.Container.ResolveNamed<IPublisherMediaSyncService>(PublisherType.Facebook.ToString());
    private static readonly IRedisClientsManager _redisClientsManager = RydrEnvironment.Container.TryResolve<IRedisClientsManager>();

    private readonly IAssociationService _associationService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IOpsNotificationService _opsNotificationService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly ICacheClient _cacheClient;
    private readonly IDistributedLockService _distributedLockService;

    public MonitorServiceAdmin(IAssociationService associationService,
                               IRydrDataService rydrDataService,
                               IOpsNotificationService opsNotificationService,
                               IPublisherAccountService publisherAccountService,
                               IDeferRequestsService deferRequestsService,
                               ICacheClient cacheClient,
                               IDistributedLockService distributedLockService)
    {
        _associationService = associationService;
        _rydrDataService = rydrDataService;
        _opsNotificationService = opsNotificationService;
        _publisherAccountService = publisherAccountService;
        _deferRequestsService = deferRequestsService;
        _cacheClient = cacheClient;
        _distributedLockService = distributedLockService;
    }

    public async Task Post(MonitorSystemResources request)
    {
        using(var lockItem = _distributedLockService.TryGetKeyLock(nameof(MonitorSystemResources), nameof(MonitorServiceAdmin), 600))
        {
            if (lockItem == null)
            {
                return;
            }

            var systemResourcesLastMonitoredOnItem = _cacheClient.TryGet(nameof(MonitorSystemResources), () => new Int64Id(), CacheConfig.LongConfig) ?? new Int64Id();

            if (!request.Force && (_dateTimeProvider.UtcNowTs - systemResourcesLastMonitoredOnItem.Id) < 900)
            {
                return;
            }

            // Monitor things, set the last time in cache, return
            await VerifyFacebookPublisherSyncJobsAsync();

            await ProcessDlqsAsync();

            _deferRequestsService.DeferLowPriRequest(new MonitorRequestNotifications().WithAdminRequestInfo());
            _deferRequestsService.DeferLowPriRequest(new MonitorRequestAllowances().WithAdminRequestInfo());

            systemResourcesLastMonitoredOnItem.Id = _dateTimeProvider.UtcNowTs;

            await _cacheClient.TrySetAsync(systemResourcesLastMonitoredOnItem, nameof(MonitorSystemResources), CacheConfig.LongConfig);
        }
    }

    private async Task ProcessDlqsAsync()
    {
        if (_useRedisMq && _redisClientsManager != null)
        {
            using(var redisClient = _redisClientsManager.GetClient())
            {
                // We do not re-process anything in the syncMedia DLQ, just clear the key so it doesn't take up space
                var syncMediaDlqName = QueueNames<PostSyncRecentPublisherAccountMedia>.Dlq;
                redisClient.RemoveEntry(syncMediaDlqName);
            }
        }

        var registeredMqTypeNames = RydrEnvironment.Container.TryResolveNamed<List<string>>("MessageQueueProcessorRegisteredTypeNames");

        if (registeredMqTypeNames.IsNullOrEmpty())
        {
            return;
        }

        foreach (var registeredMqTypeName in registeredMqTypeNames)
        {
            var processor = RydrEnvironment.Container.TryResolveNamed<IMessageQueueProcessor>(registeredMqTypeName);

            if (processor == null)
            {
                _log.WarnFormat("ProcessDlqsAsync could not find processor for registered MqTypeName of [{0}]", registeredMqTypeName);

                continue;
            }

            await processor.ReprocessDlqAsync(new MqRetry
                                              {
                                                  TypeName = registeredMqTypeName,
                                                  Limit = 1000
                                              });
        }
    }

    private async Task VerifyFacebookPublisherSyncJobsAsync()
    {
        var now = DateTimeHelper.UtcNow;
        var minLastSyncedOn = now - _lastSyncPeriodBuffer;

        var publisherAccountIdsToSync = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<Int64Id>(@"
SELECT   DISTINCT pa.Id AS Id
FROM     PublisherAccounts pa
WHERE    pa.IsSyncDisabled = 0
         AND pa.DeletedOn IS NULL
         AND pa.PublisherType IN (1,2)
         AND pa.AccountType > 1
         AND pa.LastMediaSyncedOn IS NOT NULL
         AND pa.LastMediaSyncedOn <= @MinSyncedOn
         AND
         (
	         pa.FailuresSinceLastSuccess <= 1
             OR
             pa.ModifiedOn <= @MinModifiedOn
         )
         AND EXISTS
         (
            SELECT	NULL
            FROM    PublisherAccountLinks pal
            JOIN    PublisherAccounts pat
            ON      pal.FromPublisherAccountId = pat.Id
            WHERE   pal.DeletedOn IS NULL
                    AND pal.ToPublisherAccountId = pa.Id
                    AND pat.DeletedOn IS NULL
                    AND pat.IsSyncDisabled <= 0
                    AND pat.PublisherType = 1
                    AND pat.AccountType = 1
            UNION ALL
            SELECT	NULL
            FROM    PublisherAccountLinks palt
            WHERE   palt.DeletedOn IS NULL
                    AND palt.ToPublisherAccountId = pa.Id
                    AND palt.FromPublisherAccountId = 0
         );
",
                                                                                                            new
                                                                                                            {
                                                                                                                MinSyncedOn = minLastSyncedOn,
                                                                                                                MinModifiedOn = (now - _lastModifiedPeriodBuffer)
                                                                                                            }));

        if (publisherAccountIdsToSync == null)
        {
            return;
        }

        var deferAffectedPublisherAccountIds = new List<long>();
        var opsNotificationMinSyncTime = now - (TimeSpan.FromMinutes(_lastSyncPeriodBuffer.TotalMinutes * 5));
        var countAlerted = 0;

        foreach (var publisherAccountId in publisherAccountIdsToSync.Where(l => l.Id > 0)
                                                                    .Select(l => l.Id)
                                                                    .Distinct())
        {
            var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(publisherAccountId);

            if (publisherAccount == null || publisherAccount.IsDeleted())
            {
                _deferRequestsService.DeferRequest(new DeletePublisherAccountInternal
                                                   {
                                                       PublisherAccountId = publisherAccountId
                                                   }.WithAdminRequestInfo());

                continue;
            }

            var publisherAccountLastSyncedOn = publisherAccount.LastMediaSyncedOn.ToDateTime();

            // If this account is sync-disabled, or if the actual last sync time is valid, defer this account for updating in the db and continue
            if (publisherAccount.IsSyncDisabled || publisherAccountLastSyncedOn > minLastSyncedOn)
            {
                deferAffectedPublisherAccountIds.Add(publisherAccount.PublisherAccountId);

                continue;
            }

            var linkageType = await PublisherAccountHasValidLinkageAsync(publisherAccount);

            if (linkageType == PublisherLinkType.None)
            { // No valid linkage, nothing to do
                continue;
            }

            if (linkageType == PublisherLinkType.Basic && !publisherAccount.IsBasicLink && !publisherAccount.IsSoftLinked)
            { // If have only basic valid linkage but the publisher is not a basic publisher, convert to basic now...
                _deferRequestsService.DeferLowPriRequest(new PublisherAccountDownConvert
                                                         {
                                                             PublisherAccountId = publisherAccount.PublisherAccountId
                                                         }.WithAdminRequestInfo());

                continue;
            }

            _log.WarnFormat("Restarting MediaSync recurring job for PublisherAccount [{0}] due to stale execution", publisherAccount.DisplayName());

            await _facebookPublisherMediaSyncService.AddOrUpdateMediaSyncAsync(publisherAccountId);

            // Send a notification if we're way behind
            if (publisherAccountLastSyncedOn < opsNotificationMinSyncTime && countAlerted < 3)
            {
                countAlerted++;

                await _opsNotificationService.TrySendApiNotificationAsync($"Restarting MediaSync Job - {publisherAccount.DisplayName()}",
                                                                          $@"<https://app.datadoghq.com/logs?live=true&query=%40pubId%3A{publisherAccount.PublisherAccountId}|Publisher Logs ({publisherAccount.PublisherAccountId})>
<https://app.datadoghq.com/logs?live=true&query=%40dto%3APostSyncRecentPublisherAccountMedia%20%40pubId%3A{publisherAccount.PublisherAccountId}|Publisher Sync Logs>
Last synced : {Math.Round((now - publisherAccountLastSyncedOn).TotalHours, 2)} hours ago");
            }
        }

        if (countAlerted >= 3)
        {
            await _opsNotificationService.TrySendApiNotificationAsync($"Restarting MediaSync Job x [{countAlerted}]", $@"Restarted [{countAlerted}] media sync jobs in one check round");
        }

        if (deferAffectedPublisherAccountIds.Count > 0)
        { // Defer them, which will set the fields in sql...
            _deferRequestsService.PublishMessage(new PostDeferredAffected
                                                 {
                                                     Ids = deferAffectedPublisherAccountIds,
                                                     Type = RecordType.PublisherAccount
                                                 });
        }
    }

    private async Task<PublisherLinkType> PublisherAccountHasValidLinkageAsync(DynPublisherAccount publisherAccount)
    {
        var validFullLinkages = 0;
        var validBasicLinkages = 0;

        async Task<bool> linkageIsValidAsync(long fromWorkspaceId, long fromPublisherAccountId, DynWorkspace workspace = null)
        {
            if (fromWorkspaceId <= 0)
            {
                return false;
            }

            if (fromPublisherAccountId > 0)
            { // Have a FROM publisher account specified, which has to be associated with the workspace and down to the other publisher account
                var result = await _associationService.IsAssociatedAsync(fromWorkspaceId, fromPublisherAccountId)
                             &&
                             await _associationService.IsAssociatedAsync(fromPublisherAccountId, publisherAccount.PublisherAccountId);

                if (result)
                {
                    validFullLinkages++;
                }

                return result;
            }

            // No from, could be a basic linked account
            workspace ??= await WorkspaceService.DefaultWorkspaceService.TryGetWorkspaceAsync(fromWorkspaceId);

            var basicLinkIsValid = !workspace.SecondaryTokenPublisherAccountIds.IsNullOrEmpty() &&
                                   workspace.SecondaryTokenPublisherAccountIds.Contains(publisherAccount.PublisherAccountId);

            if (basicLinkIsValid)
            {
                validBasicLinkages++;
            }

            return basicLinkIsValid;
        }

        // Get the linkages that are supposedly still valid in the db and ensure they are in the real world...if they are not, send a delink...if they are, continue along
        var linkedWorkspacePublisherAccountIds = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<LinkedWorkspacePublisherAccountIds>(@"
SELECT    DISTINCT pal.WorkspaceId AS WorkspaceId, pal.FromPublisherAccountId AS FromPublisherAccountId
FROM      PublisherAccountLinks pal
WHERE     pal.ToPublisherAccountId = @PublisherAccountId
          AND pal.DeletedOn IS NULL;
",
                                                                                                                                                new
                                                                                                                                                {
                                                                                                                                                    publisherAccount.PublisherAccountId
                                                                                                                                                }));

        if (linkedWorkspacePublisherAccountIds != null)
        {
            foreach (var linkedWorkspacePublisherAccountId in linkedWorkspacePublisherAccountIds)
            {
                // Ensure the workspace is linked to the token account (i.e. the FromPublisherAccount retrieved) and the token account is linked to the non-token account
                if (await linkageIsValidAsync(linkedWorkspacePublisherAccountId.WorkspaceId,
                                              linkedWorkspacePublisherAccountId.FromPublisherAccountId))
                {
                    continue;
                }

                // Workspace and token account are not linked anymore, or publisher accounts aren't linked anymore, delink to ensure everything is mapped up
                _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                   {
                                                       FromWorkspaceId = linkedWorkspacePublisherAccountId.WorkspaceId,
                                                       FromPublisherAccountId = linkedWorkspacePublisherAccountId.FromPublisherAccountId,
                                                       ToPublisherAccountId = publisherAccount.PublisherAccountId
                                                   }.WithAdminRequestInfo());
            }
        }

        // If we did not find any valid linkages from the db side, see if any exist in the real world....
        if (validFullLinkages <= 0 && validBasicLinkages <= 0)
        {
            await foreach (var linkedWorkspaceId in WorkspaceService.DefaultWorkspaceService
                                                                    .GetAssociatedWorkspaceIdsAsync(publisherAccount))
            {
                var workspace = await WorkspaceService.DefaultWorkspaceService.TryGetWorkspaceAsync(linkedWorkspaceId);

                if (workspace != null)
                {
                    if (await linkageIsValidAsync(workspace.Id, workspace.DefaultPublisherAccountId, workspace))
                    {
                        continue;
                    }

                    // Can have a workspace where the default account is a valid full account that has no access to the given account, but has a backup/secondary
                    // link to it...try that
                    if (await linkageIsValidAsync(workspace.Id, 0, workspace))
                    {
                        continue;
                    }

                    // Workspace and token account are not linked anymore, or publisher accounts aren't linked anymore, delink to ensure everything is mapped up
                    _deferRequestsService.DeferRequest(new DelinkPublisherAccount
                                                       {
                                                           FromWorkspaceId = workspace.Id,
                                                           FromPublisherAccountId = workspace.DefaultPublisherAccountId,
                                                           ToPublisherAccountId = publisherAccount.PublisherAccountId
                                                       }.WithAdminRequestInfo());
                }
            }
        }

        return validFullLinkages > 0
                   ? PublisherLinkType.Full
                   : validBasicLinkages > 0
                       ? PublisherLinkType.Basic
                       : PublisherLinkType.None;
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class LinkedWorkspacePublisherAccountIds
    {
        public long WorkspaceId { get; set; }
        public long FromPublisherAccountId { get; set; }
    }
}
