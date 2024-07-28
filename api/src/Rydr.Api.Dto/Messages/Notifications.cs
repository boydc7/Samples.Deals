using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Messages;

[Route("/notifications", "GET")]
public class GetNotifications : RequestBase, IReturn<OnlyResultsResponse<NotificationItem>>, IHasSkipTake, IGet
{
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<long> ForPublisherAccountIds { get; set; }
}

[Route("/notifications/counts", "GET")]
public class GetNotificationCounts : RequestBase, IReturn<OnlyResultsResponse<NotificationCount>>, IGet
{
    public List<long> ForPublisherAccountIds { get; set; }
}

[Route("/notifications", "POST")]
public class PostServerNotification : RequestBase, IReturnVoid
{
    public ServerNotification Notification { get; set; }
}

[Route("/dealmatchnotifications", "POST")]
public class PostServerDealMatchNotification : RequestBase, IReturnVoid
{
    public string Title { get; set; }
    public string Message { get; set; }
    public long FromPublisherAccountId { get; set; }
    public List<long> ToPublisherAccountIds { get; set; }
    public long DealId { get; set; }
}

[Route("/notifications", "DELETE")]
[Route("/notifications/{id}", "DELETE")]
public class DeleteNotifications : RequestBase, IReturnVoid, IDelete
{
    public string Id { get; set; }
}

[Route("/notifications/subscriptions", "GET")]
public class GetNotificationSubscriptions : RequestBase, IReturn<OnlyResultsResponse<NotificationSubscription>>, IGet { }

[Route("/notifications/subscriptions", "PUT")]
public class PutNotificationSubscription : RequestBase, IReturnVoid, IPut
{
    public ServerNotificationType NotificationType { get; set; }
}

[Route("/notifications/subscriptions/{notificationtype}", "DELETE")]
public class DeleteNotificationSubscription : RequestBase, IReturnVoid, IDelete
{
    public ServerNotificationType NotificationType { get; set; }
}

[Route("/notifications/subscribe", "POST")]
public class ServerNotificationSubscribe : DeferrableRequestBase, IReturn<OnlyResultResponse<StringIdResponse>>, IPost
{
    public string Token { get; set; }
    public string OldTokenHash { get; set; }
}

[Route("/notifications/unsubscribe", "POST")]
public class ServerNotificationUnSubscribe : DeferrableRequestBase, IReturnVoid, IPost
{
    public string TokenHash { get; set; }
}

[Route("/notifications/subscribe/{tokenhash}", "DELETE")]
public class DeleteServerNotification : RequestBase, IReturnVoid
{
    public string TokenHash { get; set; }
}

// INTERNAL REQUESTS

[Route("/internal/notifications/trackevent", "POST")]
public class PostTrackEventNotification : RequestBase, IReturnVoid
{
    public string EventName { get; set; }
    public string EventData { get; set; }
    public string UserEmail { get; set; }

    public List<ExternalCrmUpdateItem> RelatedUpdateItems { get; set; }
}

[Route("/internal/notifications/crmcontactupdate", "POST")]
public class PostExternalCrmContactUpdate : RequestBase, IReturnVoid
{
    public string UserEmail { get; set; }
    public List<ExternalCrmUpdateItem> Items { get; set; }
}

public class NotificationSubscription
{
    public ServerNotificationType NotificationType { get; set; }
    public bool IsSubscribed { get; set; }
}

public class NotificationCount
{
    public long PublisherAccountId { get; set; }
    public long TotalUnread { get; set; }
}

public class NotificationItem
{
    public string NotificationId { get; set; }
    public PublisherAccountInfo FromPublisherAccount { get; set; }
    public PublisherAccountInfo ToPublisherAccount { get; set; }
    public RecordTypeId ForRecord { get; set; }
    public string ForRecordName { get; set; }
    public string NotificationType { get; set; }

    public bool IsRead { get; set; }
    public long Count { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public DateTime OccurredOn { get; set; }
}

public class ServerNotification
{
    public PublisherAccountInfo From { get; set; }
    public PublisherAccountInfo To { get; set; }
    public RecordTypeId ForRecord { get; set; }
    public long InWorkspaceId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }

    public string NotificationType
    {
        get => ServerNotificationType.ToString().ToLowerInvariant();
        set
        {
            try
            {
                ServerNotificationType = value.TryToEnum(ServerNotificationType.Unspecified);
            }
            catch
            {
                ServerNotificationType = ServerNotificationType.Unspecified;
            }
        }
    }

    [Ignore]
    [IgnoreDataMember]
    public ServerNotificationType ServerNotificationType { get; set; }
}

public class ExternalCrmUpdateItem
{
    public string FieldName { get; set; }
    public string FieldValue { get; set; }
    public bool Remove { get; set; }
}
