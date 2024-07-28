using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE PublisherTags;
CREATE TABLE PublisherTags
(
PublisherAccountId BIGINT NOT NULL,
Tag VARCHAR(100) NOT NULL,
PRIMARY KEY (PublisherAccountId, Tag)
);
CREATE UNIQUE INDEX IDX_PublisherTags__Tag_PubAcctId ON PublisherTags (Tag, PublisherAccountId);
")]
[Alias("PublisherTags")]
public class RydrPublisherTag
{
    [Required]
    [StringLength(100)]
    public string Tag { get; set; }

    [Required]
    public long PublisherAccountId { get; set; }
}
