using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Services;

namespace Rydr.Api.Core.Services.Internal
{
    public class AwsOpsNotificationService : IOpsNotificationService
    {
        private static readonly string _apiNotificationTopicArn = RydrEnvironment.GetAppSetting("AWS.SNS.ApiAlertArn", "arn:aws:sns:us-west-2:933347060724:Api-Slack-Api-Alerts");
        private static readonly string _appNotificationTopicArn = RydrEnvironment.GetAppSetting("AWS.SNS.AppAlertArn", "arn:aws:sns:us-west-2:933347060724:Api-Slack-App-Alerts");
        private static readonly string _managedAccountNotificationTopicArn = RydrEnvironment.GetAppSetting("AWS.SNS.ManagedAccountAlertArn", "arn:aws:sns:us-west-2:933347060724:Api-Slack-ManagedAccount-Alerts");
        private static readonly string _eventTrackNotificationTopicArn = RydrEnvironment.GetAppSetting("AWS.SNS.EventTrackArn", "arn:aws:sns:us-west-2:933347060724:Api-ActiveCampaign-TrackEvent");

        private readonly IAwsSnsService _awsSnsService;

        public AwsOpsNotificationService(IAwsSnsService awsSnsService)
        {
            _awsSnsService = awsSnsService;
        }

        public async Task SendAppNotificationAsync(string subject, string message)
            => await _awsSnsService.PublishTopicNotificationAsync(_appNotificationTopicArn, subject, message);

        public async Task SendApiNotificationAsync(string subject, string message)
            => await _awsSnsService.PublishTopicNotificationAsync(_apiNotificationTopicArn, subject, message);

        public async Task SendManagedAccountNotificationAsync(string subject, string message)
            => await _awsSnsService.PublishTopicNotificationAsync(_managedAccountNotificationTopicArn, subject, message);

        public async Task SendTrackEventNotificationAsync(string eventName, string userEmail, string extraEventInfo = null)
            => await _awsSnsService.PublishTopicNotificationAsync(_eventTrackNotificationTopicArn, eventName, extraEventInfo,
                                                                  new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                                                  {
                                                                      {
                                                                          "ContactEmail", userEmail
                                                                      }
                                                                  });
    }
}
