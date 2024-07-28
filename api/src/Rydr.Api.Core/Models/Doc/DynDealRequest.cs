using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynDealRequest : DynItem, IHasNameAndIsRecordLookup, IHasPublisherAccountId
{
    // Hash/Id = DealId
    // Range/Edge = Publisher account requesting deal
    // ReferenceId = time the request last changed statuses or was created
    // OwnerId: PublisherAccountId who created/owns the Deal
    // WorkspaceId: WorkspaceId that created/owns the Deal
    // StatusId: Current RequestStatus for the deal request

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long DealId
    {
        get => Id;
        set => Id = value;
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long PublisherAccountId
    {
        get => EdgeId.ToLong(0);
        set => EdgeId = value.ToEdgeId();
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long DealPublisherAccountId
    {
        get => OwnerId;
        set => OwnerId = value;
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DealRequestStatus RequestStatus
    {
        get => Enum.TryParse<DealRequestStatus>(StatusId, true, out var status)
                   ? status
                   : DealRequestStatus.Unknown;

        set => StatusId = value.ToString();
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long DealWorkspaceId
    {
        get => WorkspaceId;
        set => WorkspaceId = value;
    }

    public int HoursAllowedInProgress { get; set; } // Time allowed while in-progress
    public int HoursAllowedRedeemed { get; set; } // Time allowed while redeemed

    public long DealContextWorkspaceId { get; set; }
    public long UsageChargedOn { get; set; }

    [ExcludeNullValue]
    public string Title { get; set; }

    public List<MediaLineItem> ReceiveType { get; set; }

    // DynPublisherMedia.PublisherMediaId
    [ExcludeNullValue]
    public HashSet<long> CompletionMediaIds { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public string Name => Title;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DateTime? RescindOn => RequestStatus == DealRequestStatus.InProgress
                                      ? ReferenceId.ToLong(0).ToDateTime().AddHours(HoursAllowedInProgress)
                                      : null;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DateTime? DelinquentOn => RequestStatus == DealRequestStatus.Redeemed
                                         ? ReferenceId.ToLong(0).ToDateTime().AddHours(HoursAllowedRedeemed)
                                         : null;
}
