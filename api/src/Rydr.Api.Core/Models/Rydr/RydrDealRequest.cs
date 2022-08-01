using System;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE DealRequests;
CREATE TABLE DealRequests
(
Id VARCHAR(40) NOT NULL,
DealId BIGINT NOT NULL,
PublisherAccountId BIGINT NOT NULL,
Status SMALLINT NOT NULL DEFAULT 0,
StatusUpdatedOn DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
CompletedOn DATETIME NULL,
DealContextWorkspaceId BIGINT NOT NULL DEFAULT 0,
HoursAllowedInProgress INT NOT NULL DEFAULT 720,
HoursAllowedRedeemed INT NOT NULL DEFAULT 720,
RescindOn DATETIME NULL,
DelinquentOn DATETIME NULL,
UsageChargedOn DATETIME NULL,
RequestedOn DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
PRIMARY KEY (DealId, PublisherAccountId)
);
CREATE UNIQUE INDEX UIDX_DealRequests__PubAcctId_DealId ON DealRequests (PublisherAccountId, DealId, Status);
CREATE UNIQUE INDEX UIDX_DealRequests__Id ON DealRequests (Id);
CREATE UNIQUE INDEX UIDX_DealRequests__StatusUpdated_DealId_PubAcctId ON DealRequests (StatusUpdatedOn, DealId, PublisherAccountId);
")]
    [Alias("DealRequests")]
    public class RydrDealRequest : IHasStringId
    {
        [Required]
        [PrimaryKey]
        public string Id
        {
            get => string.Concat(DealId, "|", PublisherAccountId);

            // ReSharper disable once ValueParameterNotUsed
            set
            {
                // Ignore
            }
        }

        [Required]
        [CheckConstraint("DealId > 0")]
        public long DealId { get; set; }

        [Required]
        [CheckConstraint("PublisherAccountId > 0")]
        public long PublisherAccountId { get; set; }

        [Required]
        public DealRequestStatus Status { get; set; }

        [Required]
        public DateTime StatusUpdatedOn { get; set; }

        public DateTime? CompletedOn { get; set; }

        [Required]
        public long DealContextWorkspaceId { get; set; }

        [Required]
        public int HoursAllowedInProgress { get; set; } // Time allowed while in-progress

        [Required]
        public int HoursAllowedRedeemed { get; set; } // Time allowed while redeemed

        public DateTime? RescindOn { get; set; }
        public DateTime? DelinquentOn { get; set; }
        public DateTime? UsageChargedOn { get; set; }

        [Required]
        public DateTime RequestedOn { get; set; }
    }
}
