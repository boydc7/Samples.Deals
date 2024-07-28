using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynWorkspace : DynItem, IHasPublisherAccountId
{
    // Hash / Id: Unique Id for the workspace
    // Range / Edge: Unique Id for the workspace (same as hash)
    // RefId: CreatedViaPublisherId
    // OwnerId: UserId who owns the workspace
    // WorkspaceId: Hash (unique id for the workspace)
    // StatusId: WorkspaceType enum value

    [ExcludeNullValue]
    public string Name { get; set; }

    [Required]
    public long DefaultPublisherAccountId { get; set; }

    public HashSet<long> SecondaryTokenPublisherAccountIds { get; set; }

    [Required]
    public long LastNonSystemTokenPublisherAccountId { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public WorkspaceType WorkspaceType
    {
        get => Enum.TryParse<WorkspaceType>(StatusId, true, out var status)
                   ? status
                   : WorkspaceType.Unspecified;

        set => StatusId = value.ToString();
    }

    public string FacebookBusinessId { get; set; }

    public PublisherType CreatedViaPublisherType { get; set; }

    [ExcludeNullValue]
    public string InviteCode { get; set; }

    public long WorkspaceFeatures { get; set; } // WorkspaceFeature enum flag

    public string CreatedViaPublisherId
    {
        get => ReferenceId;
        set => ReferenceId = value.Coalesce("NONE");
    }

    [ExcludeNullValue]
    public string StripeCustomerId { get; set; }

    [ExcludeNullValue]
    public string ActiveCampaignCustomerId { get; set; }

    public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long PublisherAccountId => DefaultPublisherAccountId;
}
