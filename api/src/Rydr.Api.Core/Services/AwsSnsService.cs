using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Extensions;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services;

public class AwsSnsService : IAwsSnsService
{
    private static readonly string _awsAccessKey = RydrEnvironment.GetAppSetting("AWSAccessKey");
    private static readonly string _awsSecretKey = RydrEnvironment.GetAppSetting("AWSSecretKey");
    private static readonly string _awsSnsApnArn = RydrEnvironment.GetAppSetting("AWS.Sns.ApnPlatformArn");
    private static readonly string _awsSnsApnEnterpriseArn = RydrEnvironment.GetAppSetting("AWS.Sns.ApnEnterprisePlatformArn");
    private static readonly string _awsSnsGcmArn = RydrEnvironment.GetAppSetting("AWS.Sns.GcmPlatformArn");
    private static readonly string _awsSnsSmsSenderId = RydrEnvironment.GetAppSetting("AWS.Sns.Sms.SenderID", "RydrApp");
    private static readonly string _awsSnsSmsMaxPrice = RydrEnvironment.GetAppSetting("AWS.Sns.Sms.MaxPrice", "0.50");

    private readonly RegionEndpoint _awsSnsRegion;
    private readonly ILog _log = LogManager.GetLogger("AwsSnsService");

    private readonly Dictionary<ServerNotificationMedium, string> _platformArnMap;

    public AwsSnsService()
    { // Ohio currently does not support texts
        _awsSnsRegion = RegionEndpoint.GetBySystemName(RydrEnvironment.GetAppSetting("AWS.Sns.Region", "us-west-2"));

        _platformArnMap = new Dictionary<ServerNotificationMedium, string>
                          {
                              {
                                  ServerNotificationMedium.AppleApn, _awsSnsApnArn
                              },
                              {
                                  ServerNotificationMedium.AppleEnterpriseApn, _awsSnsApnEnterpriseArn
                              },
                              {
                                  ServerNotificationMedium.AndroidGcm, _awsSnsGcmArn
                              }
                          };
    }

    public void PublishPushNotification(string arn, string message)
        => PublishPushNotificationAsync(arn, message).GetAwaiter().GetResult();

    public async Task PublishPushNotificationAsync(string arn, string message)
    {
        using(var client = new AmazonSimpleNotificationServiceClient(_awsAccessKey, _awsSecretKey, _awsSnsRegion))
        {
            await client.PublishAsync(new PublishRequest
                                      {
                                          Message = message,
                                          TargetArn = arn,
                                          MessageStructure = "json"
                                      });
        }
    }

    public async Task PublishTopicNotificationAsync(string topicArn, string subject, string message, IDictionary<string, string> messageAttributes = null)
    {
        var attributes = messageAttributes?.ToDictionarySafe(a => a.Key,
                                                             a => new MessageAttributeValue
                                                                  {
                                                                      DataType = "String",
                                                                      StringValue = a.Value
                                                                  });

        using(var client = new AmazonSimpleNotificationServiceClient(_awsAccessKey, _awsSecretKey, _awsSnsRegion))
        {
            await client.PublishAsync(new PublishRequest
                                      {
                                          TopicArn = topicArn,
                                          Subject = subject.Left(100),
                                          Message = message,
                                          MessageAttributes = attributes.IsNullOrEmptyRydr()
                                                                  ? null
                                                                  : attributes
                                      });
        }
    }

    public SnsEndpointAttributes GetEndpointAttributes(string arn)
    {
        using(var client = new AmazonSimpleNotificationServiceClient(_awsAccessKey, _awsSecretKey, _awsSnsRegion))
        {
            try
            {
                var response = client.GetEndpointAttributesAsync(new GetEndpointAttributesRequest
                                                                 {
                                                                     EndpointArn = arn
                                                                 })
                                     .GetAwaiter()
                                     .GetResult();

                return new SnsEndpointAttributes
                       {
                           IsEnabled = response.Attributes.ContainsKey("Enabled") && response.Attributes["Enabled"].ToBoolean(),
                           Token = response.Attributes.ContainsKey("Token")
                                       ? response.Attributes["Token"]
                                       : null,
                           UserData = response.Attributes.ContainsKey("CustomUserData")
                                          ? response.Attributes["CustomUserData"]
                                          : null
                       };
            }
            catch(NotFoundException)
            {
                return null;
            }
        }
    }

    public string SubscribeToPushNotifications(long publisherAccountId, string deviceToken, ServerNotificationMedium notificationMedium, string deviceInfo)
    {
        Guard.AgainstNullArgument(!deviceToken.HasValue(), "deviceToken");

        var platformArn = _platformArnMap.ContainsKey(notificationMedium)
                              ? _platformArnMap[notificationMedium]
                              : null;

        Guard.AgainstArgumentOutOfRange(platformArn == null, "Platform ARN could not be found for medium specified");

        using(var client = new AmazonSimpleNotificationServiceClient(_awsAccessKey, _awsSecretKey, _awsSnsRegion))
        {
            var response = client.CreatePlatformEndpointAsync(new CreatePlatformEndpointRequest
                                                              {
                                                                  PlatformApplicationArn = platformArn,
                                                                  Token = deviceToken,
                                                                  CustomUserData = string.Concat(publisherAccountId, "|", deviceInfo)
                                                              })
                                 .GetAwaiter()
                                 .GetResult();

            return response.EndpointArn;
        }
    }

    public void UnsubscribeFromPushNotifications(string arn)
    {
        Guard.AgainstNullArgument(!arn.HasValue(), "Arn");

        using(var client = new AmazonSimpleNotificationServiceClient(_awsAccessKey, _awsSecretKey, _awsSnsRegion))
        {
            try
            {
                client.DeleteEndpointAsync(new DeleteEndpointRequest
                                           {
                                               EndpointArn = arn
                                           })
                      .GetAwaiter()
                      .GetResult();
            }
            catch(Exception x)
            {
                _log.Exception(x);
            }
        }
    }

    public void SendSms(string phoneNumber, string message, SmsType messageType, string countryCode)
    {
        phoneNumber = string.Concat(countryCode == null
                                        ? "+1"
                                        : countryCode.PrependIfNotStartsWith("+"),
                                    phoneNumber);

        messageType = messageType == SmsType.Unspecified
                          ? SmsType.Transactional
                          : messageType;

        var messageAttribs = new Dictionary<string, MessageAttributeValue>
                             {
                                 {
                                     "AWS.SNS.SMS.SenderID", new MessageAttributeValue
                                                             {
                                                                 StringValue = _awsSnsSmsSenderId,
                                                                 DataType = "String"
                                                             }
                                 },
                                 {
                                     "AWS.SNS.SMS.SMSType", new MessageAttributeValue
                                                            {
                                                                StringValue = messageType.ToString(),
                                                                DataType = "String"
                                                            }
                                 },
                                 {
                                     "AWS.SNS.SMS.MaxPrice", new MessageAttributeValue
                                                             {
                                                                 StringValue = _awsSnsSmsMaxPrice,
                                                                 DataType = "Number"
                                                             }
                                 }
                             };

        using(var client = new AmazonSimpleNotificationServiceClient(_awsAccessKey, _awsSecretKey, _awsSnsRegion))
        {
            client.PublishAsync(new PublishRequest
                                {
                                    PhoneNumber = phoneNumber,
                                    Message = message,
                                    MessageAttributes = messageAttribs
                                })
                  .GetAwaiter()
                  .GetResult();
        }
    }
}
