using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Supporting
{
    public class DynItemEdgeIdGlobalIndex : IGlobalIndex<DynItem>, IDynItemGlobalSecondaryIndex, ICanBeRecordLookup, IHaveIdAndEdgeId
    {
        [HashKey]
        public string EdgeId { get; set; }

        // Type|Reference combination
        [RangeKey]
        [ExcludeNullValue]
        public string TypeReference { get; set; }

        public long Id { get; set; }

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

        public long? DeletedOnUtc { get; set; }

        [Required]
        public int TypeId { get; set; }

        [ExcludeNullValue]
        public string StatusId { get; set; }

        public DynamoId GetDynamoId() => new DynamoId(Id, EdgeId);

        public AccessIntent DefaultAccessIntent() => AccessIntent.Unspecified;
        public bool IsPubliclyReadable() => false;
    }
}
