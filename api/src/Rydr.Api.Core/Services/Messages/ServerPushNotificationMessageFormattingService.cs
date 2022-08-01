using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Messages;

namespace Rydr.Api.Core.Services.Messages
{
    public class ServerPushNotificationMessageFormattingService : BasePushNotificationMessageFormattingService
    {
        private readonly IUserNotificationService _userNotificationService;

        public ServerPushNotificationMessageFormattingService(IUserNotificationService userNotificationService)
        {
            _userNotificationService = userNotificationService;
        }

        public override PushNotificationMessageParts GetMessageParts<T>(T message)
        {
            if (!(message is ServerNotification serverNotification))
            {
                return null;
            }

            // Influencers aren't bound to workspace specific things, others are
            var customObj = new ServerNotificationPushNotificationObject
                            {
                                FromPublisherAccount = serverNotification.From,
                                ToPublisherAccount = serverNotification.To,
                                ForRecord = serverNotification.ForRecord,
                                NotificationType = serverNotification.NotificationType,
                                OccurredOn = DateTimeHelper.UtcNow,
                                WorkspaceId = serverNotification.InWorkspaceId
                            };

            if (customObj.FromPublisherAccount?.Metrics != null)
            {
                customObj.FromPublisherAccount.Metrics = null;
            }

            if (customObj.ToPublisherAccount?.Metrics != null)
            {
                customObj.ToPublisherAccount.Metrics = null;
            }

            var msgParts = new PushNotificationMessageParts
                           {
                               Title = serverNotification.Title,
                               Body = serverNotification.Message,
                               CustomObj = customObj,
                               Badge = serverNotification.To == null
                                           ? 0
                                           : _userNotificationService.GetUnreadCount(serverNotification.To.Id, serverNotification.InWorkspaceId)
                           };

            return msgParts;
        }
    }
}
