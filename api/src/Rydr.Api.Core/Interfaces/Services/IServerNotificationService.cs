using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Models;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IServerNotificationService
    {
        Task NotifyAsync(ServerNotification message, RecordTypeId notifyRecordId = null);
        Task SubscribeAsync(long userId, string token, string oldTokenHash);
        Task UnsubscribeAsync(string tokenHash);
    }

    public interface IServerNotificationSubsriptionService
    {
        Task<IReadOnlyList<(ServerNotificationType Type, bool IsSubscribed)>> GetSubscriptionsAsync(long userId, long workspaceId, long publisherAccountId);
        Task AddSubscriptionAsync(long userId, long workspaceId, long publisherAccountId, ServerNotificationType toNotificationType);
        Task DeleteSubscriptionAsync(long userId, long workspaceId, long publisherAccountId, ServerNotificationType fromNotificationType);
    }

    public interface IUserNotificationService
    {
        void AddUnread(long publisherAccountId, string notificationEdgeId, long workspaceId);
        void RemoveUnread(long publisherAccountId, string notificationEdgeId, long workspaceId);
        void RemoveAllUnread(long publisherAccountId, long workspaceId);
        long GetUnreadCount(long publisherAccountId, long workspaceId);
    }

    public interface IPushNotificationMessageFormattingService
    {
        string FormatMessage<T>(T message, ServerNotificationMedium medium)
            where T : class;

        PushNotificationMessageParts GetMessageParts<T>(T message)
            where T : class;
    }
}
