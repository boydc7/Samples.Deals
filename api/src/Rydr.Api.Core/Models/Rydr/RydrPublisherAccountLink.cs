using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE PublisherAccountLinks;
CREATE TABLE PublisherAccountLinks
(
WorkspaceId BIGINT NOT NULL,
FromPublisherAccountId BIGINT NOT NULL,
ToPublisherAccountId BIGINT NOT NULL,
Id VARCHAR(50) NOT NULL,
DeletedOn DATETIME NULL,
PRIMARY KEY (FromPublisherAccountId, ToPublisherAccountId, WorkspaceId),
CONSTRAINT CHK_PublisherAccountLinks_WorkspaceId CHECK ( WorkspaceId > 0),
CONSTRAINT CHK_PublisherAccountLinks_ToPublisherAccountId CHECK ( ToPublisherAccountId > 0)
);
CREATE UNIQUE INDEX IDX_PublisherAccountLinks__ToPubId_FromPubId_WksId ON PublisherAccountLinks (ToPublisherAccountId, FromPublisherAccountId, WorkspaceId);
CREATE UNIQUE INDEX IDX_PublisherAccountLinks__Id ON PublisherAccountLinks (Id);
")]
[Alias("PublisherAccountLinks")]
public class RydrPublisherAccountLink : IHasStringId
{
    [Required]
    [PrimaryKey]
    public string Id
    {
        get => string.Concat(WorkspaceId, "_", FromPublisherAccountId, "_", ToPublisherAccountId);

        // ReSharper disable once ValueParameterNotUsed
        set
        {
            // Ignore
        }
    }

    public long WorkspaceId { get; set; }

    public long FromPublisherAccountId { get; set; }

    public long ToPublisherAccountId { get; set; }

    public DateTime? DeletedOn { get; set; }
}
