using System;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [Alias("Deals")]
    [CompositeIndex(nameof(PublisherAccountId), nameof(Id), Unique = true)]
    [CompositeIndex(nameof(WorkspaceId), nameof(Id), nameof(PublisherAccountId), nameof(Status), Unique = true)]
    public class RydrDeal : BaseLongDeletableDataModel
    {
        [Required]
        [CheckConstraint("PublisherAccountId > 0")]
        public long PublisherAccountId { get; set; }

        [Required]
        public double Value { get; set; }

        [Required]
        public long PlaceId { get; set; }

        [Required]
        [CheckConstraint("Status > 0")]
        public DealStatus Status { get; set; }

        [Required]
        [CheckConstraint("DealType > 0")]
        public DealType DealType { get; set; }

        [Required]
        public DateTime ExpirationDate { get; set; }

        [Required]
        public int MaxApprovals { get; set; }

        [Required]
        public bool AutoApproveRequests { get; set; }

        [Required]
        public long ReceivePlaceId { get; set; }

        [Required]
        public bool IsPrivateDeal { get; set; }

        public DateTime? PublishedOn { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [Required]
        public long DealContextWorkspaceId { get; set; }

        [Required]
        public long OwnerId { get; set; }
    }
}
