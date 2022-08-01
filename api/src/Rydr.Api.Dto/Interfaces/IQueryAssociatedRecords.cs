using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Dto.Interfaces
{
    public interface IQueryAssociatedRecords : IDecorateAsRecordType
    {
        long? RecordId { get; set; }
        RecordType? RecordType { get; set; }
    }
}
