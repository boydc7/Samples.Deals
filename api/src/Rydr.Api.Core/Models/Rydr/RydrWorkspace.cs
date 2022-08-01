using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [Alias("Workspaces")]
    public class RydrWorkspace : BaseLongDeletableDataModel
    {
        [StringLength(250)]
        public string Name { get; set; }

        [Required]
        public long DefaultPublisherAccountId { get; set; }

        [Required]
        public long OwnerId { get; set; }

        [Required]
        [CheckConstraint("WorkspaceType > 0")]
        public WorkspaceType WorkspaceType { get; set; }

        [Required]
        public WorkspaceFeature WorkspaceFeatures { get; set; }

        [Required]
        public PublisherType CreatedViaPublisherType { get; set; }

        [StringLength(100)]
        public string StripeCustomerId { get; set; }

        [StringLength(100)]
        public string ActiveCampaignCustomerId { get; set; }

        [StringLength(100)]
        public string CreatedViaPublisherId { get; set; }
    }
}
