using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynDealRequestStatusChange : DynItem, IHasPublisherAccountId, IHasLatitudeLongitude
{
    // Hash/Id: DealId
    // Range/Edge: PublisherAccountId + ToStatus
    // ReferenceId = 3-character padded ToDealRequestStatus - OccurredOn (i.e. 002-unixtimestamp)
    // OwnerId: PublisherAccountId who created/owns the Deal
    // WorkspaceId: WorkspaceId that created/owns the Deal
    // StatusId: ToStatus

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public static string[] CompletedStatusChangeReferenceBetweenMinMax { get; } =
        {
            string.Concat(BuildReferecneIdPrefix(DealRequestStatus.Completed), "1500000000"),
            string.Concat(BuildReferecneIdPrefix(DealRequestStatus.Completed), "3000000000"),
        };

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public static string[] RedeemedStatusChangeReferenceBetweenMinMax { get; } =
        {
            string.Concat(BuildReferecneIdPrefix(DealRequestStatus.Redeemed), "1500000000"),
            string.Concat(BuildReferecneIdPrefix(DealRequestStatus.Redeemed), "3000000000"),
        };

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public static string[] CompletedStatusChangeTypeReferenceBetweenMinMax { get; } =
        {
            string.Concat((int)DynItemType.DealRequestStatusChange, "|", CompletedStatusChangeReferenceBetweenMinMax[0]),
            string.Concat((int)DynItemType.DealRequestStatusChange, "|", CompletedStatusChangeReferenceBetweenMinMax[1]),
        };

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public static string[] RedeemedStatusChangeTypeReferenceBetweenMinMax { get; } =
        {
            string.Concat((int)DynItemType.DealRequestStatusChange, "|", RedeemedStatusChangeReferenceBetweenMinMax[0]),
            string.Concat((int)DynItemType.DealRequestStatusChange, "|", RedeemedStatusChangeReferenceBetweenMinMax[1]),
        };

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long DealId
    {
        get => Id;
        set => Id = value;
    }

    public long PublisherAccountId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public DealRequestStatus ToDealRequestStatus
    {
        get => Enum.TryParse<DealRequestStatus>(StatusId, true, out var status)
                   ? status
                   : DealRequestStatus.Unknown;

        set => StatusId = value.ToString();
    }

    public long ModifiedByPublisherAccountId { get; set; }
    public long OccurredOn { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [ExcludeNullValue]
    public string Reason { get; set; }

    public DealRequestStatus FromDealRequestStatus { get; set; }

    public static string BuildEdgeId(DealRequestStatus status, long publisherAccountId)
        => BuildEdgeId(status.ToString(), publisherAccountId);

    public static string BuildEdgeId(string statusString, long publisherAccountId)
        => string.Concat(publisherAccountId, "|DealRequestStatus:", statusString);

    public static string BuildReferecneIdPrefix(DealRequestStatus requestStatus)
        => string.Concat(((int)requestStatus).ToStringInvariant().PadLeft(3, '0'), "|");
}
