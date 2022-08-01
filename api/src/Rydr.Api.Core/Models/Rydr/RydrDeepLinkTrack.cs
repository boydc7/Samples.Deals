using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE DeepLinkTracks;
CREATE TABLE DeepLinkTracks
(
Timestamp BIGINT NOT NULL,
DealId BIGINT NOT NULL,
Uniqueifier VARCHAR(100) NOT NULL,
Path VARCHAR(100) NULL,
Campaign VARCHAR(100) NULL,
Medium VARCHAR(100) NULL,
Source VARCHAR(100) NULL,
Content VARCHAR(100) NULL,
Term VARCHAR(100) NULL,
WorkspaceId BIGINT NOT NULL,
UserId BIGINT NOT NULL,
PublisherAccountId BIGINT NOT NULL,
PRIMARY KEY (Timestamp, Uniqueifier)
);
")]
    [Alias("DeepLinkTracks")]
    public class RydrDeepLinkTrack
    {
        [Required]
        public long Timestamp { get; set; }

        [Required]
        public long DealId { get; set; }

        [Required]
        [StringLength(100)]
        public string Path { get; set; }

        [StringLength(100)]
        public string Campaign { get; set; }

        [StringLength(100)]
        public string Medium { get; set; }

        [StringLength(100)]
        public string Source { get; set; }

        [StringLength(100)]
        public string Content { get; set; }

        [StringLength(100)]
        public string Term { get; set; }

        [StringLength(100)]
        public string Uniqueifier { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        public long PublisherAccountId { get; set; }
    }
}
