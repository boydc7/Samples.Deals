using System;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE DailyStats;
CREATE TABLE DailyStats
(
StatEnumId BIGINT NOT NULL,
RecordId BIGINT NOT NULL,
RecordType SMALLINT NOT NULL DEFAULT 0,
DayUtc DATE NOT NULL,
Id VARCHAR(65) NOT NULL,
PublisherAccountId BIGINT NOT NULL,
Val DECIMAL(18,4) NOT NULL,
MinVal DECIMAL(18,4) NOT NULL,
MaxVal DECIMAL(18,4) NOT NULL,
Measurements INT NOT NULL,
PRIMARY KEY (RecordType, RecordId, DayUtc, StatEnumId)
);
CREATE UNIQUE INDEX IDX_DailyStats__Day_RecId_StatEnId_RecType ON DailyStats (DayUtc, RecordId, StatEnumId, RecordType);
CREATE UNIQUE INDEX IDX_DailyStats__RecId_Day_StatEnId_RecType ON DailyStats (RecordId, DayUtc, StatEnumId, RecordType);
CREATE UNIQUE INDEX IDX_DailyStats__Id ON DailyStats (Id);
")]
    [Alias("DailyStats")]
    public class RydrDailyStat : RydrDailyStatBase { }

    public abstract class RydrDailyStatBase : IHasStringId
    {
        [Required]
        [PrimaryKey]
        public string Id
        {
            get => string.Concat(StatEnumId, "_", (int)RecordType, "_", RecordId, "_", DayUtc.ToUnixTimestamp());

            // ReSharper disable once ValueParameterNotUsed
            set
            {
                // Ignore
            }
        }

        [Required]
        public long StatEnumId { get; set; }

        [Required]
        [CheckConstraint("RecordId > 0")]
        public long RecordId { get; set; }

        [Required]
        [CheckConstraint("RecordType > 0")]
        public RecordType RecordType { get; set; }

        [Required]
        public DateTime DayUtc { get; set; }

        [Required]
        [CheckConstraint("PublisherAccountId > 0")]
        public long PublisherAccountId { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double Val { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double MinVal { get; set; }

        [Required]
        [DecimalLength(18, 4)]
        public double MaxVal { get; set; }

        [Required]
        [CheckConstraint("Measurements > 0")]
        public int Measurements { get; set; }
    }
}
