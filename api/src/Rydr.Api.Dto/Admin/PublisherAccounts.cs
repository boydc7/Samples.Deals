using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Admin;

[Route("/internal/admin/publisherappaccounts", "POST")]
public class PostGetPublisherAppAccounts : RequestBase, IPost, IHasPublisherAccountId, IReturn<OnlyResultsResponse<PublisherAppAccount>>
{
    public long PublisherAccountId { get; set; }
}

[Route("/internal/admin/remappubacct", "POST")]
public class RemapSoftBasicPublisherAccount : RequestBase, IReturnVoid, IPost
{
    public long SoftLinkedPublisherAccountId { get; set; }
    public long BasicPublisherAccountId { get; set; }
}

public class PublisherAppAccount
{
    public long PublisherAccountId { get; set; }
    public string PublisherUserName { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public long PublisherAppId { get; set; }
    public PublisherType PublisherAppType { get; set; }

    public string AccessToken { get; set; }
    public string TokenForUserId { get; set; }
    public string PubTokenType { get; set; }
    public List<string> TokenScopes { get; set; }
    public DateTime TokenLastUpdated { get; set; }
    public bool IsSyncDisabled { get; set; }
    public int FailuresSinceLastSuccess { get; set; }
    public DateTime LastFailedOn { get; set; }
    public Dictionary<string, long> SyncStepsLastFailedOn { get; set; }
    public Dictionary<string, long> SyncStepsFailCount { get; set; }
}
