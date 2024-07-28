using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE WorkspacePublisherSubscriptions;
CREATE TABLE WorkspacePublisherSubscriptions
(
Id VARCHAR(65) NOT NULL,
WorkspaceId BIGINT NOT NULL,
PublisherAccountId BIGINT NOT NULL,
WorkspaceSubscriptionId BIGINT NOT NULL,
SubscriptionId VARCHAR(100) NULL,
CustomerId VARCHAR(100) NULL,
SubscriptionType INT NOT NULL,
CreatedOn DATETIME NOT NULL,
ModifiedOn DATETIME NOT NULL,
DeletedOn DATETIME NULL,
CustomMonthlyFee DECIMAL(18,4) NULL,
CustomPerPostFee DECIMAL(18,4) NULL,
PRIMARY KEY (WorkspaceId, PublisherAccountId)
);
CREATE UNIQUE INDEX IDX_WorkspacePublisherSubscriptions__Id ON WorkspacePublisherSubscriptions (Id);
")]
[Alias("WorkspacePublisherSubscriptions")]
public class RydrWorkspacePublisherSubscription : IHasStringId, IHasPublisherAccountId
{
    [Required]
    [PrimaryKey]
    public string Id
    {
        get => string.Concat(PublisherAccountId, "_", WorkspaceId);

        // ReSharper disable once ValueParameterNotUsed
        set
        {
            // Ignore
        }
    }

    [Required]
    public long WorkspaceId { get; set; }

    [Required]
    public long PublisherAccountId { get; set; }

    [Required]
    public long WorkspaceSubscriptionId { get; set; }

    [Required]
    public SubscriptionType SubscriptionType { get; set; }

    [StringLength(100)]
    public string SubscriptionId { get; set; }

    [StringLength(100)]
    public string CustomerId { get; set; }

    [Required]
    [QueryDbField(Operand = ">=")]
    public DateTime CreatedOn { get; set; }

    [Required]
    [QueryDbField(Operand = ">=")]
    public DateTime ModifiedOn { get; set; }

    [QueryDbField(Operand = ">=")]
    public DateTime? DeletedOn { get; set; }

    [DecimalLength(18, 4)]
    public double? CustomMonthlyFee { get; set; }

    [DecimalLength(18, 4)]
    public double? CustomPerPostFee { get; set; }
}
