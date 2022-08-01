using System;

namespace Rydr.Api.Dto.Interfaces
{
    public interface IDateTimeTracked : IDateTimeCreateTracked, IDateTimeModifyTracked { }

    public interface IDateTimeCreateTracked : IHasCreatedBy
    {
        DateTime CreatedOn { get; set; }
    }

    public interface IHasCreatedBy
    {
        long CreatedBy { get; set; }
        long CreatedWorkspaceId { get; set; }
    }

    public interface IHasModifiedBy
    {
        long ModifiedBy { get; set; }
        long ModifiedWorkspaceId { get; set; }
    }

    public interface IDateTimeModifyTracked : IHasModifiedBy
    {
        DateTime ModifiedOn { get; set; }
    }

    public interface IDateTimeDeleteTracked
    {
        DateTime? DeletedOn { get; set; }
        long? DeletedBy { get; set; }
        long? DeletedByWorkspaceId { get; set; }
    }
}
