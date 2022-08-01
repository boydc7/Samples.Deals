using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;
using ServiceStack.Text;

namespace Rydr.Api.Core.Services.Messages
{
    public class ManagedWorkspaceServerNotificationService : IServerNotificationService
    {
        private static readonly HashSet<ServerNotificationType> _handledNotificationTypes = new HashSet<ServerNotificationType>
                                                                                            {
                                                                                                ServerNotificationType.DealCompleted,
                                                                                                ServerNotificationType.DealRequested,
                                                                                                ServerNotificationType.DealRequestRedeemed,
                                                                                                ServerNotificationType.DealRequestCompleted,
                                                                                                ServerNotificationType.Message,
                                                                                                ServerNotificationType.Dialog,
                                                                                            };

        private readonly ILog _log = LogManager.GetLogger("ManagedWorkspaceServerNotificationService");
        private readonly IOpsNotificationService _opsNotificationService;
        private readonly IPocoDynamo _dynamoDb;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IPushNotificationMessageFormattingService _pushNotificationMessageFormattingService;

        public ManagedWorkspaceServerNotificationService(IOpsNotificationService opsNotificationService,
                                                         IPocoDynamo dynamoDb,
                                                         IPublisherAccountService publisherAccountService,
                                                         IPushNotificationMessageFormattingService pushNotificationMessageFormattingService)
        {
            _opsNotificationService = opsNotificationService;
            _dynamoDb = dynamoDb;
            _publisherAccountService = publisherAccountService;
            _pushNotificationMessageFormattingService = pushNotificationMessageFormattingService;
        }

        public async Task NotifyAsync(ServerNotification message, RecordTypeId notifyRecordId = null)
        {
            try
            {
                await DoNotifyAsync(message);
            }
            catch(Exception x)
            {
                // Don't let exceptions in our operational stuff impact app operations
                _log.Exception(x);
            }
        }

        private async Task DoNotifyAsync(ServerNotification message)
        {
            if (!_handledNotificationTypes.Contains(message.ServerNotificationType))
            {
                return;
            }

            var isRydrWorkspace = WorkspaceExtensions.IsRydrWorkspace(message.InWorkspaceId);

            var dynDeal = message.ForRecord == null || message.ForRecord.Type != RecordType.Deal
                              ? null
                              : await DealExtensions.DefaultDealService.GetDealAsync(message.ForRecord.Id, true);

            isRydrWorkspace = isRydrWorkspace || WorkspaceExtensions.IsRydrWorkspace(dynDeal?.WorkspaceId ?? 0);

            if (!isRydrWorkspace)
            {
                return;
            }

            var location = dynDeal == null
                               ? null
                               : (await _dynamoDb.TryGetPlaceAsync(dynDeal.ReceivePlaceId.Gz(dynDeal.PlaceId))).ToDisplayLocation();

            var dealPublisherAccount = (await _publisherAccountService.GetPublisherAccountAsync(dynDeal?.PublisherAccountId ?? 0)).ToPublisherAccountInfo()
                                       ??
                                       message.To;

            if (!TryGetActionableNotification(message, dynDeal, dealPublisherAccount, location, out var alertSubject, out var alertMessage))
            {
                return;
            }

            // Managed workspace for internal team....send a slack notification
            if (alertSubject.HasValue() && alertMessage.HasValue())
            {
                await _opsNotificationService.SendManagedAccountNotificationAsync(alertSubject, alertMessage);
            }
        }

        public Task SubscribeAsync(long userId, string token, string oldTokenHash) => Task.CompletedTask;

        public Task UnsubscribeAsync(string tokenHash) => Task.CompletedTask;

        private bool TryGetActionableNotification(ServerNotification message, DynDeal dynDeal, PublisherAccountInfo dealPublisherAccount, string dealDisplayLocation,
                                                  out string alertSubject, out string alertMessage)
        {
            alertSubject = null;
            alertMessage = null;

            var dealId = dynDeal?.Id ?? 0;
            var dealTitle = dynDeal?.Title ?? "Unknown";

            var msgParts = _pushNotificationMessageFormattingService.GetMessageParts(message);

            if ((msgParts?.CustomObj == null) || !(msgParts.CustomObj is ServerNotificationPushNotificationObject serverPushObject))
            {
                return false;
            }

            // Clear out stuff that we do not want to encode
            static void scrubPublisherInfo(PublisherAccountInfo info)
            {
                if (info == null)
                {
                    return;
                }

                info.Metrics = null;
                info.FullName = null;
                info.Description = null;
                info.Website = null;
                info.Tags = null;
                info.ProfilePicture = null;
                info.OptInToAi = null;
            }

            scrubPublisherInfo(serverPushObject.ToPublisherAccount);
            scrubPublisherInfo(serverPushObject.FromPublisherAccount);

            string encodedRydrNotifyMsg = null;

            using(JsConfig.With(new Config
                                {
                                    ExcludeTypeInfo = true,
                                    TextCase = TextCase.CamelCase,
                                    DateHandler = DateHandler.ISO8601,
                                    AssumeUtc = true
                                }))
            {
                var jsonObj = new
                              {
                                  data = new Dictionary<string, object>
                                         {
                                             {
                                                 "RydrObject", msgParts.CustomObj
                                             }
                                         },
                                  notification = new
                                                 {
                                                     // Purposely leaving out the body/title info, as we don't need it in the deep link, only
                                                     // used to navigate user into the correct location in the app from a link, this would just
                                                     // unnesseccarily increase the size of the url, probably greatly
                                                     body = string.Empty,
                                                     title = string.Empty
                                                 }
                              };

                encodedRydrNotifyMsg = jsonObj.ToJson().UrlEncode();
            }

            if (encodedRydrNotifyMsg.IsNullOrEmpty())
            {
                return false;
            }

            var rydrLink = $"<https://in.getrydr.com/share/?link=https://dl.getrydr.com/notify?rydrmsg={encodedRydrNotifyMsg}&apn=com.rydr.app&isi=1480064664&ibi=com.rydr.app&ofl=https://app.getrydr.com&efr=1|View in PostPact>";

            if (message.ServerNotificationType == ServerNotificationType.DealCompleted)
            { // Always actionable for the business (their deal is exhausted)
                alertSubject = "Deal Completed - Max Approvals Met";

                alertMessage = string.Concat($@"
Deal      :      {dealTitle} ({dealId})
Business  :      <https://instagram.com/{dealPublisherAccount.UserName}|IG: {dealPublisherAccount.UserName}> ({dealPublisherAccount.Id})",
                                             dealDisplayLocation.IsNullOrEmpty()
                                                 ? null
                                                 : @"
Location  :
", dealDisplayLocation, $@"

{rydrLink}
");

                return true;
            }

            if (message.ServerNotificationType == ServerNotificationType.DealRequested)
            { // Deal requested is always something the business has to take action on if not auto-approving
                if (dynDeal != null && dynDeal.AutoApproveRequests)
                {
                    return false;
                }

                alertSubject = string.Concat($"{dealPublisherAccount.UserName}> Deal ",
                                             message.From == null
                                                 ? message.Title
                                                 : $"Requested by {message.From.UserName}");

                alertMessage = string.Concat($@"
Deal      :      {dealTitle} ({dealId})",
                                             message.From == null
                                                 ? string.Empty
                                                 : $@"
Creator   :      <https://instagram.com/{message.From.UserName}|IG: {message.From.UserName}> ({message.From.Id})
Business  :      <https://instagram.com/{dealPublisherAccount.UserName}|IG: {dealPublisherAccount.UserName}> ({dealPublisherAccount.Id})

{rydrLink}
");

                return true;
            }

            if (message.ServerNotificationType == ServerNotificationType.DealRequestRedeemed ||
                message.ServerNotificationType == ServerNotificationType.DealRequestCompleted)
            { // Actionable if the business is receiving the notification
                if ((dynDeal != null && message.To.Id != dynDeal.PublisherAccountId) ||
                    (dynDeal == null && !message.To.IsBusiness()))
                {
                    return false;
                }

                alertSubject = string.Concat($"{dealPublisherAccount.UserName}> Deal ",
                                             message.From == null
                                                 ? message.Title
                                                 : $"{(message.ServerNotificationType == ServerNotificationType.DealRequestRedeemed ? "Redeemed" : "Completed")} by {message.From.UserName}");

                alertMessage = string.Concat($@"
Deal      :      {dealTitle} ({dealId})",
                                             message.From == null
                                                 ? string.Empty
                                                 : $@"
Creator   :      <https://instagram.com/{message.From.UserName}|IG: {message.From.UserName}> ({message.From.Id})
Business  :      <https://instagram.com/{dealPublisherAccount.UserName}|IG: {dealPublisherAccount.UserName}> ({dealPublisherAccount.Id})

{rydrLink}
");

                return true;
            }

            if (message.ServerNotificationType == ServerNotificationType.Message ||
                message.ServerNotificationType == ServerNotificationType.Dialog)
            { // Actionable if the business is receiving the notification
                if ((dynDeal != null && message.To.Id != dynDeal.PublisherAccountId) ||
                    (dynDeal == null && !message.To.IsBusiness()))
                {
                    return false;
                }

                alertSubject = alertSubject = string.Concat($"Message for {dealPublisherAccount.UserName} from ",
                                                            message.From == null
                                                                ? message.Title
                                                                : $"{message.From.UserName}");

                alertMessage = string.Concat($@"
Message   :      {message.Message.Left(1000)}
Deal      :      {dealTitle} ({dealId})",
                                             message.From == null
                                                 ? string.Empty
                                                 : $@"
Creator   :      <https://instagram.com/{message.From.UserName}|IG: {message.From.UserName}> ({message.From.Id})
Business  :      <https://instagram.com/{dealPublisherAccount.UserName}|IG: {dealPublisherAccount.UserName}> ({dealPublisherAccount.Id})

{rydrLink}
");

                return true;
            }

            return false;
        }
    }
}
