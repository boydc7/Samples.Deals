using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Messages
{
    public class LogServerNotificationService : IServerNotificationService, IUserNotificationService
    {
        private const long _maxUnreadSetItemCount = 250;

        private readonly IPocoDynamo _dynamoDb;
        private readonly IRecordTypeRecordService _recordTypeRecordService;
        private readonly IPersistentCounterAndListService _counterAndListService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly ILog _log = LogManager.GetLogger("LogServerNotificationService");

        public LogServerNotificationService(IPocoDynamo dynamoDb,
                                            IRecordTypeRecordService recordTypeRecordService,
                                            IPersistentCounterAndListService counterAndListService,
                                            IServiceCacheInvalidator serviceCacheInvalidator)
        {
            _dynamoDb = dynamoDb;
            _recordTypeRecordService = recordTypeRecordService;
            _counterAndListService = counterAndListService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
        }

        public async Task NotifyAsync(ServerNotification message, RecordTypeId notifyRecordId = null)
        {
            var toPublisherAccountId = message.To?.Id ??
                                       (notifyRecordId.Type == RecordType.PublisherAccount
                                            ? notifyRecordId.Id
                                            : 0);

            if (toPublisherAccountId <= 0)
            {
                return;
            }

            var title = message.Title ?? (notifyRecordId == null
                                              ? message.From == null
                                                    ? "New Notification"
                                                    : message.From.UserName
                                              : (await _recordTypeRecordService.TryGetRecordAsync<IHasNameAndIsRecordLookup>(message.ForRecord))?.Name);

            var isMessage = message.ServerNotificationType == ServerNotificationType.Message;
            var isDialogOrMessage = isMessage || message.ServerNotificationType.IsDialogOrMessageNotification();

            var utcNow = DateTimeHelper.UtcNow;

            var dynNotification = new DynNotification
                                  {
                                      ToPublisherAccountId = toPublisherAccountId,
                                      FromPublisherAccountId = message.From?.Id ?? 0,
                                      EdgeId = DynNotification.BuildEdgeId(message.ServerNotificationType, message.ForRecord),
                                      ForRecord = message.ForRecord,
                                      NotificationType = message.ServerNotificationType,
                                      Title = title,
                                      Message = isMessage // Messages we keep the most recent message, first 50
                                                    ? message.Message.Left(50)
                                                    : isDialogOrMessage // Dialogs, nothing...
                                                        ? null
                                                        : message.Message,
                                      DynItemType = DynItemType.Notification,
                                      ReferenceId = string.Concat(message.InWorkspaceId.ToEdgeId(), "|", utcNow.ToUnixTimestamp().ToStringInvariant()),
                                      ExpiresAt = utcNow.AddDays(95).ToUnixTimestamp(),
                                      IsRead = false
                                  };

            dynNotification.UpdateDateTimeTrackedValues();

            if (message.InWorkspaceId > 0)
            {
                dynNotification.WorkspaceId = message.InWorkspaceId;
            }

            _log.TraceInfoFormat("Saving Notification FromPublisherAccountId [{0}] ToPublisherAccountId [{1}] for Record [{2}]",
                                 dynNotification.FromPublisherAccountId, dynNotification.ToPublisherAccountId, dynNotification.ForRecord?.ToString());

            await _dynamoDb.PutItemAsync(dynNotification);

            AddUnread(toPublisherAccountId, dynNotification.EdgeId, message.InWorkspaceId);

            // Flush the notification cache
            await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(dynNotification.ToPublisherAccountId, "notifications", isDialogOrMessage
                                                                                                                                      ? "dialogs"
                                                                                                                                      : string.Empty);
        }

        public Task SubscribeAsync(long userId, string token, string oldTokenHash) => Task.CompletedTask;

        public Task UnsubscribeAsync(string tokenHash) => Task.CompletedTask;

        public long GetUnreadCount(long publisherAccountId, long workspaceId)
        {
            var key = GetTotalUnreadKey(publisherAccountId, workspaceId);

            var unreadCount = _counterAndListService.CountOfUniqueItems(key);

            if (unreadCount > _maxUnreadSetItemCount)
            {
                _counterAndListService.PopUniqueItems(key, (int)((unreadCount - _maxUnreadSetItemCount) + (_maxUnreadSetItemCount * .2)));
            }

            return unreadCount;
        }

        public void AddUnread(long publisherAccountId, string notificationEdgeId, long workspaceId)
            => _counterAndListService.AddUniqueItem(GetTotalUnreadKey(publisherAccountId, workspaceId), notificationEdgeId);

        public void RemoveUnread(long publisherAccountId, string notificationEdgeId, long workspaceId)
            => _counterAndListService.RemoveUniqueItem(GetTotalUnreadKey(publisherAccountId, workspaceId), notificationEdgeId);

        public void RemoveAllUnread(long publisherAccountId, long workspaceId)
            => _counterAndListService.Clear(GetTotalUnreadKey(publisherAccountId, workspaceId));

        private string GetTotalUnreadKey(long publisherAccountId, long workspaceId)
            => string.Concat("urn:servernotifications.totalunread.", workspaceId, "|", publisherAccountId);
    }
}
