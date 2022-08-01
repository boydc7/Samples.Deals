/*
using System;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE Media;
CREATE TABLE Media
(
PublisherAccountId BIGINT NOT NULL,
Id BIGINT NOT NULL,
PublisherMediaId VARCHAR(50) NOT NULL,
PublisherType SMALLINT NOT NULL DEFAULT 0,
MediaType VARCHAR(50) NOT NULL,
MediaCreatedAt DATETIME NOT NULL,
ActionCount BIGINT NOT NULL,
CommentCount BIGINT NOT NULL,
IsCompletionMedia SMALLINT NOT NULL DEFAULT 0,
IsAnalyzed SMALLINT NOT NULL DEFAULT 0,
PreBizConversionErrorCount BIGINT NOT NULL DEFAULT 0,
ContentType SMALLINT NOT NULL DEFAULT 0,
PRIMARY KEY (PublisherAccountId, Id)
);
CREATE UNIQUE INDEX IDX_Media__MediaType_PubAcctId__Id ON Media (MediaType, PublisherAccountId, Id);
CREATE UNIQUE INDEX IDX_Media__Id ON Media (Id);
")]
    [Alias("Media")]
    public class RydrPublisherMedia : IHasLongId
    {
        [Required]
        [PrimaryKey]
        [CheckConstraint("Id > 0")]
        public long Id { get; set; }

        [Required]
        [CheckConstraint("PublisherAccountId > 0")]
        public long PublisherAccountId { get; set; }

        [Required]
        public string PublisherMediaId { get; set; }

        [Required]
        public PublisherType PublisherType { get; set; }

        [Required]
        public string MediaType { get; set; }

        [Required]
        public DateTime MediaCreatedAt { get; set; }

        [Required]
        [CheckConstraint("ActionCount >= 0")]
        public long ActionCount { get; set; }

        [Required]
        [CheckConstraint("CommentCount >= 0")]
        public long CommentCount { get; set; }

        [Required]
        public int IsCompletionMedia { get; set; }

        [Required]
        public int IsAnalyzed { get; set; }

        [Required]
        public int PreBizConversionErrorCount { get; set; }

        [Required]
        public PublisherContentType ContentType { get; set; }
    }

}
*/


