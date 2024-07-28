using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IDialogMessageService
{
    Task<DialogMessageIds> SendMessageAsync(SendMessage request);
    Task MarkMessageReadAsync(long dialogId, long messageId);
    Task<DynDialogMessage> GetMessageAsync(long messageId);
    Task<DynDialogMessage> GetLastMessageAsync(long dialogId);

    IAsyncEnumerable<DialogMessage> GetDialogMessagesAsync(long dialogId, long sentBefore = 0, long sentAfter = 0,
                                                           long sentBeforeId = 0, long sentAfterId = 0);
}

public interface IDialogService
{
    Task MarkDialogReadAsync(long dialogId);
    Task<Dialog> GetDialogAsync(long dialogId);

    IAsyncEnumerable<Dialog> GetDialogsAsync(RecordTypeId forRecordTypeId = null, DialogType type = DialogType.Unknown, long forWorkspaceId = 0,
                                             int skip = 0, int take = 50);

    IAsyncEnumerable<DynDialog> GetDialogsAsync(long forWorkspaceId = 0, int skip = 0, int take = 50);
    Task<DynDialog> TryGetDynDialogAsync(IEnumerable<long> members);
    Task<Dialog> GetOrCreateDialogAsync(HashSet<RecordTypeId> members, long dialogId = 0, RecordTypeId forRecordTypeId = null);
    Task<HashSet<RecordTypeId>> GetDialogMembersAsync(long dialogId);
}

public interface IDialogCountService
{
    Task MarkMessageReadAsync(long dialogId, long messageId, long memberId);
    Task MarkDialogReadAsync(long dialogId, long memberId);
    long AddUnreadMessageForMember(long dialogId, long messageId, long memberId, ICounterAndListService countService = null);
    bool IsMessageRead(long dialogId, long messageId, long memberId, ICounterAndListService countService = null);
    long GetDialogUnreadCount(long dialogId, long memberId, ICounterAndListService countService = null);
    long GetMemberTotalUnreadCount(long memberId, ICounterAndListService countService = null);
    long GetRecordTypeUnreadCount(RecordTypeId recordTypeId, long memberId, ICounterAndListService countService = null);
    long IncrementUnreadCountForRecord(RecordTypeId forRecord, long memberId, ICounterAndListService countService = null);
    long DecrementUnreadCountForRecord(RecordTypeId forRecord, long memberId, ICounterAndListService countService = null);
}
