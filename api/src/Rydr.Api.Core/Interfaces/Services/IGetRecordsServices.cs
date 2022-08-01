using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IRecordTypeRecordService
    {
        Task ValidateAsync(RecordType recordType, long recordId, IHasUserAuthorizationInfo request = null);

        Task<T> GetRecordAsync<T>(RecordType recordType, long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false)
            where T : class, ICanBeRecordLookup;

        IAsyncEnumerable<T> GetRecordsAsync<T>(RecordType recordType, IEnumerable<long> recordIds,
                                               IHasUserAuthorizationInfo request = null, bool isInternal = false)
            where T : class, ICanBeRecordLookup;

        IAsyncEnumerable<T> GetRecords<T>(RecordType recordType, IEnumerable<DynamoItemIdEdge> compositeRecordIds)
            where T : class, ICanBeRecordLookup;

        Task SaveRydrRecordsAsync(RecordType recordType, ICollection<long> ids);
        Task SaveRydrRecordsAsync(RecordType recordType, ICollection<DynamoItemIdEdge> compositeRecordIds);
    }

    public interface IGetRecordsService<T> : IGetRecordsService
        where T : class, ICanBeRecordLookup
    {
        Task<T> GetRecordAsync(long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false);

        IAsyncEnumerable<T> GetRecordsAsync(IEnumerable<long> recordIds, IHasUserAuthorizationInfo request = null, bool isInternal = false);
        IAsyncEnumerable<T> GetRecordsAsync(IEnumerable<DynamoItemIdEdge> compositeRecordIds);
    }

    public interface IGetRecordsService
    {
        RecordType ForRecordType { get; }

        Task ValidateAsync(long recordId, IHasUserAuthorizationInfo request = null);

        Task<TAs> GetRecordAsAsync<TAs>(long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false)
            where TAs : class, ICanBeRecordLookup;

        IAsyncEnumerable<TAs> GetRecordsAsAsync<TAs>(IEnumerable<long> recordIds, IHasUserAuthorizationInfo request = null, bool isInternal = false)
            where TAs : class, ICanBeRecordLookup;

        IAsyncEnumerable<TAs> GetRecordsAsAsync<TAs>(IEnumerable<DynamoItemIdEdge> compositeRecordIds)
            where TAs : class, ICanBeRecordLookup;
    }

    public interface IGetRecordServiceFactory
    {
        IGetRecordsService Resolve(RecordType type);
    }

    public interface ISaveRydrRecordsService
    {
        Task SaveRecordsAsync(ICollection<long> ids);
        Task SaveRecordsAsync(ICollection<DynamoItemIdEdge> compositeRecordIds);
    }
}
