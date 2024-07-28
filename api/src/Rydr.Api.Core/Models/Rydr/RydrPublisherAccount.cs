using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr;

[Alias("PublisherAccounts")]
public class RydrPublisherAccount : BaseLongDeletableDataModel
{
    [Required]
    [StringLength(150)]
    public string AccountId { get; set; }

    [Required]
    [CheckConstraint("PublisherType > 0")]
    public PublisherType PublisherType { get; set; }

    [Required]
    [CheckConstraint("AccountType > 0")]
    public PublisherAccountType AccountType { get; set; }

    [Required]
    [CheckConstraint("RydrAccountType > 0")]
    public RydrAccountType RydrAccountType { get; set; }

    [StringLength(150)]
    public string UserName { get; set; }

    [StringLength(150)]
    public string Email { get; set; }

    [StringLength(150)]
    public string AlternateAccountId { get; set; } // AccountId from another platform - i.e. the Instagram accountId for a Facebook biz ig page

    public DateTime? LastEngagementMetricsUpdatedOn { get; set; }

    public DateTime? LastMediaSyncedOn { get; set; }

    [Required]
    public long PrimaryPlaceId { get; set; }

    [Required]
    public bool IsSyncDisabled { get; set; }

    [Required]
    public bool OptInToAi { get; set; }

    [Required]
    public int FailuresSinceLastSuccess { get; set; }

    public double? LastKnownLatitude { get; set; }
    public double? LastKnownLongitude { get; set; }

    [Required]
    public int AgeRangeMin { get; set; }

    [Required]
    public int AgeRangeMax { get; set; }
}
