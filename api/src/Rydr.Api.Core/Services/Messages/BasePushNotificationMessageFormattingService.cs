using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using ServiceStack;

namespace Rydr.Api.Core.Services.Messages
{
    public abstract class BasePushNotificationMessageFormattingService : IPushNotificationMessageFormattingService
    {
        private static readonly bool _isSandboxApnEnvironment = RydrEnvironment.GetAppSetting("AWS.Sns.IsSandboxApn", true);

        public abstract PushNotificationMessageParts GetMessageParts<T>(T message)
            where T : class;

        public string FormatMessage<T>(T message, ServerNotificationMedium medium)
            where T : class
        {
            if (message == null || medium == ServerNotificationMedium.Unspecified)
            {
                return null;
            }

            var parts = GetMessageParts(message);

            if (parts == null)
            {
                return null;
            }

            switch (medium)
            {
                case ServerNotificationMedium.AppleApn:
                case ServerNotificationMedium.AppleEnterpriseApn:
                    var apnPushMsg = FormatApn(parts).ToJson();

                    return apnPushMsg;

                case ServerNotificationMedium.AndroidGcm:
                    var gcmPushMsg = FormatGcm(parts).ToJson();

                    return gcmPushMsg;

                default:

                    return null;
            }
        }

        protected ApnPushNotification FormatApn(PushNotificationMessageParts parts)
        {
            if ((parts?.Title).IsNullOrEmpty() || parts.Body.IsNullOrEmpty())
            {
                return null;
            }

            var apsPart = new ApnPushNotificationFormat
                          {
                              Aps = new ApnPushNotificationAps
                                    {
                                        Alert = new PushNotificationTitleBody
                                                {
                                                    Title = parts.Title,
                                                    Body = parts.Body
                                                },
                                        Badge = (int)parts.Badge,
                                        IsBackgroundUpdate = parts.IsBackgroundUpdate
                                                                 ? (int?)1
                                                                 : null
                                    },
                              RydrObject = parts.CustomObj.ToJsv()
                          };

            var apnPushNotification = new ApnPushNotification
                                      {
                                          Apns = _isSandboxApnEnvironment
                                                     ? null
                                                     : apsPart.ToJson(),
                                          ApnsSandbox = _isSandboxApnEnvironment
                                                            ? apsPart.ToJson()
                                                            : null
                                      };

            return apnPushNotification;
        }

        protected GcmPushNotification FormatGcm(PushNotificationMessageParts parts)
        {
            if ((parts?.Title).IsNullOrEmpty() || parts.Body.IsNullOrEmpty())
            {
                return null;
            }

            var titleBody = new PushNotificationTitleBody
                            {
                                Title = parts.Title,
                                Body = parts.Body
                            };

            var gcmPart = new GcmPushNotificationFormat
                          {
                              Notification = titleBody,
                              Data = new GcmPushNotificationData
                                     {
                                         Notification = titleBody,
                                         RydrObject = parts.CustomObj.ToJsv()
                                     }
                          };

            var gcmPushNotification = new GcmPushNotification
                                      {
                                          Gcm = gcmPart.ToJson()
                                      };

            return gcmPushNotification;
        }
    }
}
