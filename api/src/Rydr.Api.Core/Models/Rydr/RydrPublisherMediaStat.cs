using System;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE MediaStats;
CREATE TABLE MediaStats
(
PublisherAccountId BIGINT NOT NULL,
MediaId BIGINT NOT NULL,
Id VARCHAR(75) NOT NULL,
PeriodEnumId BIGINT NOT NULL,
EndTime DATETIME NOT NULL,
StatEnumId BIGINT NOT NULL,
Value DECIMAL(18,4) NOT NULL,
PRIMARY KEY (PublisherAccountId, PeriodEnumId, EndTime, MediaId, StatEnumId)
);
CREATE UNIQUE INDEX IDX_MediaStats__Id ON MediaStats (Id);
")]
    [Alias("MediaStats")]
    public class RydrPublisherMediaStat : IHasStringId, IHasPublisherAccountId
    {
        [Required]
        [PrimaryKey]
        public string Id
        {
            get => string.Concat(StatEnumId, "_", PeriodEnumId, "_", MediaId, "_", EndTime.ToUnixTimestamp());

            // ReSharper disable once ValueParameterNotUsed
            set
            {
                // Ignore
            }
        }

        [Required]
        [CheckConstraint("MediaId > 0")]
        public long MediaId { get; set; }

        [Required]
        [CheckConstraint("PublisherAccountId > 0")]
        public long PublisherAccountId { get; set; }

        [Required]
        public long PeriodEnumId { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        public long StatEnumId { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double Value { get; set; }
    }
}
