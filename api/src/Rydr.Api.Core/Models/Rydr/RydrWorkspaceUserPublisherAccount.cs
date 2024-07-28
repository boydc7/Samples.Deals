using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE WorkspaceUserPublisherAccounts;
CREATE TABLE WorkspaceUserPublisherAccounts
(
Id VARCHAR(65) NOT NULL,
WorkspaceId BIGINT NOT NULL,
UserId BIGINT NOT NULL,
WorkspaceUserId BIGINT NOT NULL,
PublisherAccountId BIGINT NOT NULL,
DeletedOn DATETIME NULL,
PRIMARY KEY (UserId, WorkspaceId, PublisherAccountId)
);
CREATE UNIQUE INDEX IDX_WorkspaceUserPublisherAccounts__Id ON WorkspaceUserPublisherAccounts (Id);
CREATE UNIQUE INDEX IDX_WorkspaceUserPublisherAccounts__Wid_Pid_Uid ON WorkspaceUserPublisherAccounts (WorkspaceId, PublisherAccountId, UserId);
")]
[Alias("WorkspaceUserPublisherAccounts")]
public class RydrWorkspaceUserPublisherAccount : IHasStringId
{
    [Required]
    [PrimaryKey]
    public string Id
    {
        get => string.Concat(PublisherAccountId, "_", WorkspaceUserId, "_", WorkspaceId);

        // ReSharper disable once ValueParameterNotUsed
        // ReSharper disable once UnusedMember.Global
        set
        {
            // Ignore
        }
    }

    public long WorkspaceUserId { get; set; }

    public long PublisherAccountId { get; set; }

    public long WorkspaceId { get; set; }

    public long UserId { get; set; }

    public DateTime? DeletedOn { get; set; }
}
