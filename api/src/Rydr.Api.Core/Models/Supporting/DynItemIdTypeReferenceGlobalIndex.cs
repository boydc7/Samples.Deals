using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Supporting;

public class DynItemIdTypeReferenceGlobalIndex : IGlobalIndex<DynItem>, ICanBeRecordLookup, IDynItemGlobalSecondaryIndex, IHaveIdAndEdgeId
{
    [HashKey]
    public long Id { get; set; }

    [RangeKey]
    [ExcludeNullValue]
    public string TypeReference { get; set; }

    [Required]
    [ExcludeNullValue]
    public string EdgeId { get; set; }

    [Required]
    public long WorkspaceId { get; set; }

    [Required]
    public long OwnerId { get; set; }

    [Required]
    public long CreatedBy { get; set; }

    [Required]
    public long CreatedWorkspaceId { get; set; }

    [Required]
    public long ModifiedBy { get; set; }

    [Required]
    public long ModifiedWorkspaceId { get; set; }

    [Required]
    public int TypeId { get; set; }

    [ExcludeNullValue]
    public string StatusId { get; set; }

    public long? DeletedOnUtc { get; set; }

    public DynamoId GetDynamoId() => new(Id, EdgeId);

    public AccessIntent DefaultAccessIntent() => AccessIntent.Unspecified;
    public bool IsPubliclyReadable() => false;
}
