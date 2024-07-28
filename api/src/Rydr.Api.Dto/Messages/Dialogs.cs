using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Dto.Messages;

[Route("/messages", "POST")]
public class PostMessage : RequestBase, IReturn<OnlyResultResponse<DialogMessageIds>>, IPost
{
    public RecordTypeId From { get; set; }
    public RecordTypeId To { get; set; }
    public string Message { get; set; }
}

[Route("/messages/{id}/read", "PUT")]
public class PutMessageRead : RequestBase, IReturnVoid, IPut
{
    public long DialogId { get; set; }
    public long Id { get; set; }
}

[Route("/dialogs/{id}/read", "PUT")]
public class PutDialogRead : RequestBase, IReturnVoid, IPut
{
    public long Id { get; set; }
}

// Send message to any dialog (private, group, etc.)
[Route("/dialogs/{dialogid}/messages", "POST")]
public class PostDialogMessage : RequestBase, IReturn<LongIdResponse>, IPost
{
    public long DialogId { get; set; }
    public string Message { get; set; }
}

// Get messages in a dialog
[Route("/dialogs/{dialogid}/messages", "GET")]
public class GetDialogMessages : RequestBase, IReturn<OnlyResultsResponse<DialogMessage>>, IHasSkipTake
{
    public long DialogId { get; set; }
    public long SentAfter { get; set; }
    public long SentBefore { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public long SentAfterId { get; set; }
    public long SentBeforeId { get; set; }
}

// Get dialogs
[Route("/dialogs", "GET")]
public class GetDialogs : RequestBase, IReturn<OnlyResultsResponse<Dialog>>, IHasSkipTake
{
    public RecordTypeId ForRecord { get; set; }
    public long SentAfter { get; set; }
    public long ForWorkspaceId { get; set; }
    public DialogType Type { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

[Route("/dialogs/{id}", "GET")]
[Route("/dialogs/{from}/{to}", "GET")]
public class GetDialog : BaseGetRequest<Dialog>
{
    public RecordTypeId From { get; set; }
    public RecordTypeId To { get; set; }
}

public class Dialog : BaseDateTimeDeleteTrackedDtoModel, IHasLongId
{
    public long Id { get; set; }
    public List<DialogMember> Members { get; set; }
    public string Name { get; set; }
    public string LastMessage { get; set; }
    public DateTime? LastMessageSentOn { get; set; }
    public long LastMessageSentBy { get; set; }
    public long LastMessageSentByPublisherAccountId { get; set; }
    public DialogType DialogType { get; set; }
    public long UnreadMessages { get; set; }
    public List<RecordTypeId> ForRecords { get; set; }
    public string DialogKey { get; set; }
}

public class DialogMember : IDecorateWithPublisherAccountInfo
{
    public RecordTypeId Record { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public long PublisherAccountId => Record.Type == RecordType.PublisherAccount
                                          ? Record.Id
                                          : 0;

    public PublisherAccountInfo PublisherAccount { get; set; }
}

public class DialogMessage : DialogMessageIds
{
    public bool IsRead { get; set; }
    public string Message { get; set; }
    public PublisherAccountInfo PublisherAccount { get; set; }
    public DateTime SentOn { get; set; }
}

public class DialogMessageIds : BaseDateTimeDeleteTrackedDtoModel, IHasLongId
{
    public long Id { get; set; }
    public long DialogId { get; set; }
    public string DialogKey { get; set; }
}
