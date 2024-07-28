using Funq;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Services;

public class DefaultRecordTypeRecordService : IRecordTypeRecordService, IGetRecordServiceFactory
{
    private readonly Container _resolver;

    public DefaultRecordTypeRecordService(Container resolver)
    {
        _resolver = resolver;
    }

    public IGetRecordsService Resolve(RecordType recordType)
    {
        var getRecordsService = _resolver.TryResolveNamed<IGetRecordsService>(recordType.ToString());

        if (getRecordsService == null)
        {
            throw new ArgumentOutOfRangeException(nameof(recordType), $"No RecordService could be resolved for EntityType [{recordType.ToString()}]");
        }

        return getRecordsService;
    }

    public async Task ValidateAsync(RecordType recordType, long recordId, IHasUserAuthorizationInfo request = null)
    {
        var recordService = Resolve(recordType);

        await recordService.ValidateAsync(recordId, request);
    }

    public async Task SaveRydrRecordsAsync(RecordType recordType, ICollection<long> ids)
    {
        var recordService = Resolve(recordType);

        if (!(recordService is ISaveRydrRecordsService saveRydrRecordsService))
        {
            return;
        }

        await saveRydrRecordsService.SaveRecordsAsync(ids);
    }

    public async Task SaveRydrRecordsAsync(RecordType recordType, ICollection<DynamoItemIdEdge> compositeRecordIds)
    {
        var recordService = Resolve(recordType);

        if (!(recordService is ISaveRydrRecordsService saveRydrRecordsService))
        {
            return;
        }

        await saveRydrRecordsService.SaveRecordsAsync(compositeRecordIds);
    }

    public async Task<T> GetRecordAsync<T>(RecordType recordType, long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false)
        where T : class, ICanBeRecordLookup
    {
        var recordService = Resolve(recordType);

        var result = await recordService.GetRecordAsAsync<T>(recordId, request, isInternal);

        return result;
    }

    public IAsyncEnumerable<T> GetRecordsAsync<T>(RecordType recordType, IEnumerable<long> recordIds,
                                                  IHasUserAuthorizationInfo request = null, bool isInternal = false)
        where T : class, ICanBeRecordLookup
    {
        var recordService = Resolve(recordType);

        return recordService.GetRecordsAsAsync<T>(recordIds, request, isInternal);
    }

    public IAsyncEnumerable<T> GetRecords<T>(RecordType recordType, IEnumerable<DynamoItemIdEdge> compositeRecordIds)
        where T : class, ICanBeRecordLookup
    {
        var recordService = Resolve(recordType);

        return recordService.GetRecordsAsAsync<T>(compositeRecordIds);
    }
}
