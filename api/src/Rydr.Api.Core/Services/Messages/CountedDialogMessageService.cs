using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Messages
{
    public class CountedDialogMessageService : IDialogMessageService, IDialogService
    {
        private const string _dialogLastMsg = "lastmsg";

        private readonly ILog _log = LogManager.GetLogger("CountedDialogMessageService");
        private readonly IDialogMessageService _messageService;
        private readonly IDialogService _messageDialogService;
        private readonly IPersistentCounterAndListService _counterService;
        private readonly ICacheClient _cache;
        private readonly IServerNotificationService _serverNotificationService;
        private readonly ITaskExecuter _taskExecuter;
        private readonly IRequestStateManager _requestStateManager;
        private readonly IDialogCountService _dialogCountService;
        private readonly IPublisherAccountService _publisherAccountService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly IPocoDynamo _dynamoDb;
        private readonly IRydrDataService _rydrDataService;
        private readonly IRecordTypeRecordService _recordTypeRecordService;

        private static readonly CacheConfig _messageServiceCacheConfig = CacheConfig.FromHours(24 * 35);

        public CountedDialogMessageService(IDialogMessageService messageService,
                                           IDialogService messageDialogService,
                                           IPersistentCounterAndListService counterService,
                                           ICacheClient cache,
                                           IServerNotificationService serverNotificationService,
                                           ITaskExecuter taskExecuter,
                                           IRequestStateManager requestStateManager,
                                           IDialogCountService dialogCountService,
                                           IPublisherAccountService publisherAccountService,
                                           IServiceCacheInvalidator serviceCacheInvalidator,
                                           IPocoDynamo dynamoDb,
                                           IRydrDataService rydrDataService,
                                           IRecordTypeRecordService recordTypeRecordService)
        {
            _messageService = messageService;
            _messageDialogService = messageDialogService;
            _counterService = counterService;
            _cache = cache;
            _serverNotificationService = serverNotificationService;
            _taskExecuter = taskExecuter;
            _requestStateManager = requestStateManager;
            _dialogCountService = dialogCountService;
            _publisherAccountService = publisherAccountService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
            _dynamoDb = dynamoDb;
            _rydrDataService = rydrDataService;
            _recordTypeRecordService = recordTypeRecordService;
        }

        public async Task<DialogMessageIds> SendMessageAsync(SendMessage request)
        {
            var response = await _messageService.SendMessageAsync(request);

            if (request.SentByPublisherAccountId > 0)
            {
                await _serviceCacheInvalidator.InvalidatePublisherAccountAsync(request.SentByPublisherAccountId, "notifications", "dialogs");
            }

            _taskExecuter.ExecAsync(new DialogMessageNotification
                                    {
                                        MessageId = response.Id,
                                        DialogId = response.DialogId,
                                        DialogKey = response.DialogKey,
                                        Message = request.Message,
                                        SentBy = response.CreatedBy,
                                        SentByPublisherAccountId = request.SentByPublisherAccountId,
                                        SentOn = DateTimeHelper.UtcNowTs,
                                        ForRecord = request.ForRecord
                                    },
                                    OnSendChatAsync,
                                    true,
                                    maxAttempts: 2);

            return response;
        }

        public async Task MarkMessageReadAsync(long dialogId, long messageId)
        {
            var currentState = _requestStateManager.GetState();

            await _dialogCountService.MarkMessageReadAsync(dialogId, messageId, currentState.RequestPublisherAccountId.Gz(currentState.UserId));
        }

        public async Task MarkDialogReadAsync(long dialogId)
        {
            var currentState = _requestStateManager.GetState();

            await _dialogCountService.MarkDialogReadAsync(dialogId, currentState.RequestPublisherAccountId.Gz(currentState.UserId));
        }

        public async Task<DynDialogMessage> GetLastMessageAsync(long dialogId)
        {
            var lastMsgKey = string.Concat("messages.dialog.", _dialogLastMsg, ".", dialogId);

            var lastSentMsgId = _cache.TryGet<Int64Id>(lastMsgKey);

            if ((lastSentMsgId?.Id ?? 0) > 0)
            {
                var lastMessage = await _messageService.GetMessageAsync(lastSentMsgId.Id);

                return lastMessage;
            }

            // It's unlikely that there is not a last message, this is mostly an optimization above (i.e. cache a pointer and get it), but go get the
            // actual one if it isn't available
            var lastDialogMsg = await _messageService.GetLastMessageAsync(dialogId);

            if (lastDialogMsg == null)
            {
                return null;
            }

            // Got the last msg, set it in cache and return it
            lastSentMsgId = new Int64Id
                            {
                                Id = lastDialogMsg.MessageId
                            };

            await _cache.TrySetAsync(lastSentMsgId, lastMsgKey, _messageServiceCacheConfig);

            return lastDialogMsg;
        }

        public Task<DynDialogMessage> GetMessageAsync(long messageId)
            => _messageService.GetMessageAsync(messageId);

        public async IAsyncEnumerable<DialogMessage> GetDialogMessagesAsync(long dialogId, long sentBefore = 0, long sentAfter = 0, long sentBeforeId = 0, long sentAfterId = 0)
        {
            var currentState = _requestStateManager.GetState();

            using(var counterService = _counterService.CreateStatefulInstance)
            {
                await foreach (var result in _messageService.GetDialogMessagesAsync(dialogId, sentBefore, sentAfter, sentBeforeId, sentAfterId))
                {
                    result.IsRead = _dialogCountService.IsMessageRead(dialogId, result.Id, currentState.RequestPublisherAccountId.Gz(currentState.UserId), counterService);

                    yield return result;
                }
            }
        }

        public async Task<Dialog> GetDialogAsync(long dialogId)
        {
            var dialog = await _messageDialogService.GetDialogAsync(dialogId);

            using(var counterService = _counterService.CreateStatefulInstance)
            {
                var decorated = await DecorateSingleDialogAsync(dialog, counterService);

                return decorated;
            }
        }

        public async IAsyncEnumerable<Dialog> GetDialogsAsync(RecordTypeId forRecord = null, DialogType type = DialogType.Unknown, long forWorkspaceId = 0,
                                                              int skip = 0, int take = 50)
        {
            var currentState = _requestStateManager.GetState();

            using(var counterService = _counterService.CreateStatefulInstance)
            {
                await foreach (var result in _messageDialogService.GetDialogsAsync(forRecord, type, forWorkspaceId, skip, take))
                {
                    var decorated = await DecorateSingleDialogAsync(result, counterService, currentState);

                    yield return decorated;
                }
            }
        }

        // NOTE: Purposely not decorating this...only used internally
        public Task<Dialog> GetOrCreateDialogAsync(HashSet<RecordTypeId> members, long dialogId = 0, RecordTypeId forRecordTypeId = null)
            => _messageDialogService.GetOrCreateDialogAsync(members, dialogId, forRecordTypeId);

        public Task<DynDialog> TryGetDynDialogAsync(IEnumerable<long> forMembers)
            => _messageDialogService.TryGetDynDialogAsync(forMembers);

        public IAsyncEnumerable<DynDialog> GetDialogsAsync(long forWorkspaceId = 0, int skip = 0, int take = 50)
            => _messageDialogService.GetDialogsAsync(forWorkspaceId, skip, take);

        public Task<HashSet<RecordTypeId>> GetDialogMembersAsync(long dialogId)
            => _messageDialogService.GetDialogMembersAsync(dialogId);

        private async Task<Dialog> DecorateSingleDialogAsync(Dialog dialog, ICounterAndListService counterService, IRequestState state = null)
        {
            var lastSentMsg = await GetLastMessageAsync(dialog.Id);

            if (lastSentMsg != null)
            {
                dialog.LastMessage = lastSentMsg.Message ?? string.Empty;
                dialog.LastMessageSentOn = lastSentMsg.CreatedOn;
                dialog.LastMessageSentBy = lastSentMsg.CreatedBy;
                dialog.LastMessageSentByPublisherAccountId = lastSentMsg.SentByPublisherAccountId;
            }

            var currentState = state ?? _requestStateManager.GetState();

            dialog.UnreadMessages = _dialogCountService.GetDialogUnreadCount(dialog.Id, currentState.RequestPublisherAccountId.Gz(currentState.UserId), counterService);

            return dialog;
        }

        private async Task OnSendChatAsync(DialogMessageNotification dialogMessageNotification)
        {
            var lastMsgKey = string.Concat("messages.dialog.", _dialogLastMsg, ".", dialogMessageNotification.DialogId);

            _log.TraceInfoFormat("  OnSendChatAsync storing lastMessageId of [{0}] for dialogId [{1}] using cacheKey of [{2}]", dialogMessageNotification.MessageId, dialogMessageNotification.DialogId, lastMsgKey);

            await _cache.TrySetAsync(new Int64Id
                                     {
                                         Id = dialogMessageNotification.MessageId
                                     },
                                     lastMsgKey, _messageServiceCacheConfig);

            // NOTE: Correctly using dialogMessageNotification.ForRecord here vs. the resolved notificationForRecord below - only map things that are for non-dialog only things
            if (dialogMessageNotification.ForRecord != null && dialogMessageNotification.ForRecord.Id > 0)
            {
                await _dynamoDb.PutItemAsync(new DynItemMap
                                             {
                                                 Id = dialogMessageNotification.ForRecord.Id,
                                                 EdgeId = DynItemMap.BuildEdgeId(DynItemType.Message, dialogMessageNotification.DialogKey),
                                                 ReferenceNumber = dialogMessageNotification.DialogId,
                                                 MappedItemEdgeId = dialogMessageNotification.MessageId.ToEdgeId()
                                             });
            }

            var fromPublisherAccount = dialogMessageNotification.SentByPublisherAccountId > 0
                                           ? await _publisherAccountService.TryGetPublisherAccountAsync(dialogMessageNotification.SentByPublisherAccountId)
                                           : null;

            if (fromPublisherAccount == null || fromPublisherAccount.IsDeleted())
            {
                return;
            }

            var fromPublisherAccountInfo = fromPublisherAccount.ToPublisherAccountInfo();

            var notificationForRecord = dialogMessageNotification.ForRecord ?? new RecordTypeId(RecordType.Dialog, dialogMessageNotification.DialogId);

            if (!dialogMessageNotification.DialogName.HasValue())
            {
                dialogMessageNotification.DialogName = (await _messageDialogService.GetDialogAsync(dialogMessageNotification.DialogId)).Name;
            }

            var notifyServerNotificationMsg = new ServerNotification
                                              {
                                                  From = fromPublisherAccountInfo,
                                                  ForRecord = notificationForRecord,
                                                  ServerNotificationType = ServerNotificationType.Message,
                                                  Title = fromPublisherAccount.UserName,
                                                  Message = dialogMessageNotification.Message
                                              };

            using(var counterService = _counterService.CreateStatefulInstance)
            {
                // We don't send notifications/unread/etc. to the sender of the message
                var dialogMembers = await _messageDialogService.GetDialogMembersAsync(dialogMessageNotification.DialogId);

                foreach (var memberId in dialogMembers.Where(rti => rti.IsUserOrAccountRecordType())
                                                      .Where(i => i.Id != fromPublisherAccountInfo.Id))
                {
                    // Unread messages for this user in this dialog
                    _dialogCountService.AddUnreadMessageForMember(dialogMessageNotification.DialogId, dialogMessageNotification.MessageId, memberId.Id, counterService);

                    // If ForRecord is being tracked, increment unread count for this user to that record
                    _dialogCountService.IncrementUnreadCountForRecord(dialogMessageNotification.ForRecord, memberId.Id, counterService);

                    await _serverNotificationService.NotifyAsync(notifyServerNotificationMsg, memberId);
                }
            }

            try
            {
                // NOTE: Correctly using the ForRecord here to get the record (only want these for non-dialog types), AND correctly using the notificationForRecord.Id in the insert...
                var forRecord = await _recordTypeRecordService.TryGetRecordAsync<IHasNameAndIsRecordLookup>(dialogMessageNotification.ForRecord);

                await _rydrDataService.ExecAdHocAsync(@"
REPLACE   INTO DialogActivity
          (Id, DialogKey, WorkspaceId, ForRecordId, LastMessageSentOn)
VALUES    (@Id, @DialogKey, @WorkspaceId, @ForRecordId, @LastMessageSentOn);
",
                                                      new
                                                      {
                                                          Id = dialogMessageNotification.DialogId,
                                                          dialogMessageNotification.DialogKey,
                                                          WorkspaceId = forRecord?.WorkspaceId ?? fromPublisherAccount.WorkspaceId,
                                                          ForRecordId = notificationForRecord.Id,
                                                          LastMessageSentOn = dialogMessageNotification.SentOn
                                                      });
            }
            catch(Exception x)
            {
                _log.Exception(x, "REPLACE INTO DialogActivity failed");
            }
        }
    }
}
