using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Services.Messages
{
    public class CounterListDialogCountService : IDialogCountService
    {
        private const string _dialogCountKeyUnread = "unread";

        private readonly IPersistentCounterAndListService _counterService;
        private readonly IAssociationService _associationService;

        public CounterListDialogCountService(IPersistentCounterAndListService counterService, IAssociationService associationService)
        {
            _counterService = counterService;
            _associationService = associationService;
        }

        public async Task MarkMessageReadAsync(long dialogId, long messageId, long memberId)
        {
            var unreadDialogMessagesKey = GetCounterKey(_dialogCountKeyUnread, dialogId, memberId);
            var unreadAllCountKey = GetCounterKey(_dialogCountKeyUnread, 0, memberId);

            using(var counterService = _counterService.CreateStatefulInstance)
            {
                if (!counterService.RemoveUniqueItem(unreadDialogMessagesKey, messageId.ToStringInvariant()))
                {
                    return;
                }

                counterService.DecrementNonNegative(unreadAllCountKey);

                await DecrementUnreadForAssociatedRecordsAsync(dialogId, memberId, counterService);
            }
        }

        public async Task MarkDialogReadAsync(long dialogId, long memberId)
        {
            var unreadDialogMessagesKey = GetCounterKey(_dialogCountKeyUnread, dialogId, memberId);
            var unreadAllCountKey = GetCounterKey(_dialogCountKeyUnread, 0, memberId);

            using(var counterService = _counterService.CreateStatefulInstance)
            {
                // Remove the counter list for this dialog and drop the unread count to 0
                var xCount = counterService.CountOfUniqueItems(unreadDialogMessagesKey).ToInteger();

                if (xCount <= 0)
                {
                    return;
                }

                // Have to get the count to know how far to decrement the total by, but since we have it may as well
                // use it to validate if we need to bother to do anything...
                counterService.Clear(unreadDialogMessagesKey);
                counterService.DecrementNonNegative(unreadAllCountKey, xCount);

                await DecrementUnreadForAssociatedRecordsAsync(dialogId, memberId, counterService);
            }
        }

        private async Task DecrementUnreadForAssociatedRecordsAsync(long dialogId, long memberId, ICounterAndListService countService = null)
        {
            await foreach (var dialogAssocation in _associationService.GetAssociationsToAsync(dialogId, toRecordType: RecordType.Dialog))
            {
                DecrementUnreadCountForRecord(new RecordTypeId(dialogAssocation.IdRecordType, dialogAssocation.Id), memberId,
                                              countService ?? _counterService);
            }
        }

        public long IncrementUnreadCountForRecord(RecordTypeId forRecord, long memberId, ICounterAndListService countService = null)
            => forRecord == null
                   ? 0
                   : (countService ?? _counterService).Increment(GetCounterKey(_dialogCountKeyUnread, memberId, forRecord));

        public long DecrementUnreadCountForRecord(RecordTypeId forRecord, long memberId, ICounterAndListService countService = null)
            => forRecord == null
                   ? 0
                   : (countService ?? _counterService).DecrementNonNegative(GetCounterKey(_dialogCountKeyUnread, memberId, forRecord));

        public long AddUnreadMessageForMember(long dialogId, long messageId, long memberId, ICounterAndListService countService = null)
        {
            // Add messageId as unread for user
            (countService ?? _counterService).AddUniqueItem(GetCounterKey(_dialogCountKeyUnread, dialogId, memberId), messageId.ToStringInvariant());

            // Increment total unread for user
            var totalUnread = (countService ?? _counterService).Increment(GetCounterKey(_dialogCountKeyUnread, 0, memberId));

            return totalUnread;
        }

        public bool IsMessageRead(long dialogId, long messageId, long memberId, ICounterAndListService countService = null)
            => !(countService ?? _counterService).Exists(GetCounterKey(_dialogCountKeyUnread, dialogId, memberId), messageId.ToStringInvariant());

        public long GetDialogUnreadCount(long dialogId, long memberId, ICounterAndListService countService = null)
            => (countService ?? _counterService).CountOfUniqueItems(GetCounterKey(_dialogCountKeyUnread, dialogId, memberId));

        public long GetMemberTotalUnreadCount(long memberId, ICounterAndListService countService = null)
            => (countService ?? _counterService).GetCounter(GetCounterKey(_dialogCountKeyUnread, 0, memberId));

        public long GetRecordTypeUnreadCount(RecordTypeId recordTypeId, long memberId, ICounterAndListService countService = null)
            => recordTypeId == null
                   ? 0
                   : (countService ?? _counterService).GetCounter(GetCounterKey(_dialogCountKeyUnread, memberId, recordTypeId));

        private string GetCounterKey(string counter, long dialogId, long memberId)
            => string.Concat("urn:messages.dialog", counter, ".", dialogId, "_", memberId.ToStringInvariant());

        private string GetCounterKey(string counter, long memberId, RecordTypeId recordTypeId)
            => string.Concat("urn:messages.dialog", counter, ".", memberId, "_", recordTypeId.ToString());
    }
}
