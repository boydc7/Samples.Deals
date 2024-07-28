using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr;

[Alias("Files")]
[CompositeIndex(nameof(WorkspaceId), nameof(FileType), nameof(Id), Unique = true)]
public class RydrFile : BaseLongDeletableDataModel, ICanBeAuthorized
{
    [Required]
    [CheckConstraint("WorkspaceId > 0")]
    public long WorkspaceId { get; set; }

    [Required]
    [CheckConstraint("FileType > 0")]
    public FileType FileType { get; set; }

    [Required]
    public long ContentLength { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public long OwnerId => 0;
}
