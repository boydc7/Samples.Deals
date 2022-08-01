using System;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models
{
    public abstract class BaseDataModel : IDateTimeTracked
    {
        [Required]
        [QueryDbField(Operand = ">=")]
        [IgnorePopulateExisting]
        public DateTime CreatedOn { get; set; }

        [Required]
        [IgnorePopulateExisting]
        public long CreatedBy { get; set; }

        [Required]
        [IgnorePopulateExisting]
        public long CreatedWorkspaceId { get; set; }

        [Required]
        [QueryDbField(Operand = ">=")]
        public DateTime ModifiedOn { get; set; }

        [Required]
        public long ModifiedBy { get; set; }

        [Required]
        public long ModifiedWorkspaceId { get; set; }

        public virtual AccessIntent DefaultAccessIntent() => AccessIntent.Unspecified;
        public bool IsPubliclyReadable() => false;
    }

    public abstract class BaseDeleteableDataModel : BaseDataModel, IDateTimeDeleteTracked
    {
        [QueryDbField(Operand = ">=")]
        public DateTime? DeletedOn { get; set; }

        public long? DeletedBy { get; set; }
        public long? DeletedByWorkspaceId { get; set; }

        public bool IsDeleted() => DeletedOn.HasValue;
    }

    public abstract class BaseDeleteableOnlyDataModel : IDateTimeDeleteTracked
    {
        [QueryDbField(Operand = ">=")]
        public DateTime? DeletedOn { get; set; }

        public long? DeletedBy { get; set; }
        public long? DeletedByWorkspaceId { get; set; }

        public bool IsDeleted() => DeletedOn.HasValue;
    }

    public abstract class BaseLongDeletableDataModel : BaseDeleteableDataModel, IHasSettableId
    {
        [Required]
        [PrimaryKey]
        [IgnorePopulateExisting]
        public virtual long Id { get; set; }
    }
}
