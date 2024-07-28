using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Dto.Publishers;

[Route("/publisheracct/{id}", "GET")]
public class GetPublisherAccount : BaseGetRequest<PublisherAccount> { }

[Route("/publisheracct/x/{id}", "GET")]
public class GetPublisherAccountExternal : BaseGetRequest<PublisherAccountInfo> { }

[Route("/publisheracct", "GET")]
public class GetPublisherAccounts : BaseGetManyRequest<PublisherAccountLinkInfo>, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
}

[Route("/publisheracct/me", "GET")]
public class GetMyPublisherAccount : RequestBase, IReturn<OnlyResultResponse<PublisherAccount>>, IGet { }

[Route("/publisheracct/{PublisherIdentifier}/stats", "GET")]
public class GetPublisherAccountStats : RequestBase, IReturn<OnlyResultResponse<PublisherAccountStats>>, IGet, IHasPublisherAccountIdentifier
{
    public string PublisherIdentifier { get; set; }
}

[Route("/publisheracct/{PublisherIdentifier}/stats/{DealtWithPublisherAccountId}", "GET")]
public class GetPublisherAccountStatsWith : RequestBase, IReturn<OnlyResultResponse<PublisherAccountStatsWith>>, IGet, IHasPublisherAccountIdentifier
{
    public string PublisherIdentifier { get; set; }
    public long DealtWithPublisherAccountId { get; set; }
}

[Route("/publisheracct", "POST")]
public class PostPublisherAccount : PostPublisherAccountBase
{
    protected override RecordType GetRecordType() => RecordType.PublisherAccount;
}

[Route("/publisheracct/upsert", "POST")]
public class PostPublisherAccountUpsert : PostPublisherAccountBase { }

public abstract class PostPublisherAccountBase : BasePostRequest<PublisherAccount>
{
    protected override RecordType GetRecordType() => RecordType.PublisherAccount;
}

[Route("/publisheracct/{id}", "PUT")]
public class PutPublisherAccount : BasePutRequest<PublisherAccount>
{
    protected override RecordType GetRecordType() => RecordType.PublisherAccount;
}

[Route("/internal/admin/publisheracct/{publisheraccountid}", "PUT")]
public class PutPublisherAccountAdmin : RequestBase, IPut, IHasPublisherAccountId, IReturn<OnlyResultResponse<PublisherAccount>>
{
    public long PublisherAccountId { get; set; }
    public int MaxDelinquent { get; set; }
}

[Route("/publisheracct/{id}/tags", "PUT")]
public class PutPublisherAccountTag : RequestBase, IPut, IReturnVoid
{
    public long Id { get; set; }
    public Tag Tag { get; set; }
}

[Route("/publisheracct/{publisheraccountid}/tokens", "PUT")]
public class PutPublisherAccountToken : RequestBase, IPut, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public long PublisherAppId { get; set; }
    public string AccessToken { get; set; }
    public bool Force { get; set; }
}

[Route("/publisheracct/{id}", "DELETE")]
public class DeletePublisherAccount : BaseDeleteRequest
{
    protected override RecordType GetRecordType() => RecordType.PublisherAccount;
}

[Route("/publisheracct/link", "PUT")]
public class PutPublisherAccountLinks : RequestBaseDeferAffected<OnlyResultsResponse<PublisherAccountLinkInfo>>, IPut
{
    public List<PublisherAccount> LinkAccounts { get; set; }

    protected override (IEnumerable<long> Ids, RecordType Type) GetMyAffected(OnlyResultsResponse<PublisherAccountLinkInfo> result)
        => (result?.Results.Select(r => r.PublisherAccount.Id), RecordType.PublisherAccount);
}

[Route("/publisheracct/link/{id}", "DELETE")]
public class DeletePublisherAccountLink : BaseDeleteRequest
{
    protected override RecordType GetRecordType() => RecordType.PublisherAccount;
}

// Business - public related reporting, business is a publisher account
[Route("/businesses/{publisheridentifier}/rxlink", "GET")]
public class GetBusinessReportExternalLink : RequestBase, IReturn<OnlyResultResponse<StringIdResponse>>, IGet, IHasPublisherAccountIdentifier
{
    public DateTime CompletedOnStart { get; set; }
    public DateTime CompletedOnEnd { get; set; }
    public long DealId { get; set; }
    public long Duration { get; set; }
    public string PublisherIdentifier { get; set; }
}

[Route("/businesses/xr/{businessreportid}", "GET")]
public class GetBusinessReportExternal : RequestBase, IGet, IReturn<OnlyResultResponse<BusinessReportData>>
{
    public string BusinessReportId { get; set; }
}

// DEFERRED actions in response to pubacct actions
[Route("/internal/publisheracct/updated", "POST")]
public class PublisherAccountUpdated : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public RydrAccountType FromRydrAccountType { get; set; }
    public RydrAccountType ToRydrAccountType { get; set; }
}

[Route("/internal/publisheracct/processtags", "POST")]
public class ProcessPublisherAccountTags : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
}

[Route("/internal/publisheracct/delete", "POST")]
public class DeletePublisherAccountInternal : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
}

[Route("/internal/publisheracct/deleted", "POST")]
public class PublisherAccountDeleted : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
}

[Route("/internal/publisheracct/link", "POST")]
public class LinkPublisherAccount : RequestBase, IReturnVoid, IPost
{
    public long FromPublisherAccountId { get; set; }
    public long ToPublisherAccountId { get; set; }
    public long ToWorkspaceId { get; set; }
    public bool IsValidateRequest { get; set; }
}

[Route("/internal/publisheracct/delink", "POST")]
public class DelinkPublisherAccount : RequestBase, IReturnVoid, IPost
{
    public long FromPublisherAccountId { get; set; }
    public long ToPublisherAccountId { get; set; }
    public long FromWorkspaceId { get; set; }
}

[Route("/internal/publisheracct/unlinked", "POST")]
public class PublisherAccountUnlinked : RequestBase, IReturnVoid, IPost
{
    public long FromPublisherAccountId { get; set; }
    public long ToPublisherAccountId { get; set; }
    public long FromWorkspaceId { get; set; }
}

[Route("/internal/publisheracct/linked", "POST")]
public class PublisherAccountLinked : PublisherAccountLinkedCompleteBase { }

[Route("/internal/publisheracct/linkedcomplete", "POST")]
public class PublisherAccountLinkedComplete : PublisherAccountLinkedCompleteBase { }

[Route("/internal/publisheracct/updaterecentdealstats", "POST")]
public class PublisherAccountRecentDealStatsUpdate : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public long InWorkspaceId { get; set; }

    public static string GetRecurringJobId(long publisherAccountId, long contextWorkspaceId)
        => string.Concat("PublisherAccountRecentDealStats|", contextWorkspaceId, "|", publisherAccountId);
}

[Route("/internal/publisheracct/downconvert", "POST")]
public class PublisherAccountDownConvert : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
}

[Route("/internal/publisheracct/upconvertfacebook", "POST")]
public class PublisherAccountUpConvertFacebook : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public FacebookAccount WithFacebookAccount { get; set; }
}

public class BusinessReportData
{
    public DealCompletionMediaMetrics CompletionMetrics { get; set; }
    public Dictionary<long, DealCompletionMediaMetrics> DealCompletionMetrics { get; set; }

    // ReSharper disable once CollectionNeverQueried.Global
    public List<BusinessReportDealRequest> CompletedDealRequests { get; set; }
}

public class BusinessReportDealRequest
{
    public string DealRequestReportLink { get; set; }
    public DateTime CompletedOn { get; set; }
    public long DealId { get; set; }
    public string DealTitle { get; set; }
    public PublisherAccountProfile PublisherAccount { get; set; }
}

public abstract class PublisherAccountLinkedCompleteBase : RequestBase, IReturnVoid, IPost
{
    public long FromPublisherAccountId { get; set; }
    public long ToPublisherAccountId { get; set; }
    public long FromWorkspaceId { get; set; }
    public bool IsValidateRequest { get; set; }
}

public class PublisherAccountLinkInfo
{
    public PublisherAccount PublisherAccount { get; set; }
    public long? UnreadNotifications { get; set; }
}

public class PublisherAccountStats
{
    public List<DealStat> DealRequestStats { get; set; }
}

public class PublisherAccountStatsWith
{
    public long CompletedDealCount { get; set; }
    public long CompletionMediaCount { get; set; }
    public List<PublisherStatValue> Stats { get; set; }
}

public class PublisherAccount : PublisherAccountInfo
{
    public string PageId { get; set; }
    public string Email { get; set; }
    public string AccessToken { get; set; }
    public int AccessTokenExpiresIn { get; set; }
}

public class PublisherAccountInfo : PublisherAccountProfile
{
    public string AccountId { get; set; }
    public PublisherType Type { get; set; }
    public PublisherAccountType AccountType { get; set; }
    public bool IsSyncDisabled { get; set; }
    public Dictionary<string, double> Metrics { get; set; }
    public DateTime LastSyncedOn { get; set; }
    public string FullName { get; set; }
    public string Description { get; set; }
    public string Website { get; set; }
    public int MaxDelinquent { get; set; } = 5;
    public HashSet<Tag> Tags { get; set; }

    // Response only
    public List<PublisherAccountProfile> RecentCompleters { get; set; }

    public bool HasUpsertData() => Type != PublisherType.Unknown ||
                                   AccountType != PublisherAccountType.Unknown ||
                                   RydrAccountType != RydrAccountType.None;
}

public class PublisherAccountProfile : BaseDateTimeDeleteTrackedDtoModel, IHasSettableId, IHasRydrAccountType
{
    public long Id { get; set; }
    public RydrAccountType RydrAccountType { get; set; }
    public string ProfilePicture { get; set; }
    public string UserName { get; set; }
    public bool? OptInToAi { get; set; }
    public override DateTime CreatedOn { get; set; }
    public PublisherLinkType LinkType { get; set; }
}
