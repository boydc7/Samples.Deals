using System;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE WorkspaceSubscriptions;
CREATE TABLE WorkspaceSubscriptions
(
Id BIGINT NOT NULL,
WorkspaceId BIGINT NOT NULL,
SubscriptionId VARCHAR(100) NOT NULL,
SubscriptionType INT NOT NULL,
Quantity BIGINT NOT NULL,
UnitPrice DECIMAL(18,4) NOT NULL,
SubscriptionInterval VARCHAR(100) NULL,
IntervalCount BIGINT NOT NULL,
ProductId VARCHAR(100) NULL,
PlanId VARCHAR(100) NULL,
SubscriptionStatus VARCHAR(100) NULL,
Email VARCHAR(100) NULL,
CustomerId VARCHAR(100) NULL,
BillingCycleAnchor DATETIME NULL,
StartedOn DATETIME NULL,
EndsOn DATETIME NULL,
TrialStartedOn DATETIME NULL,
TrialEndsOn DATETIME NULL,
CanceledOn DATETIME NULL,
CreatedOn DATETIME NOT NULL,
ModifiedOn DATETIME NOT NULL,
DeletedOn DATETIME NULL,
PRIMARY KEY (WorkspaceId, Id)
);
CREATE UNIQUE INDEX IDX_WorkspaceSubscriptions__Id ON WorkspaceSubscriptions (Id);
CREATE UNIQUE INDEX IDX_WorkspaceSubscriptions__CustomerId_WorkspaceId ON WorkspaceSubscriptions (CustomerId, WorkspaceId, Id);
")]
    [Alias("WorkspaceSubscriptions")]
    public class RydrWorkspaceSubscription : IHasLongId
    {
        [Required]
        public long Id { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [Required]
        [StringLength(100)]
        public string SubscriptionId { get; set; }

        [Required]
        public SubscriptionType SubscriptionType { get; set; }

        [Required]
        public long Quantity { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double UnitPrice { get; set; }

        [StringLength(100)]
        public string SubscriptionInterval { get; set; }

        [Required]
        public long IntervalCount { get; set; }

        [Required]
        [StringLength(100)]
        public string ProductId { get; set; }

        [Required]
        [StringLength(100)]
        public string PlanId { get; set; }

        [StringLength(50)]
        public string SubscriptionStatus { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(100)]
        public string CustomerId { get; set; }

        public DateTime? BillingCycleAnchor { get; set; }
        public DateTime? StartedOn { get; set; }
        public DateTime? EndsOn { get; set; }
        public DateTime? TrialStartedOn { get; set; }
        public DateTime? TrialEndsOn { get; set; }
        public DateTime? CanceledOn { get; set; }

        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
        public DateTime? DeletedOn { get; set; }
    }
}
