using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IAwsSnsService : ISmsService
    {
        SnsEndpointAttributes GetEndpointAttributes(string arn);
        string SubscribeToPushNotifications(long publisherAccountId, string deviceToken, ServerNotificationMedium notificationMedium, string deviceInfo = null);

        void UnsubscribeFromPushNotifications(string arn);

        void PublishPushNotification(string arn, string message);
        Task PublishPushNotificationAsync(string arn, string message);
        Task PublishTopicNotificationAsync(string topicArn, string subject, string message, IDictionary<string, string> messageAttributes = null);
    }
}
