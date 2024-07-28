using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE DealRequestMedia;
CREATE TABLE DealRequestMedia
(
Id VARCHAR(60) NOT NULL,
DealId BIGINT NOT NULL,
PublisherAccountId BIGINT NOT NULL,
MediaId BIGINT NOT NULL,
ContentType SMALLINT NOT NULL DEFAULT 0,
MediaType VARCHAR(50) NOT NULL,
PRIMARY KEY (DealId, PublisherAccountId, MediaId)
);
CREATE UNIQUE INDEX IDX_DealRequestMedia__PubAcctId_DealId_MediaId ON DealRequestMedia (PublisherAccountId, DealId, MediaId);
CREATE UNIQUE INDEX IDX_DealRequestMedia__Id ON DealRequestMedia (Id);
CREATE INDEX IDX_DealRequestMedia__MediaId ON DealRequestMedia (MediaId);
")]
[Alias("DealRequestMedia")]
public class RydrDealRequestMedia : IHasStringId, IHasPublisherAccountId
{
    [Required]
    [PrimaryKey]
    public string Id
    {
        get => string.Concat(DealId, "|", PublisherAccountId, "|", MediaId);

        // ReSharper disable once ValueParameterNotUsed
        set
        {
            // Ignore
        }
    }

    [Required]
    [CheckConstraint("DealId > 0")]
    public long DealId { get; set; }

    [Required]
    [CheckConstraint("PublisherAccountId > 0")]
    public long PublisherAccountId { get; set; }

    [Required]
    [CheckConstraint("MediaId > 0")]
    public long MediaId { get; set; }

    [Required]
    public string MediaType { get; set; }

    [Required]
    public PublisherContentType ContentType { get; set; }
}
