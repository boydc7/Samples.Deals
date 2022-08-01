using System;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE WorkspaceUsers;
CREATE TABLE WorkspaceUsers
(
Id VARCHAR(50) NOT NULL,
WorkspaceId BIGINT NOT NULL,
UserId BIGINT NOT NULL,
WorkspaceUserId BIGINT NOT NULL,
DeletedOn DATETIME NULL,
WorkspaceRole INT NOT NULL DEFAULT 2,
PRIMARY KEY (WorkspaceId, WorkspaceUserId)
);
CREATE UNIQUE INDEX IDX_WorkspaceUsers__Id ON WorkspaceUsers (Id);
CREATE UNIQUE INDEX IDX_WorkspaceUsers__Uid_Wid ON WorkspaceUsers (UserId, WorkspaceId);
")]
    [Alias("WorkspaceUsers")]
    public class RydrWorkspaceUser : IHasStringId
    {
        [Required]
        [PrimaryKey]
        public string Id
        {
            get => string.Concat(WorkspaceUserId, "_", WorkspaceId);

            // ReSharper disable once ValueParameterNotUsed
            // ReSharper disable once UnusedMember.Global
            set
            {
                // Ignore
            }
        }

        [Required]
        public long WorkspaceUserId { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        public WorkspaceRole WorkspaceRole { get; set; }

        public DateTime? DeletedOn { get; set; }
    }
}
