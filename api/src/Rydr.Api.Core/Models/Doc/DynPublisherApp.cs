using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynPublisherApp : DynItem
{
    // Hash/Id = PublisherAppId
    // Range/Edge = PublisherType / AppId combo
    // Ref = PublisherAppId
    // OwnerId: DedicatedWorkspaceId (if dedicated), PublicOwnerId otherwise
    // WorkspaceId:
    // StatusId:

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long PublisherAppId
    {
        get => Id;
        set => Id = value;
    }

    [Required]
    public string AppId { get; set; }

    [Required]
    public PublisherType PublisherType { get; set; }

    [Required]
    public string AppSecret { get; set; }

    [ExcludeNullValue]
    public string ApiVersion { get; set; }

    [ExcludeNullValue]
    public string Name { get; set; }

    public long DedicatedWorkspaceId { get; set; }

    public string GetEdgeId() => BuildEdgeId(PublisherType, AppId);
    public static string BuildEdgeId(PublisherType type, string appId) => string.Concat(type, "|", appId);

    public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;
}
