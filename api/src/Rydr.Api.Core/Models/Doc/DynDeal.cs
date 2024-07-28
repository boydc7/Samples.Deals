using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynDeal : DynItem, IHasNameAndIsRecordLookup, IHasPublisherAccountId
{
    // Hash / Id : PublisherAccountId
    // Range / Edge: DealId
    // RefId: unixtimestamp that a deal was set to published and/or paused, cannot be used here for something else
    // i.e. the refId is only set if the deal is in a published or paused status
    // OwnerId:
    // WorkspaceId: Workspace that created the deal
    // StatusId: DealStatus

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long PublisherAccountId
    {
        get => Id;
        set => Id = value;
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long DealId
    {
        get => EdgeId.ToLong(0);
        set => EdgeId = value.ToEdgeId();
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DealStatus DealStatus
    {
        get => Enum.TryParse<DealStatus>(StatusId, true, out var status)
                   ? status
                   : DealStatus.Unknown;

        set => StatusId = value.ToString();
    }

    public DealType DealType { get; set; }

    [ExcludeNullValue]
    public string Title { get; set; }

    [ExcludeNullValue]
    public CompressedString Description { get; set; }

    public double Value { get; set; }
    public long PlaceId { get; set; }

    [ExcludeNullValue]
    public List<DealRestriction> Restrictions { get; set; }

    [ExcludeNullValue]
    public DateTime ExpirationDate { get; set; }

    public int MaxApprovals { get; set; } // Max # of approvals for the deal, as set by the business/deal creator
    public int ReturnedApprovals { get; set; } // # of approvals that were returned to inventory (cancelled after being approved) for use by someone else
    public bool AutoApproveRequests { get; set; }

    [ExcludeNullValue]
    public CompressedString ApprovalNotes { get; set; }

    public long ReceivePlaceId { get; set; }

    [ExcludeNullValue]
    public HashSet<long> PublisherMediaIds { get; set; }

    [ExcludeNullValue]
    public HashSet<long> ReceivePublisherAccountIds { get; set; }

    [ExcludeNullValue]
    public HashSet<long> ReceiveHashtagIds { get; set; }

    [ExcludeNullValue]
    public CompressedString ReceiveNotes { get; set; }

    public List<MediaLineItem> ReceiveType { get; set; }

    [ExcludeNullValue]
    public HashSet<long> InvitedPublisherAccountIds { get; set; }

    public bool IsPrivateDeal { get; set; }

    [ExcludeNullValue]
    public HashSet<Tag> Tags { get; set; }

    [ExcludeNullValue]
    public HashSet<DealMetaData> MetaData { get; set; }

    [ExcludeNullValue]
    public string DealGroupId { get; set; }

    public long DealContextWorkspaceId { get; set; }

    // Status change dates
    [ExcludeNullValue]
    public DateTime? PublishedOn { get; set; }

    [ExcludeNullValue]
    public HashSet<long> PublisherApprovedMediaIds { get; set; }

    public int HoursAllowedInProgress { get; set; } // Default time allowed while in-progress
    public int HoursAllowedRedeemed { get; set; } // Default time allowed while redeemed

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public string Name => Title;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public int ApprovalLimit => ReturnedApprovals <= 0 || MaxApprovals == int.MaxValue
                                    ? MaxApprovals
                                    : int.MaxValue - MaxApprovals - ReturnedApprovals <= 0
                                        ? int.MaxValue
                                        : MaxApprovals + ReturnedApprovals;

    public override bool IsPubliclyReadable() => true;
    public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

    public override string UnsetNameToPropertyName(string unsetName)
    {
        switch (unsetName.ToUpperInvariant())
        {
            case "PLACE":

                return "PlaceId";

            case "RECEIVEPLACE":

                return "ReceivePlaceId";

            case "RECEIVEPUBLISHERACCOUNTS":

                return "ReceivePublisherAccountIds";

            case "RECEIVEHASHTAGS":

                return "ReceiveHashtagIds";

            default:

                return unsetName;
        }
    }
}
