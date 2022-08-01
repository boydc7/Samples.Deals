using System;
using System.Runtime.Serialization;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto
{
    public abstract class BaseDateTimeTrackedDtoModel : IDateTimeTracked
    {
        [Ignore]
        [IgnoreDataMember]
        public virtual DateTime CreatedOn { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long CreatedBy { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long CreatedWorkspaceId { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public DateTime ModifiedOn { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long ModifiedBy { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long ModifiedWorkspaceId { get; set; }
    }

    public abstract class BaseDateTimeDeleteTrackedDtoModel : BaseDateTimeTrackedDtoModel, IDateTimeDeleteTracked
    {
        public DateTime? DeletedOn { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long? DeletedBy { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long? DeletedByWorkspaceId { get; set; }
    }
}
