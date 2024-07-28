using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr;

[Alias("PublisherApprovedMedia")]
[CompositeIndex(nameof(PublisherAccountId), nameof(FileId), nameof(Id), Unique = true)]
[CompositeIndex(nameof(WorkspaceId), nameof(PublisherAccountId), nameof(Id), Unique = true)]
public class RydrPublisherApprovedMedia : BaseLongDeletableDataModel, ICanBeAuthorized
{
    [Required]
    [CheckConstraint("WorkspaceId > 0")]
    public long WorkspaceId { get; set; }

    [Required]
    [CheckConstraint("PublisherAccountId > 0")]
    public long PublisherAccountId { get; set; }

    [Required]
    [CheckConstraint("FileId > 0")]
    public long FileId { get; set; }

    [StringLength(150)]
    public string Caption { get; set; }

    [Required]
    [CheckConstraint("ContentType > 0")]
    public PublisherContentType ContentType { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public long OwnerId => 0;
}
