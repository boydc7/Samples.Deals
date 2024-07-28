using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynPublisherMediaComment : DynItem
{
    // Hash/Id = DynPublisherMedia.Id
    // Range/Edge = PublisherType / CommentId combination
    // RefId =
    // Expires = 35 days from creation

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long PublisherMediaId
    {
        get => Id;
        set => Id = value;
    }

    [ExcludeNullValue]
    public string Text { get; set; }

    [ExcludeNullValue]
    public string UserName { get; set; }

    [ExcludeNullValue]
    public string ThumbnailUrl { get; set; }

    public long CommentCreatedAt { get; set; }
    public long ActionCount { get; set; }

    public static string BuildEdgeId(PublisherType type, string commentId) => string.Concat(type, "|", commentId);
}
