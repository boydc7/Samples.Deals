using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr;

[PostCreateTable(@"
DROP TABLE DailyStatSnapshots;
CREATE TABLE DailyStatSnapshots
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
CREATE UNIQUE INDEX IDX_DailyStatSnapshots__Day_RecId_StatEnId_RecType ON DailyStatSnapshots (DayUtc, RecordId, StatEnumId, RecordType);
CREATE UNIQUE INDEX IDX_DailyStatSnapshots__RecId_Day_StatEnId_RecType ON DailyStatSnapshots (RecordId, DayUtc, StatEnumId, RecordType);
CREATE UNIQUE INDEX IDX_DailyStatSnapshots__Id ON DailyStatSnapshots (Id);
")]
[Alias("DailyStatSnapshots")]
public class RydrDailyStatSnapshot : RydrDailyStatBase { }
