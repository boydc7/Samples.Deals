using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Services.Services;

public class SystemTaskService : BaseAdminApiService
{
    private readonly IRydrDataService _rydrDataService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly ICacheClient _cacheClient;
    private readonly IDealService _dealService;
    private readonly IPersistentCounterAndListService _persistentCounterAndListService;
    private readonly IDistributedLockService _distributedLockService;

    public SystemTaskService(IRydrDataService rydrDataService,
                             IDeferRequestsService deferRequestsService,
                             IPublisherAccountService publisherAccountService,
                             ICacheClient cacheClient,
                             IDealService dealService,
                             IPersistentCounterAndListService persistentCounterAndListService,
                             IDistributedLockService distributedLockService)
    {
        _rydrDataService = rydrDataService;
        _deferRequestsService = deferRequestsService;
        _publisherAccountService = publisherAccountService;
        _cacheClient = cacheClient;
        _dealService = dealService;
        _persistentCounterAndListService = persistentCounterAndListService;
        _distributedLockService = distributedLockService;
    }

    public async Task Post(MonitorRequestAllowances request)
    {
        var requestsToRescind = await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<DealRequestIds>(@"
SELECT  dr.DealId, dr.PublisherAccountId
FROM    DealRequests dr
WHERE   dr.Status = 4
        AND dr.RescindOn IS NOT NULL
        AND dr.RescindOn <= @NowUtc
UNION
SELECT  dr.DealId, dr.PublisherAccountId
FROM    DealRequests dr
WHERE   dr.Status = 7
        AND dr.DelinquentOn IS NOT NULL
        AND dr.DelinquentOn <= @NowUtc;
",
                                                                                                           new
                                                                                                           {
                                                                                                               NowUtc = _dateTimeProvider.UtcNow
                                                                                                           }));

        if (requestsToRescind == null)
        {
            return;
        }

        foreach (var requestToRescind in requestsToRescind)
        {
            _deferRequestsService.DeferLowPriRequest(new CheckDealRequestAllowances
                                                     {
                                                         DealId = requestToRescind.DealId,
                                                         PublisherAccountId = requestToRescind.PublisherAccountId,
                                                         DeferAsAffectedOnPass = true
                                                     }.WithAdminRequestInfo());
        }
    }

    public async Task Post(MonitorRequestNotifications request)
    {
        var nowUtc = _dateTimeProvider.UtcNow;
        var todayUtc = nowUtc.Date;
        var startSendTime = todayUtc.AddHours(13.5); // Gets us to around 9am ESTish depending on the time of year...bit of a hack for now, but eh
        var endSendTime = startSendTime.AddHours(2);
        var todayTimestamp = todayUtc.ToUnixTimestamp();

        if (!request.Force && (nowUtc < startSendTime || nowUtc > endSendTime))
        { // Outside our sending window for the day...
            return;
        }

        var requestNotificationsLastCheckedOnItem = _cacheClient.TryGet(nameof(MonitorRequestNotifications), () => new Int64Id(), CacheConfig.LongConfig) ?? new Int64Id();

        if (!request.Force && requestNotificationsLastCheckedOnItem.Id >= todayTimestamp)
        { // Have sent notifications today already, nothing else to do
            return;
        }

        using(var lockItem = _distributedLockService.TryGetKeyLock(nameof(MonitorRequestNotifications), nameof(SystemTaskService), 1800))
        {
            if (lockItem == null)
            {
                return;
            }

            var notifyOutLimitDate = nowUtc.AddDays(8).Date;

            var publishersToNotifyMap = (await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<DealRequestIds>(@"
SELECT  MAX(t.DealId) AS DealId, t.PublisherAccountId, COUNT(*) AS Cnt, MIN(t.DateVal) AS EarliestDate, MAX(t.DateVal) AS LastestDate
FROM    (
        SELECT  dr.DealId, dr.PublisherAccountId, dr.RescindOn AS DateVal
        FROM    DealRequests dr
        WHERE   dr.Status = 4
                AND dr.RescindOn IS NOT NULL
                AND dr.RescindOn > @NowUtc
                AND dr.RescindOn < @NotifyOutLimitDate
        UNION ALL
        SELECT  dr.DealId, dr.PublisherAccountId, dr.DelinquentOn AS DateVal
        FROM    DealRequests dr
        WHERE   dr.Status = 7
                AND dr.DelinquentOn IS NOT NULL
                AND dr.DelinquentOn > @NowUtc
                AND dr.DelinquentOn < @NotifyOutLimitDate
        ) t
GROUP BY
        t.PublisherAccountId;
",
                                                                                                                    new
                                                                                                                    {
                                                                                                                        NowUtc = nowUtc,
                                                                                                                        NotifyOutLimitDate = notifyOutLimitDate
                                                                                                                    }))).ToDictionarySafe(dri => dri.PublisherAccountId);

            if (publishersToNotifyMap.IsNullOrEmptyRydr())
            {
                return;
            }

            var publisherAccountMap = await _publisherAccountService.GetPublisherAccountsAsync(publishersToNotifyMap.Keys)
                                                                    .ToDictionarySafe(p => p.PublisherAccountId,
                                                                                      p => p.ToPublisherAccountInfo());

            var tomorrowUtc = nowUtc.AddDays(1).Date;
            var twoDaysFromNowUtc = nowUtc.AddDays(2).Date;

            var dailyNotificationSentKey = string.Concat("urn:", nameof(MonitorRequestNotifications), todayTimestamp);

            using(var counterService = _persistentCounterAndListService.CreateStatefulInstance)
            {
                await foreach (var dynDealRequest in _dynamoDb.QueryItemsAsync<DynDealRequest>(publishersToNotifyMap.Values
                                                                                                                    .Select(r => new DynamoId(r.DealId, r.PublisherAccountId.ToEdgeId()))))
                {
                    if (dynDealRequest.RequestStatus != DealRequestStatus.InProgress && dynDealRequest.RequestStatus != DealRequestStatus.Redeemed)
                    {
                        _deferRequestsService.DeferLowPriRequest(new CheckDealRequestAllowances
                                                                 {
                                                                     DealId = dynDealRequest.DealId,
                                                                     PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                                     DeferAsAffectedOnPass = true
                                                                 }.WithAdminRequestInfo());

                        continue;
                    }

                    var rescindOn = dynDealRequest.RescindOn;
                    var delinquentOn = dynDealRequest.DelinquentOn;

                    if ((dynDealRequest.RequestStatus == DealRequestStatus.InProgress && (rescindOn.Value <= nowUtc || rescindOn.Value > notifyOutLimitDate)) ||
                        (dynDealRequest.RequestStatus == DealRequestStatus.Redeemed && (delinquentOn.Value <= nowUtc || delinquentOn.Value > notifyOutLimitDate)))
                    {
                        _deferRequestsService.DeferLowPriRequest(new CheckDealRequestAllowances
                                                                 {
                                                                     DealId = dynDealRequest.DealId,
                                                                     PublisherAccountId = dynDealRequest.PublisherAccountId,
                                                                     DeferAsAffectedOnPass = true
                                                                 }.WithAdminRequestInfo());

                        continue;
                    }

                    if (counterService.Exists(dailyNotificationSentKey, dynDealRequest.PublisherAccountId.ToStringInvariant()))
                    {
                        continue;
                    }

                    var publisherToNotifyIds = publishersToNotifyMap[dynDealRequest.PublisherAccountId];
                    var publisherInfo = publisherAccountMap[dynDealRequest.PublisherAccountId];

                    var dynDeal = publisherToNotifyIds.Cnt > 1
                                      ? null
                                      : await _dealService.GetDealAsync(dynDealRequest.DealId);

                    var dealPublisher = publisherToNotifyIds.Cnt > 1
                                            ? null
                                            : await _publisherAccountService.GetPublisherAccountAsync(dynDealRequest.DealPublisherAccountId);

                    var multiCntMsg = publisherToNotifyIds.Cnt > 1
                                          ? string.Concat("Multiple pacts expire starting ",
                                                          publisherToNotifyIds.EarliestDate < tomorrowUtc
                                                              ? "today emoji.Exclamation"
                                                              : publisherToNotifyIds.EarliestDate < twoDaysFromNowUtc
                                                                  ? "tomorrow emoji.WarningSign"
                                                                  : $"{(int)(publisherToNotifyIds.EarliestDate - todayUtc).TotalDays} days from today",
                                                          ".")
                                          : null;

                    // Send the notification to this creator...
                    counterService.AddUniqueItem(dailyNotificationSentKey, dynDealRequest.PublisherAccountId.ToStringInvariant());

                    _deferRequestsService.DeferLowPriRequest(new PostServerNotification
                                                             {
                                                                 Notification = new ServerNotification
                                                                                {
                                                                                    To = publisherInfo,
                                                                                    ForRecord = publisherToNotifyIds.Cnt > 1
                                                                                                    ? null
                                                                                                    : new RecordTypeId(RecordType.Deal, dynDealRequest.DealId),
                                                                                    ServerNotificationType = publisherToNotifyIds.Cnt > 1
                                                                                                                 ? ServerNotificationType.DealRequestsGeneric
                                                                                                                 : ServerNotificationType.DealRequestGeneric,
                                                                                    InWorkspaceId = publisherInfo.IsInfluencer()
                                                                                                        ? 0
                                                                                                        : dynDealRequest.DealWorkspaceId,
                                                                                    Title = publisherToNotifyIds.Cnt > 1
                                                                                                ? $"Complete {publisherToNotifyIds.Cnt} pacts"
                                                                                                : "Complete your pact today emoji.Exclamation",
                                                                                    Message = publisherToNotifyIds.Cnt > 1
                                                                                                  ? multiCntMsg
                                                                                                  : $"{dealPublisher.UserName}: {dynDeal.Title}"
                                                                                }
                                                             });
                }

                // Successful, clear the set/key...
                counterService.Clear(dailyNotificationSentKey);
            }

            requestNotificationsLastCheckedOnItem.Id = _dateTimeProvider.UtcNowTs;
            await _cacheClient.TrySetAsync(requestNotificationsLastCheckedOnItem, nameof(MonitorSystemResources), CacheConfig.LongConfig);
        }
    }

    private class DealRequestIds
    {
        public long DealId { get; }
        public long PublisherAccountId { get; }
        public int Cnt { get; }
        public DateTime EarliestDate { get; }
        public DateTime LastestDate { get; set; }
    }
}
