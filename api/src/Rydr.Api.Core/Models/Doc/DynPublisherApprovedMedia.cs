using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynPublisherApprovedMedia : DynItem, IHasPublisherAccountId, ICanBeAuthorizedById, IGenerateFileMediaUrls
{
    // Hash/Id: PublisherAccountId
    // Range/Edge: Unique Id for this approved media (new sequence)
    // RefId: MediaFileId (DynFile.Id) reference
    // OwnerId:
    // WorkspaceId: Workspace that owns the approved media
    // StatusId:
    // Expires = never

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
    public long PublisherApprovedMediaId
    {
        get => EdgeId.ToLong(0);
        set => EdgeId = value.ToEdgeId();
    }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long MediaFileId
    {
        get => ReferenceId.ToLong(0);
        set => ReferenceId = value.ToStringInvariant();
    }

    [ExcludeNullValue]
    public string Caption { get; set; }

    public PublisherContentType ContentType { get; set; }

    [ExcludeNullValue]
    public string MediaUrl { get; set; }

    [ExcludeNullValue]
    public string ThumbnailUrl { get; set; }

    [ExcludeNullValue]
    public long UrlsGeneratedOn { get; set; }

    // Authorize by owner of the media (i.e. the publisher account id)
    public long AuthorizeId() => PublisherAccountId;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public long RydrMediaId => 0;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public bool IsPermanentMedia => true;
}
