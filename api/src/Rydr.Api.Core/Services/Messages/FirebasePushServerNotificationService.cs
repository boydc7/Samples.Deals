using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Messages
{
    public class FirebasePushServerNotificationService : IServerNotificationService, IServerNotificationSubsriptionService
    {
        private static readonly string[] _invalidTokenMessageParts =
        {
            "registration token is not a valid", "requested entity was not found", "auth error from APNS"
        };

        private static readonly List<ServerNotificationType> _serverNotificationTypes = EnumsNET.Enums.GetValues<ServerNotificationType>()
                                                                                                .Where(et => et != ServerNotificationType.All &&
                                                                                                             et != ServerNotificationType.Unspecified)
                                                                                                .AsList();

        private readonly ILog _log = LogManager.GetLogger("FirebasePushServerNotificationService");

        private readonly ICacheClient _cacheClient;
        private readonly IPushNotificationMessageFormattingService _pushNotificationMessageFormattingService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IWorkspaceService _workspaceService;
        private readonly IPocoDynamo _dynamoDb;
        private readonly CacheConfig _userSubscribedConfig = CacheConfig.FromHours(1);

        public FirebasePushServerNotificationService(IPocoDynamo dynamoDb,
                                                     ICacheClient cacheClient,
                                                     IPushNotificationMessageFormattingService pushNotificationMessageFormattingService,
                                                     IPublisherAccountService publisherAccountService,
                                                     IWorkspaceService workspaceService)
        {
            _dynamoDb = dynamoDb;
            _cacheClient = cacheClient;
            _pushNotificationMessageFormattingService = pushNotificationMessageFormattingService;
            _publisherAccountService = publisherAccountService;
            _workspaceService = workspaceService;
        }

        public async Task NotifyAsync(ServerNotification message, RecordTypeId notifyRecordId = null)
        {
            Guard.AgainstArgumentOutOfRange(message?.To == null, "Message and notification info invalid");
            Guard.AgainstArgumentOutOfRange(message.ServerNotificationType == ServerNotificationType.Unspecified, "ServerNotificationType");

            var msgParts = _pushNotificationMessageFormattingService.GetMessageParts(message);

            if (msgParts == null)
            {
                return;
            }

            var notification = new Notification
                               {
                                   Body = msgParts.Body,
                                   Title = msgParts.Title
                               };

            var data = new Dictionary<string, string>
                       {
                           {
                               "click_action", "FLUTTER_NOTIFICATION_CLICK"
                           }
                       };

            if (msgParts.CustomObj != null)
            {
                data.Add("RydrObject", msgParts.CustomObj.ToJson());
            }

            // Send push notifications
            await foreach (var token in GetSubscribedTokensAsync(message.To.Id, message.InWorkspaceId, message.ServerNotificationType))
            {
                try
                {
                    // ReSharper disable once UnusedVariable
                    var pubMsg = new Message
                                 {
                                     Data = data,
                                     Notification = notification,
                                     Token = token,
                                     Apns = new ApnsConfig
                                            {
                                                Aps = new Aps
                                                      {
                                                          Badge = (int)msgParts.Badge
                                                      }
                                            }
                                 };

#if LOCALDEBUG
                    _log.DebugInfoFormat("Sending PushNotification with title [{0}] from PublisherAccountId [{1}] to PublisherAccountId [{2}] for Record [{3}] - token prefix [{4}]",
                                         message.Title, message.From?.Id, message.To.Id, message.ForRecord?.ToString(), token.Left(30));
#else
                    await FirebaseMessaging.DefaultInstance.SendAsync(pubMsg);
#endif
                }
                catch(FirebaseMessagingException fbmx) when(fbmx.Message.ContainsAny(_invalidTokenMessageParts, StringComparison.OrdinalIgnoreCase))
                {
                    await _dynamoDb.DeleteItemAsync<DynInfo>(message.To.Id, token);
                }
                catch(FirebaseException fbx) when(fbx.Message.ContainsAny(_invalidTokenMessageParts, StringComparison.OrdinalIgnoreCase))
                {
                    await _dynamoDb.DeleteItemAsync<DynInfo>(message.To.Id, token);
                }
                catch(Exception x)
                {
                    _log.Exception(x, $"Could not publish to cid [{message.To.Id}], token [{token}]");
                }
            }
        }

        public async Task SubscribeAsync(long userId, string token, string oldTokenHash)
        {
            if (!token.HasValue())
            {
                _log.Warn("Tried to subscribe to valid channel/medium, however no Token was included");

                return;
            }

            var tokenHash = token.ToSafeSha64();
            var edgeId = GetSubscribeDynInfoEdgeId(tokenHash);

            // If we have an old tokenHash that is the same as the new one being registered, we have to get any existing token registrations, as we need
            // to remove any that are mapped to a publisher account this user is not mapped to any longer.  If we have an old tokenHash that is different
            // from the one being registered, just delete all the existing ones
            HashSet<long> existingPublisherAccountIds = null;

            if (oldTokenHash.HasValue())
            {
                if (tokenHash.EqualsOrdinal(oldTokenHash))
                { // Same hashes...get existing publisherAccountIds so we can delete any we do not process below with the new token
                    existingPublisherAccountIds = await _dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == edgeId &&
                                                                                                                Dynamo.BeginsWith(i.TypeReference,
                                                                                                                                  string.Concat((int)DynItemType.FirebasePushToken, "|")))
                                                                 .Filter(i => i.TypeId == (int)DynItemType.FirebasePushToken)
                                                                 .Select(i => new
                                                                              {
                                                                                  i.Id
                                                                              })
                                                                 .ExecAsync()
                                                                 .Select(r => r.Id)
                                                                 .ToHashSet();
                }
                else
                { // Different hashes, just delete all the existing ones
                    await UnsubscribeAsync(oldTokenHash);
                }
            }

            // A map of the publisherAccounts this user has access to
            // to
            // each distinct contextWorkspaceId and the specific workspaceIds within each context
            var publisherContextWorkspaceMap = new Dictionary<DynPublisherAccount, Dictionary<long, HashSet<long>>>(DynPublisherAccount.DefaultComparer);

            // Build up the map...
            await foreach (var userWorkspace in _workspaceService.GetUserWorkspacesAsync(userId)
                                                                 .Where(w => !w.IsDeleted()))
            {
                await foreach (var userWorkspacePublisherAccount in _workspaceService.GetWorkspaceUserPublisherAccountsAsync(userWorkspace.Id, userId))
                {
                    // The outer key of the outer map, i.e. the publisher account
                    if (!publisherContextWorkspaceMap.ContainsKey(userWorkspacePublisherAccount))
                    {
                        publisherContextWorkspaceMap[userWorkspacePublisherAccount] = new Dictionary<long, HashSet<long>>();
                    }

                    // Context of this workpsace for this publisher account (the key to the inner map)
                    var contextWorkspaceId = userWorkspace.GetContextWorkspaceId(userWorkspacePublisherAccount.RydrAccountType);

                    if (!publisherContextWorkspaceMap[userWorkspacePublisherAccount].ContainsKey(contextWorkspaceId))
                    {
                        publisherContextWorkspaceMap[userWorkspacePublisherAccount][contextWorkspaceId] = new HashSet<long>();
                    }

                    // And always add the specific workpsace to the context's map
                    publisherContextWorkspaceMap[userWorkspacePublisherAccount][contextWorkspaceId].Add(userWorkspace.Id);
                }
            }

            // Create a token subscription for each publisher account the user has access to with the various workspace
            // and context info embedded within that (might be multiple workspaces for a given publisher account and user/token)
            foreach (var publisherContextWorkspaceItem in publisherContextWorkspaceMap)
            {
                existingPublisherAccountIds?.Remove(publisherContextWorkspaceItem.Key.PublisherAccountId);

                if (publisherContextWorkspaceItem.Value.IsNullOrEmptyRydr() || publisherContextWorkspaceItem.Key.IsDeleted())
                {
                    await _dynamoDb.TryDeleteItemAsync<DynInfo>(publisherContextWorkspaceItem.Key.PublisherAccountId, edgeId);

                    continue;
                }

                // No need to get existing, update, etc. - if one already exists, we just overwrite it with the new info
                var dynInfo = new DynInfo
                              {
                                  Id = publisherContextWorkspaceItem.Key.PublisherAccountId,
                                  EdgeId = edgeId,
                                  Info = new TokenSubscribeInfo
                                         {
                                             Token = token,
                                             WorkspaceContextMap = publisherContextWorkspaceItem.Value
                                         }.ToJsv(),
                                  DynItemType = DynItemType.FirebasePushToken,
                                  ExpiresAt = DateTimeHelper.UtcNow.AddDays(45).ToUnixTimestamp(),
                                  ReferenceId = userId.ToStringInvariant()
                              };

                await _dynamoDb.PutItemTrackedAsync(dynInfo);
            }

            if (!existingPublisherAccountIds.IsNullOrEmpty())
            { // Have some to remove...
                await _dynamoDb.DeleteItemsAsync<DynInfo>(existingPublisherAccountIds.Select(xid => new DynamoId(xid, edgeId)));
            }
        }

        public async Task UnsubscribeAsync(string tokenHash)
        {
            // Unsubscribing a token subscription is simple, just delete them all for a given hash
            var edgeId = GetSubscribeDynInfoEdgeId(tokenHash);

            await _dynamoDb.DeleteItemsFromAsync<DynInfo, DynItemEdgeIdGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == edgeId &&
                                                                                                                                            Dynamo.BeginsWith(i.TypeReference,
                                                                                                                                                              string.Concat((int)DynItemType.FirebasePushToken, "|")))
                                                                                             .Filter(i => i.TypeId == (int)DynItemType.FirebasePushToken)
                                                                                             .Select(i => new
                                                                                                          {
                                                                                                              i.Id,
                                                                                                              i.EdgeId
                                                                                                          })
                                                                                             .ExecAsync(),
                                                                                    i => i.GetDynamoId());
        }

        public async Task AddSubscriptionAsync(long userId, long workspaceId, long publisherAccountId, ServerNotificationType toNotificationType)
        {
            var dynInfo = await _dynamoDb.GetItemAsync<DynInfo>(userId, GetSubscriptionDynInfoEdgeId(workspaceId, publisherAccountId))
                          ??
                          CreateSubscriptionDynInfo(userId, workspaceId, publisherAccountId);

            var subscriptionInfo = dynInfo.Info?.ToString()?.FromJsv<NotificationSubscriptionInfo>()
                                   ??
                                   new NotificationSubscriptionInfo();

            if (toNotificationType == ServerNotificationType.All)
            { // Subscribing to all, just empty out the unsubscibed set
                subscriptionInfo.UnsubscribedFrom = new HashSet<ServerNotificationType>();
            }
            else
            { // Subscribing to one thing...
                if (subscriptionInfo.UnsubscribedFrom.Contains(ServerNotificationType.All))
                { // Unsubscribed from everything, add all but the one being subscribed to
                    subscriptionInfo.UnsubscribedFrom = _serverNotificationTypes.Where(et => et != toNotificationType)
                                                                                .AsHashSet();
                }
                else
                { // Currently unsubscribed from some things, remove this one if there
                    if (!subscriptionInfo.UnsubscribedFrom.Remove(toNotificationType))
                    {
                        return;
                    }
                }
            }

            var contextWorkspaceId = (await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId)).GetContextWorkspaceId(workspaceId);

            dynInfo.Info = subscriptionInfo.ToJsv();

            await _dynamoDb.PutItemTrackedAsync(dynInfo);

            _cacheClient.TryRemove<ValueWrap<bool>>(GetCacheId(userId, contextWorkspaceId, publisherAccountId, toNotificationType));
        }

        public async Task DeleteSubscriptionAsync(long userId, long workspaceId, long publisherAccountId, ServerNotificationType fromNotificationType)
        {
            var dynInfo = await _dynamoDb.GetItemAsync<DynInfo>(userId, GetSubscriptionDynInfoEdgeId(workspaceId, publisherAccountId))
                          ??
                          CreateSubscriptionDynInfo(userId, workspaceId, publisherAccountId);

            var subscriptionInfo = dynInfo.Info?.ToString()?.FromJsv<NotificationSubscriptionInfo>()
                                   ??
                                   new NotificationSubscriptionInfo();

            if (subscriptionInfo.UnsubscribedFrom.Contains(ServerNotificationType.All) ||
                subscriptionInfo.UnsubscribedFrom.Contains(fromNotificationType))
            { // Already unsubscribed
                return;
            }

            if (fromNotificationType == ServerNotificationType.All)
            { // Simple, single entry of all...
                subscriptionInfo.UnsubscribedFrom = new HashSet<ServerNotificationType>
                                                    {
                                                        ServerNotificationType.All
                                                    };
            }
            else
            { // Add the notification type as unsubscribed
                if (!subscriptionInfo.UnsubscribedFrom.Add(fromNotificationType))
                {
                    return;
                }
            }

            var contextWorkspaceId = (await _publisherAccountService.GetPublisherAccountAsync(publisherAccountId)
                                     ).GetContextWorkspaceId(workspaceId);

            dynInfo.Info = subscriptionInfo.ToJsv();

            await _dynamoDb.PutItemTrackedAsync(dynInfo);

            _cacheClient.TryRemove<ValueWrap<bool>>(GetCacheId(userId, contextWorkspaceId, publisherAccountId, fromNotificationType));
        }

        public async Task<IReadOnlyList<(ServerNotificationType Type, bool IsSubscribed)>> GetSubscriptionsAsync(long userId, long workspaceId, long publisherAccountId)
        {
            if (!(await _workspaceService.UserHasAccessToAccountAsync(workspaceId, userId, publisherAccountId)))
            {
                return _serverNotificationTypes.Select(t => (t, false)).AsListReadOnly();
            }

            var edgeId = GetSubscriptionDynInfoEdgeId(workspaceId, publisherAccountId);

            var dynInfo = await _dynamoDb.GetItemAsync<DynInfo>(userId, edgeId);

            var subscriptionInfo = (dynInfo?.Info).HasValue()
                                       ? dynInfo.Info.ToString().FromJsv<NotificationSubscriptionInfo>()
                                       : null;

            if (subscriptionInfo == null)
            { // By default subscribed to everything, clear this out (it's invalid) and return subscribed all
                await _dynamoDb.TryDeleteItemAsync<DynInfo>(userId, edgeId);

                return _serverNotificationTypes.Select(t => (t, true)).AsListReadOnly();
            }

            if (subscriptionInfo.UnsubscribedFrom.IsNullOrEmpty())
            { // Subscribed to everything (i.e. no exception list)
                return _serverNotificationTypes.Select(t => (t, true)).AsListReadOnly();
            }

            // Unsubscribed from everything, or just some things
            return subscriptionInfo.UnsubscribedFrom.Contains(ServerNotificationType.All)
                       ? _serverNotificationTypes.Select(t => (t, false)).AsListReadOnly()
                       : _serverNotificationTypes.Select(t => (t, !subscriptionInfo.UnsubscribedFrom.Contains(t))).AsListReadOnly();
        }

        private async Task<bool> IsSubscribedAsync(long userId, long contextWorkspaceId, long publisherAccountId, IEnumerable<long> workspaceIds, ServerNotificationType toNotificationType)
        {
            var cacheKey = GetCacheId(userId, contextWorkspaceId, publisherAccountId, toNotificationType);

            var isUserValueWrap = await _cacheClient.TryGetTaskAsync(cacheKey,
                                                                     async () =>
                                                                     {
                                                                         var isSubscribed = false;

                                                                         foreach (var workspaceId in workspaceIds)
                                                                         {
                                                                             var subscriptions = await GetSubscriptionsAsync(userId, workspaceId, publisherAccountId);

                                                                             // For push notification purposes, we cache keys in the contextWorkspace...but to determine if they are subscribed, we have to use
                                                                             // the actual workspace the context is valid within
                                                                             isSubscribed = subscriptions.Any(s => s.Type == toNotificationType && s.IsSubscribed);

                                                                             if (isSubscribed)
                                                                             {
                                                                                 break;
                                                                             }
                                                                         }

                                                                         var isSub = new ValueWrap<bool>
                                                                                     {
                                                                                         Value = isSubscribed
                                                                                     };

                                                                         return isSub;
                                                                     },
                                                                     _userSubscribedConfig);

            return isUserValueWrap?.Value ?? false;
        }

        private DynInfo CreateSubscriptionDynInfo(long userId, long workspaceId, long publisherAccountId)
        {
            var newDynInfo = new DynInfo
                             {
                                 Id = userId,
                                 EdgeId = GetSubscriptionDynInfoEdgeId(workspaceId, publisherAccountId),
                                 Info = new NotificationSubscriptionInfo
                                        {
                                            UnsubscribedFrom = new HashSet<ServerNotificationType>()
                                        }.ToJsv(),
                                 DynItemType = DynItemType.NotificationSubscription,
                                 ReferenceId = publisherAccountId.ToStringInvariant()
                             };

            newDynInfo.UpdateDateTimeTrackedValues();

            newDynInfo.WorkspaceId = workspaceId;

            return newDynInfo;
        }

        private string GetSubscriptionDynInfoEdgeId(long workspaceId, long publisherAccountId)
            => string.Concat((int)DynItemType.NotificationSubscription, "|", workspaceId, "|", publisherAccountId);

        private string GetSubscribeDynInfoEdgeId(string tokenHash)
            => string.Concat((int)DynItemType.FirebasePushToken, "|", tokenHash);

        private string GetCacheId(long userId, long contextWorkspaceId, long publisherAccountId, ServerNotificationType notificationType)
            => string.Concat("fbpns|", userId, "|", contextWorkspaceId, "|",
                             publisherAccountId, "|", ((int)notificationType).ToString());

        private async IAsyncEnumerable<string> GetSubscribedTokensAsync(long publisherAccountId, long contextWorkspaceId, ServerNotificationType forNotificationType)
        {
            await foreach (var publisherTokenInfo in _dynamoDb.FromQuery<DynInfo>(i => i.Id == publisherAccountId &&
                                                                                       Dynamo.BeginsWith(i.EdgeId, string.Concat((int)DynItemType.FirebasePushToken, "|")))
                                                              .Filter(i => i.DeletedOnUtc == null &&
                                                                           i.TypeId == (int)DynItemType.FirebasePushToken)
                                                              .ExecAsync())
            {
                var tokenSubscribeInfo = publisherTokenInfo.Info?.ToString()?.FromJsv<TokenSubscribeInfo>();

                if ((tokenSubscribeInfo?.Token).IsNullOrEmpty() || tokenSubscribeInfo.WorkspaceContextMap.IsNullOrEmptyRydr())
                {
                    await _dynamoDb.TryDeleteItemAsync<DynInfo>(publisherTokenInfo.Id, publisherTokenInfo.EdgeId);

                    continue;
                }

                // If the given token subscription isn't valid in the requested contextWorkspace, nothing to send
                if (!tokenSubscribeInfo.WorkspaceContextMap.ContainsKey(contextWorkspaceId))
                {
                    continue;
                }

                // Have a valid token subscription, if the user is subscribed to the given context in some way, yield this one out
                if (await IsSubscribedAsync(publisherTokenInfo.ReferenceId.ToLong(), contextWorkspaceId, publisherAccountId,
                                            tokenSubscribeInfo.WorkspaceContextMap[contextWorkspaceId], forNotificationType))
                {
                    yield return tokenSubscribeInfo.Token;
                }
            }
        }

        private class NotificationSubscriptionInfo
        {
            public HashSet<ServerNotificationType> UnsubscribedFrom { get; set; }
        }

        private class TokenSubscribeInfo
        {
            public string Token { get; set; }
            public Dictionary<long, HashSet<long>> WorkspaceContextMap { get; set; } // ContextWorkspaceIds is key, list of actual workspaceIds is value
        }
    }
}
