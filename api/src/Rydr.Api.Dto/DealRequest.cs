using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto;

[Route("/deals/{dealid}/requests", "GET")]
[Route("/deals/{dealid}/requests/{publisheraccountid}", "GET")]
public class GetDealRequest : RequestBase, IGet, IReturn<OnlyResultResponse<DealRequest>>, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
}

[Route("/deals/{dealid}/requests/{publisheraccountid}/rxlink", "GET")]
public class GetDealRequestReportExternalLink : RequestBase, IGet, IReturn<OnlyResultResponse<StringIdResponse>>, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public long Duration { get; set; }
}

[Route("/deals/xr/{dealrequestreportid}", "GET")]
public class GetDealRequestReportExternal : RequestBase, IGet, IReturn<OnlyResultResponse<DealResponse>>
{
    public string DealRequestReportId { get; set; }
}

[Route("/deals/{dealid}/requests", "POST")]
public class PostDealRequest : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public int HoursAllowedInProgress { get; set; }
    public int HoursAllowedRedeemed { get; set; }

    [Obsolete("Only here currently to support backward compatibility with older versions, use HoursAllowedRedeemed instead. Obsoleted in June 2020.")]
    public int DaysUntilDelinquent { get; set; }
}

[Route("/deals/{dealid}/requests", "PUT")]
public class PutDealRequest : UpdateDealRequestBase, IPut, IReturnVoid
{
    public List<string> CompletionMediaIds { get; set; }
    public List<long> CompletionRydrMediaIds { get; set; }
    public bool ConsumeApprovalOnCancel { get; set; }
}

[Route("/deals/{dealid}/myrequest/media", "PUT")]
public class PutDealRequestCompletionMedia : RequestBase, IPut, IReturnVoid
{
    public long DealId { get; set; }
    public List<string> CompletionMediaIds { get; set; }
    public List<long> CompletionRydrMediaIds { get; set; }
}

[Route("/deals/{dealid}/requests", "DELETE")]
public class DeleteDealRequest : RequestBase, IDelete, IReturnVoid, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public string Reason { get; set; }
    public long PublisherAccountId { get; set; }
}

// DEFERRED actions in response to deal actions
[Route("/internal/dealrequests/requested", "POST")]
public class DealRequested : DealRequestedLow { }

[Route("/internal/dealrequests/requestedlow", "POST")]
public class DealRequestedLow : RequestBase, IReturnVoid, IPost
{
    public long DealId { get; set; }
    public long RequestedByPublisherAccountId { get; set; }
}

[Route("/internal/dealrequests/delete", "POST")]
public class DeleteDealRequestInternal : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public string Reason { get; set; }
}

[Route("/internal/dealrequests/statusupdated", "POST")]
public class DealRequestStatusUpdated : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId, IHasLatitudeLongitude
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public DealRequestStatus FromStatus { get; set; }
    public DealRequestStatus ToStatus { get; set; }
    public long OccurredOn { get; set; }
    public string Reason { get; set; }
    public long UpdatedByPublisherAccountId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

[Route("/internal/dealrequests/", "POST")]
public class UpdateDealRequest : UpdateDealRequestBase, IPost, IReturnVoid
{
    // Internal only
    public long UpdatedByPublisherAccountId { get; set; }
    public bool ForceAllowStatusChange { get; set; }
}

[Route("/internal/dealrequests/checkallowances", "POST")]
public class CheckDealRequestAllowances : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public bool DeferAsAffectedOnPass { get; set; }
}

public abstract class UpdateDealRequestBase : RequestBase, IHaveModel<DealRequest>, IHasLatitudeLongitude
{
    public long DealId { get; set; }
    public string Reason { get; set; }
    public DealRequest Model { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

[Route("/internal/dealrequests/updated", "POST")]
public class DealRequestUpdated : DealRequestStatusUpdated
{
    public int HoursAllowedInProgress { get; set; }
    public int HoursAllowedRedeemed { get; set; }
}

[Route("/internal/dealrequests/completionmedia", "POST")]
public class DealRequestCompletionMediaSubmitted : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public List<string> CompletionMediaPublisherMediaIds { get; set; }
    public List<long> CompletionRydrMediaIds { get; set; }
}

public class DealRequest : BaseDateTimeDeleteTrackedDtoModel, IDecorateWithPublisherAccountInfo, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public DealRequestStatus Status { get; set; }
    public long PublisherAccountId { get; set; }
    public int HoursAllowedInProgress { get; set; }
    public int HoursAllowedRedeemed { get; set; }

    [Obsolete("Only here currently to support backward compatibility with older versions, use HoursAllowedRedeemed instead. Obsoleted in June 2020.")]
    public int DaysUntilDelinquent { get; set; }

    // Response-only properties
    public bool IsDelinquent { get; set; }
    public string Title { get; set; }
    public PublisherAccountInfo PublisherAccount { get; set; }
    public DateTime? RequestedOn { get; set; }
    public List<PublisherMedia> CompletionMedia { get; set; }
    public List<DealRequestStatusChange> StatusChanges { get; set; }
    public DialogMessage LastMessage { get; set; }
    public List<MediaLineItem> ReceiveType { get; set; } // Type of content(s) the biz expects the influencer to post/etc

    [Ignore]
    [IgnoreDataMember]
    public long StatusLastChangedOn { get; set; }
}

public class DealRequestStatusChange : IHasLatitudeLongitude
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
    public DealRequestStatus FromStatus { get; set; }
    public DealRequestStatus ToStatus { get; set; }
    public long ModifiedByPublisherAccountId { get; set; }
    public long OccurredOn { get; set; }
    public string Reason { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
