using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc;

public class DynDialog : DynItem, IHasNameAndIsRecordLookup
{
    // Hash / Id: DialogId
    // Range / EdgeId: Hash of members in the dialog
    // RefId: DialogId
    // OwnerId:
    // WorkspaceId: WorkspaceId of the associated non-user member (i.e. a deal, or anything that is not a user/publisheracount) if present,
    //                 OR the workspace that created the dialog otherwise...
    // StatusId:

    [Ignore]
    [DynamoDBIgnore]
    [IgnoreDataMember]
    public long DialogId
    {
        get => Id;
        set => Id = value;
    }

    [Ignore]
    [DynamoDBIgnore]
    [IgnoreDataMember]
    public string DialogKey
    {
        get => EdgeId;
        set => EdgeId = value;
    }

    [ExcludeNullValue]
    public string Name { get; set; }

    [ExcludeNullValue]
    public HashSet<RecordTypeId> Members { get; set; }

    [ExcludeNullValue]
    public HashSet<long> PublisherAccountIds { get; set; }

    [ExcludeNullValue]
    public string ImageUrl { get; set; }

    public DialogType DialogType { get; set; }

    public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

    //public override bool IsPubliclyReadable() => true;
}
