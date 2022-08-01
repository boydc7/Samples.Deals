using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [Alias("DialogActivity")]
    [CompositeIndex(nameof(WorkspaceId), nameof(LastMessageSentOn), nameof(Id), nameof(DialogKey), Unique = true)]
    public class RydrDialogActivity : IHasSettableId
    {
        [Required]
        [PrimaryKey]
        public long Id { get; set; }

        [Required]
        [StringLength(100)]
        public string DialogKey { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [Required]
        public long ForRecordId { get; set; }

        [Required]
        public long LastMessageSentOn { get; set; }
    }
}
