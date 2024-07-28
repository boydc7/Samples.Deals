using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE SubscriptionUsages;
CREATE TABLE SubscriptionUsages
(
Id VARCHAR(100) NOT NULL,
WorkspaceId BIGINT NOT NULL,
WorkspaceSubscriptionId BIGINT NOT NULL,
ManagedPublisherAccountId BIGINT NOT NULL,
SubscriptionId VARCHAR(50) NOT NULL,
CustomerId VARCHAR(50) NOT NULL,
UsageType INT NOT NULL,
UsageOccurredOn DATETIME NOT NULL,
WorkspaceSubscriptionType INT NOT NULL,
PublisherSubscriptionType INT NOT NULL,
DealId BIGINT NOT NULL,
DealRequestPublisherAccountId BIGINT NOT NULL,
MonthOfService DATE NOT NULL,
Amount INT NOT NULL,
PRIMARY KEY (ManagedPublisherAccountId, WorkspaceId, DealId, DealRequestPublisherAccountId, UsageType, MonthOfService)
);
CREATE UNIQUE INDEX IDX_SubscriptionUsages__Id ON SubscriptionUsages (Id);
CREATE UNIQUE INDEX IDX_SubscriptionUsages__Wid_Mpid_Id ON SubscriptionUsages (WorkspaceId, ManagedPublisherAccountId, Id);
")]
[Alias("SubscriptionUsages")]
public class RydrSubscriptionUsage : IHasStringId
{
    [Required]
    [StringLength(100)]
    [PrimaryKey]
    public string Id
    {
        get => string.Concat(MonthOfService.ToSqlDateString(), "_", DealId, "_", DealRequestPublisherAccountId, "_", (int)UsageType);

        // ReSharper disable once ValueParameterNotUsed
        set
        {
            // Ignore
        }
    }

    [Required]
    public long WorkspaceId { get; set; }

    [Required]
    public long WorkspaceSubscriptionId { get; set; }

    [Required]
    public long ManagedPublisherAccountId { get; set; }

    [Required]
    [StringLength(50)]
    public string SubscriptionId { get; set; }

    [Required]
    [StringLength(50)]
    public string CustomerId { get; set; }

    [Required]
    public SubscriptionUsageType UsageType { get; set; }

    [Required]
    public DateTime UsageOccurredOn { get; set; }

    [Required]
    public SubscriptionType WorkspaceSubscriptionType { get; set; }

    [Required]
    public SubscriptionType PublisherSubscriptionType { get; set; }

    [Required]
    public long DealId { get; set; }

    [Required]
    public long DealRequestPublisherAccountId { get; set; }

    [Required]
    public DateTime MonthOfService { get; set; }

    [Required]
    public int Amount { get; set; }
}
