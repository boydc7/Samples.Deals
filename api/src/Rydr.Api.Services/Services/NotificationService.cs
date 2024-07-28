using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Services.Services;

[RydrNeverCacheResponse]
public class NotificationService : BaseAuthenticatedApiService
{
    private readonly IDialogCountService _dialogCountService;
    private readonly IDialogMessageService _dialogMessageService;
    private readonly IServerNotificationService _serverNotificationService;
    private readonly IServerNotificationSubsriptionService _serverNotificationSubsriptionService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IRecordTypeRecordService _recordTypeRecordService;
    private readonly IUserNotificationService _userNotificationService;

    public NotificationService(IServerNotificationService serverNotificationService,
                               IDialogCountService dialogCountService,
                               IDialogMessageService dialogMessageService,
                               IUserNotificationService userNotificationService,
                               IServerNotificationSubsriptionService serverNotificationSubsriptionService,
                               IPublisherAccountService publisherAccountService,
                               IDeferRequestsService deferRequestsService,
                               IRecordTypeRecordService recordTypeRecordService)
    {
        _serverNotificationService = serverNotificationService;
        _dialogCountService = dialogCountService;
        _dialogMessageService = dialogMessageService;
        _userNotificationService = userNotificationService;
        _serverNotificationSubsriptionService = serverNotificationSubsriptionService;
        _publisherAccountService = publisherAccountService;
        _deferRequestsService = deferRequestsService;
        _recordTypeRecordService = recordTypeRecordService;
    }

    public async Task<OnlyResultsResponse<NotificationCount>> Get(GetNotificationCounts request)
    {
        List<NotificationCount> results;

        if (request.ForPublisherAccountIds.IsNullOrEmpty())
        { // Only getting results for the current account
            var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.RequestPublisherAccountId);

            results = new List<NotificationCount>
                      {
                          new()
                          {
                              PublisherAccountId = request.RequestPublisherAccountId,
                              TotalUnread = _userNotificationService.GetUnreadCount(request.RequestPublisherAccountId, publisherAccount.GetContextWorkspaceId(request.WorkspaceId))
                          }
                      };
        }
        else
        { // Getting results for one or more api keys
            results = await _publisherAccountService.GetPublisherAccountsAsync(request.ForPublisherAccountIds)
                                                    .Select(p => new NotificationCount
                                                                 {
                                                                     PublisherAccountId = p.PublisherAccountId,
                                                                     TotalUnread = _userNotificationService.GetUnreadCount(p.PublisherAccountId, p.GetContextWorkspaceId(request.WorkspaceId))
                                                                 })
                                                    .Take(request.ForPublisherAccountIds.Count)
                                                    .ToList(request.ForPublisherAccountIds.Count);
        }

        return results.AsOnlyResultsResponse();
    }

    [RydrCacheResponse(1800, MaxAge = 120)]
    public async Task<OnlyResultsResponse<NotificationItem>> Get(GetNotifications request)
    {
        var skip = request.Skip.Gz(0);
        var take = request.Take.Gz(100);

        if (!request.ForPublisherAccountIds.IsNullOrEmpty())
        {
            return new OnlyResultsResponse<NotificationItem>
                   {
                       Results = (await DoGetMultipleAccountNotificationsAsync(request.ForPublisherAccountIds, skip, take, request.WorkspaceId)).AsListReadOnly()
                   };
        }

        // Didn't ask for multiple account data, just get info for this account - when getting notifications for "me" or a single
        // account, the notifications returned are bounded to the requested workspace, if applicable (when getting multiple account
        // notifications, we return all notifications for all workspaces)
        var myPublisherAccountInfo = (await _publisherAccountService.TryGetPublisherAccountAsync(request.RequestPublisherAccountId)).ToPublisherAccountInfo();

        // Influencer notifications aren't tracked per workspace...
        var contextWorkspaceId = myPublisherAccountInfo.GetContextWorkspaceId(request.WorkspaceId);

        var response = new OnlyResultsResponse<NotificationItem>();

        var startRef = DynItem.BuildTypeReferenceHash(DynItemType.Notification, string.Concat(contextWorkspaceId.ToEdgeId(), "|1500000000"));
        var endRef = DynItem.BuildTypeReferenceHash(DynItemType.Notification, string.Concat(contextWorkspaceId.ToEdgeId(), "|3000000000"));

        var notifications = await _dynamoDb.GetItemsFromAsync<DynNotification, DynItemIdTypeReferenceGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == myPublisherAccountInfo.Id &&
                                                                                                                                                                                   Dynamo.Between(i.TypeReference, startRef, endRef))
                                                                                                                           .Filter(i => i.DeletedOnUtc == null)
                                                                                                                           .Select(i => new
                                                                                                                                        {
                                                                                                                                            i.Id,
                                                                                                                                            i.EdgeId
                                                                                                                                        })
                                                                                                                           .QueryAsync(_dynamoDb),
                                                                                                                  i => i.GetDynamoId(),
                                                                                                                  skip,
                                                                                                                  take)
                                           .ToList(take);

        if (notifications.IsNullOrEmpty())
        {
            return response;
        }

        var fromPublisherAccountMap = _publisherAccountService.GetPublisherAccounts(notifications.Where(n => n.FromPublisherAccountId > 0)
                                                                                                 .Select(n => n.FromPublisherAccountId)
                                                                                                 .Distinct())
                                                              .Select(dpa => dpa.ToPublisherAccountInfo())
                                                              .ToSafeDictionary(dpa => dpa.Id);

        // Just a perf optimization to increase the speed of deal lookups, which are like 90% of the notification forRecords...
        IEnumerable<long> getPublisherIdsForDealNotification(DynNotification forNotification)
        {
            yield return myPublisherAccountInfo.Id;

            if (forNotification.FromPublisherAccountId > 0)
            {
                yield return forNotification.FromPublisherAccountId;
            }
        }

        var forDealRecordsMap = await _dynamoDb.QueryItemsAsync<DynDeal>(notifications.Where(n => n.ForRecord != null &&
                                                                                                  n.ForRecord.Type == RecordType.Deal)
                                                                                      .SelectMany(n => getPublisherIdsForDealNotification(n).Select(p => (PublisherAccountId: p,
                                                                                                                                                          DealId: n.ForRecord.Id)))
                                                                                      .Distinct()
                                                                                      .Select(t => new DynamoId(t.PublisherAccountId, t.DealId.ToEdgeId())))
                                               .ToDictionarySafe(d => d.DealId);

        var results = new List<NotificationItem>(notifications.Count);

        // Have to orderby here to re-order the page of data correctly - the FromQueryIndex above returns them ordered correctly, but the GetItems does
        // not guarantee order, so we get the right page of data, but have to order them on the page
        foreach (var notification in notifications.OrderByDescending(n => n.CreatedOn))
        {
            Guard.AgainstInvalidData(notification.ToPublisherAccountId != myPublisherAccountInfo.Id, "Invalid notification state - code [npid|mpid]");

            var forRecord = notification.ForRecord == null
                                ? null
                                : notification.ForRecord.Type == RecordType.Deal
                                    ? forDealRecordsMap.GetValueOrDefault(notification.ForRecord.Id)
                                    : await _recordTypeRecordService.TryGetRecordAsync<IHasNameAndIsRecordLookup>(notification.ForRecord, request, true);

            var notificationItem = new NotificationItem
                                   {
                                       NotificationId = notification.EdgeId,
                                       ToPublisherAccount = myPublisherAccountInfo,
                                       FromPublisherAccount = notification.FromPublisherAccountId > 0
                                                                  ? fromPublisherAccountMap[notification.FromPublisherAccountId]
                                                                  : null,
                                       ForRecord = notification.ForRecord,
                                       ForRecordName = forRecord?.Name.ToNullIfEmpty(),
                                       NotificationType = notification.NotificationType.ToString(),
                                       Count = 1,
                                       Title = notification.Title,
                                       Body = notification.Message,
                                       OccurredOn = notification.CreatedOn,
                                       IsRead = notification.IsRead
                                   };

            if (notification.NotificationType == ServerNotificationType.Dialog &&
                notification.ForRecord != null && notification.ForRecord.Type == RecordType.Dialog)
            {
                notificationItem.Count = _dialogCountService.GetDialogUnreadCount(notification.ForRecord.Id, request.RequestPublisherAccountId);

                var lastMsg = await _dialogMessageService.GetLastMessageAsync(notification.ForRecord.Id);

                notificationItem.Body = lastMsg?.Message;
            }

            results.Add(notificationItem);
        }

        response.Results = results;

        return response;
    }

    public async Task Delete(DeleteNotifications request)
    {
        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.RequestPublisherAccountId);

        if (request.Id.HasValue())
        { // Mark single one as read
            var dynNotification = await _dynamoDb.GetItemAsync<DynNotification>(request.RequestPublisherAccountId, request.Id);

            if (dynNotification == null || dynNotification.IsRead || dynNotification.IsDeleted())
            {
                return;
            }

            dynNotification.IsRead = true;

            await _dynamoDb.PutItemTrackedAsync(dynNotification);

            _userNotificationService.RemoveUnread(request.RequestPublisherAccountId, dynNotification.EdgeId,
                                                  publisherAccount.GetContextWorkspaceId(request.WorkspaceId));
        }
        else
        { // Mark all unread notifications as read for the user
            var contextWorkspaceId = publisherAccount.GetContextWorkspaceId(request.WorkspaceId);

            await _dynamoDb.PutItemsFromAsync(_dynamoDb.FromQuery<DynNotification>(n => n.Id == request.RequestPublisherAccountId)
                                                       .Filter(n => n.TypeId == (int)DynItemType.Notification &&
                                                                    n.DeletedOnUtc == null &&
                                                                    n.IsRead == false &&
                                                                    Dynamo.BeginsWith(n.ReferenceId, string.Concat(contextWorkspaceId.ToEdgeId(), "|")))
                                                       .QueryAsync(_dynamoDb),
                                              n =>
                                              {
                                                  n.IsRead = true;

                                                  n.UpdateDateTimeTrackedValues(request);

                                                  return n;
                                              });

            // Race condition here, but it's just notifications, and there's a race condition from the app to the server also, so...
            _userNotificationService.RemoveAllUnread(request.RequestPublisherAccountId, contextWorkspaceId);
        }
    }

    [RequiredRole("Admin")]
    public async Task Post(PostServerDealMatchNotification request)
    {
        var fromPublisherAccount = (await _publisherAccountService.TryGetPublisherAccountAsync(request.FromPublisherAccountId))?.ToPublisherAccountInfo();

        foreach (var toPublisherAccountId in request.ToPublisherAccountIds)
        {
            var toPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(toPublisherAccountId);

            if (toPublisherAccount == null || toPublisherAccount.IsDeleted() || !toPublisherAccount.IsInfluencer())
            {
                continue;
            }

            await _serverNotificationService.NotifyAsync(new ServerNotification
                                                         {
                                                             From = fromPublisherAccount,
                                                             To = toPublisherAccount.ToPublisherAccountInfo(),
                                                             ForRecord = new RecordTypeId(RecordType.Deal, request.DealId),
                                                             Message = request.Message,
                                                             Title = request.Title,
                                                             ServerNotificationType = ServerNotificationType.DealMatched
                                                         });
        }
    }

    [RequiredRole("Admin")]
    public async Task Post(PostServerNotification request)
    {
        var fromPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.Notification.From?.Id ?? 0);
        var toPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(request.Notification.To?.Id ?? 0);

        request.Notification.From = fromPublisherAccount?.ToPublisherAccountInfo();
        request.Notification.To = toPublisherAccount?.ToPublisherAccountInfo();

        await _serverNotificationService.NotifyAsync(request.Notification);
    }

    public async Task<OnlyResultsResponse<NotificationSubscription>> Get(GetNotificationSubscriptions request)
    {
        var subscriptions = await _serverNotificationSubsriptionService.GetSubscriptionsAsync(request.UserId, request.WorkspaceId, request.RequestPublisherAccountId);

        return (subscriptions?.Select(t => new NotificationSubscription
                                           {
                                               NotificationType = t.Type,
                                               IsSubscribed = t.IsSubscribed
                                           })).AsOnlyResultsResponse();
    }

    public Task Put(PutNotificationSubscription request)
        => _serverNotificationSubsriptionService.AddSubscriptionAsync(request.UserId, request.WorkspaceId,
                                                                      request.RequestPublisherAccountId, request.NotificationType);

    public Task Delete(DeleteNotificationSubscription request)
        => _serverNotificationSubsriptionService.DeleteSubscriptionAsync(request.UserId, request.WorkspaceId,
                                                                         request.RequestPublisherAccountId, request.NotificationType);

    public async Task<OnlyResultResponse<StringIdResponse>> Post(ServerNotificationSubscribe request)
    {
        // Defer or process this request
        if (request.IsDeferred)
        {
            await _serverNotificationService.SubscribeAsync(request.UserId, request.Token, request.OldTokenHash);
        }
        else
        {
            request.IsDeferred = true;
            _deferRequestsService.DeferLowPriRequest(request);
        }

        return new StringIdResponse
               {
                   Id = request.Token.ToSafeSha64()
               }.AsOnlyResultResponse();
    }

    public async Task Post(ServerNotificationUnSubscribe request)
    { // Defer or process this request
        if (request.IsDeferred)
        {
            await _serverNotificationService.UnsubscribeAsync(request.TokenHash);
        }
        else
        {
            request.IsDeferred = true;
            _deferRequestsService.DeferLowPriRequest(request);
        }
    }

    public Task Delete(DeleteServerNotification request)
        => Post(new ServerNotificationUnSubscribe
                {
                    TokenHash = request.TokenHash
                });

    private async Task<IEnumerable<NotificationItem>> DoGetMultipleAccountNotificationsAsync(List<long> forPublisherAccountIds, int skip, int take, long workspaceId)
    {
        // When getting multiple accout notifications, we do not restrict notifications by workspace, we return
        var publisherAccountMap = await _publisherAccountService.GetPublisherAccountsAsync(forPublisherAccountIds.Distinct())
                                                                .Where(p => p != null)
                                                                .Select(p => p.ToPublisherAccountInfo())
                                                                .ToDictionarySafe(p => p.Id);

        if (publisherAccountMap.IsNullOrEmptyRydr())
        {
            return Enumerable.Empty<NotificationItem>();
        }

        var publisherNotificationsMap = new Dictionary<long, List<DynNotification>>();

        foreach (var publisherTuple in publisherAccountMap)
        {
            var publisherAccountId = publisherTuple.Key;
            var contextWorkspaceIdEdge = publisherTuple.Value.GetContextWorkspaceId(workspaceId).ToEdgeId();

            publisherNotificationsMap[publisherAccountId] = await _dynamoDb.GetItemsFromAsync<DynNotification, DynItemIdTypeReferenceGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == publisherAccountId &&
                                                                                                                                                                                                                   Dynamo.Between(i.TypeReference,
                                                                                                                                                                                                                                  DynItem.BuildTypeReferenceHash(DynItemType.Notification, string.Concat(contextWorkspaceIdEdge, "|1500000000")),
                                                                                                                                                                                                                                  DynItem.BuildTypeReferenceHash(DynItemType.Notification, string.Concat(contextWorkspaceIdEdge, "|3000000000"))))
                                                                                                                                                           .Filter(i => i.DeletedOnUtc == null &&
                                                                                                                                                                        i.TypeId == (int)DynItemType.Notification)
                                                                                                                                                           .Select(i => new
                                                                                                                                                                        {
                                                                                                                                                                            i.Id,
                                                                                                                                                                            i.EdgeId
                                                                                                                                                                        })
                                                                                                                                                           .QueryAsync(_dynamoDb),
                                                                                                                                                  i => i.GetDynamoId(),
                                                                                                                                                  skip,
                                                                                                                                                  take)
                                                                           .ToList(take);
        }

        if (publisherNotificationsMap.IsNullOrEmptyRydr())
        {
            return Enumerable.Empty<NotificationItem>();
        }

        var items = new List<NotificationItem>(take);

        foreach (var dynNotification in publisherNotificationsMap.SelectMany(t => t.Value)
                                                                 .Where(n => publisherAccountMap.ContainsKey(n.ToPublisherAccountId))
                                                                 .OrderByDescending(dn => dn.CreatedOnUtc)
                                                                 .Skip(skip)
                                                                 .Take(take))
        {
            var toPublisherAccount = publisherAccountMap[dynNotification.ToPublisherAccountId];

            var fromPublisherAccount = dynNotification.FromPublisherAccountId > 0 && publisherAccountMap.ContainsKey(dynNotification.FromPublisherAccountId)
                                           ? publisherAccountMap[dynNotification.FromPublisherAccountId]
                                           : null;

            if (dynNotification.FromPublisherAccountId > 0 && fromPublisherAccount == null)
            {
                fromPublisherAccount = (await _publisherAccountService.TryGetPublisherAccountAsync(dynNotification.FromPublisherAccountId)
                                       ).ToPublisherAccountInfo();

                publisherAccountMap[fromPublisherAccount.Id] = fromPublisherAccount;
            }

            var notificationItem = new NotificationItem
                                   {
                                       NotificationId = dynNotification.EdgeId,
                                       ToPublisherAccount = toPublisherAccount,
                                       FromPublisherAccount = fromPublisherAccount,
                                       ForRecord = dynNotification.ForRecord,
                                       NotificationType = dynNotification.NotificationType.ToString(),
                                       Count = 1,
                                       Title = dynNotification.Title,
                                       Body = dynNotification.Message,
                                       OccurredOn = dynNotification.CreatedOn,
                                       IsRead = dynNotification.IsRead
                                   };

            if (dynNotification.NotificationType == ServerNotificationType.Dialog &&
                dynNotification.ForRecord != null && dynNotification.ForRecord.Type == RecordType.Dialog)
            {
                notificationItem.Count = _dialogCountService.GetDialogUnreadCount(dynNotification.ForRecord.Id, publisherAccountMap[dynNotification.ToPublisherAccountId].Id);

                var lastMsg = await _dialogMessageService.GetLastMessageAsync(dynNotification.ForRecord.Id);

                notificationItem.Body = lastMsg?.Message;
            }

            items.Add(notificationItem);
        }

        return items;
    }
}
