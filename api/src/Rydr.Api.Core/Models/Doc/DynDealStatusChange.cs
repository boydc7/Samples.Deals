using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynDealStatusChange : DynItem
{
    // Hash / Id: DealId
    // Range / Edge: DealStatus|<ToDealStatus>
    // ReferenceId = 3-character padded ToDealStatus - OccurredOn (i.e. 002-unixtimestamp)
    // OwnerId: PublisherAccountId who created/owns the Deal
    // WorkspaceId: WorkspaceId that created/owns the Deal
    // StatusId:

    private DealStatus _toDealStatus;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long DealId
    {
        get => Id;
        set => Id = value;
    }

    public DealStatus ToDealStatus
    {
        get => _toDealStatus;
        set
        {
            _toDealStatus = value;
            EdgeId = BuildEdgeId(value);
        }
    }

    public long ModifiedByPublisherAccountId { get; set; }
    public long OccurredOn { get; set; }

    [ExcludeNullValue]
    public string Reason { get; set; }

    public DealStatus FromDealStatus { get; set; }

    public static string BuildEdgeId(DealStatus status) => string.Concat("DealStatus|", status);

    public static string BuildReferecneIdPrefix(DealStatus dealStatus)
        => string.Concat(((int)dealStatus).ToStringInvariant().PadLeft(3, '0'), "|");
}
