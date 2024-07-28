using Amazon.DynamoDBv2;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;
using ServiceStack.Model;

namespace Rydr.Api.Core.Services;

public class GenericDynGetRecordsService<T, TRydr, TRydrIdType> : IGetRecordsService<T>, ISaveRydrRecordsService
    where T : class, ICanBeRecordLookup
    where TRydr : IHasId<TRydrIdType>
{
    private readonly ILog _log;
    private readonly IPocoDynamo _dynamoDb;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRequestStateManager _requestStateManager;
    private readonly IRydrDataService _rydrDataService;
    private readonly Func<long, string, IPocoDynamo, DynamoId> _idResolver;
    private readonly Func<long, string, IPocoDynamo, Task<T>> _recordResolver;
    private readonly Func<T, IEnumerable<TRydr>> _rydrTransform;

    public GenericDynGetRecordsService(RecordType forRecordType,
                                       IPocoDynamo dynamoDb,
                                       IAuthorizationService authorizationService,
                                       IRequestStateManager requestStateManager,
                                       IRydrDataService rydrDataService,
                                       Func<long, string, IPocoDynamo, DynamoId> idResolver,
                                       Func<long, string, IPocoDynamo, Task<T>> recordResolver,
                                       Func<T, IEnumerable<TRydr>> rydrTransform = null)
    {
        Guard.AgainstArgumentOutOfRange(forRecordType == RecordType.Unknown, nameof(forRecordType));

        _dynamoDb = dynamoDb;
        _authorizationService = authorizationService;
        _requestStateManager = requestStateManager;
        _rydrDataService = rydrDataService;
        _idResolver = idResolver;
        _rydrTransform = rydrTransform;
        ForRecordType = forRecordType;
        _recordResolver = recordResolver;

        _log = LogManager.GetLogger(GetType());
    }

    public RecordType ForRecordType { get; }

    public async Task ValidateAsync(long recordId, IHasUserAuthorizationInfo request = null)
    {
        var entity = await GetDynRecordAsync(recordId);

        // Validate access
        await _authorizationService.VerifyAccessToAsync(entity, request ?? _requestStateManager.GetState());
    }

    private async Task<T> GetDynRecordAsync(long longId, string edgeId = null)
    {
        try
        {
            return (_recordResolver == null
                        ? null
                        : await _recordResolver(longId, edgeId, _dynamoDb)
                          ??
                          (_idResolver == null
                               ? edgeId.HasValue()
                                     ? await _dynamoDb.GetItemAsync<T>(longId, edgeId)
                                     : null
                               : await _dynamoDb.GetItemAsync<T>(_idResolver(longId, edgeId, _dynamoDb))));
        }
        catch(AmazonDynamoDBException)
        {
            _log.WarnFormat("DynamoDbException in GenericDynGetRecordsService - typeof(T) [{0}], RecordType [{1}], LongId: [{2}], EdgeId: [{3}]",
                            typeof(T).Name, ForRecordType, longId, edgeId ?? "null");

            throw;
        }
    }

    public async Task<T> GetRecordAsync(long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false)
    {
        var entity = await GetDynRecordAsync(recordId);

        if (!isInternal)
        { // Validate access
            var state = request ?? _requestStateManager.GetState();

            await _authorizationService.VerifyAccessToAsync(entity, state);
        }

        return entity;
    }

    public async Task SaveRecordsAsync(ICollection<long> ids)
    {
        if (_rydrTransform == null || ids == null || ids.Count <= 0)
        {
            return;
        }

        // Nearly all of these messages have 1 object inside, so we optimized for that case (which is why we're looping over the Id enumerable instead
        // of using a SaveRanges or similar operation
        if (ids.Count == 1)
        {
            var recordId = ids.Single();

            var record = await GetDynRecordAsync(recordId);

            foreach (var rydrRecord in _rydrTransform(record))
            {
                await _rydrDataService.SaveAsync(rydrRecord, r => r.Id);
            }
        }
        else if (ids.Count > 1)
        {
            await foreach (var recordBatch in GetRecordsAsync(ids).ToBatchesOfAsync(25))
            {
                await _rydrDataService.SaveRangeAsync(recordBatch.SelectMany(r => _rydrTransform(r)), r => r.Id);
            }
        }
    }

    public async Task SaveRecordsAsync(ICollection<DynamoItemIdEdge> compositeRecordIds)
    {
        if (_rydrTransform == null || compositeRecordIds == null || compositeRecordIds.Count <= 0)
        {
            return;
        }

        // Nearly all of these messages have 1 object inside, so we optimized for that case (which is why we're looping over the Id enumerable instead
        // of using a SaveRanges or similar operation
        if (compositeRecordIds.Count == 1)
        {
            var recordId = compositeRecordIds.Single();

            var record = await GetDynRecordAsync(recordId.Id, recordId.EdgeId);

            if (record == null)
            {
                return;
            }

            foreach (var rydrRecord in _rydrTransform(record))
            {
                await _rydrDataService.SaveAsync(rydrRecord, r => r.Id);
            }
        }
        else if (compositeRecordIds.Count > 1)
        {
            await foreach (var recordBatch in GetRecordsAsync(compositeRecordIds).ToBatchesOfAsync(25))
            {
                await _rydrDataService.SaveRangeAsync(recordBatch.SelectMany(r => _rydrTransform(r)), r => r.Id);
            }
        }
    }

    public async IAsyncEnumerable<T> GetRecordsAsync(IEnumerable<DynamoItemIdEdge> compositeRecordIds)
    {
        if (_idResolver != null)
        {
            await foreach (var item in _dynamoDb.QueryItemsAsync<T>(compositeRecordIds.Select(c => _idResolver(c.Id, c.EdgeId, _dynamoDb)))
                                                .Where(t => t != null))
            {
                yield return item;
            }
        }
        else
        {
            foreach (var compositeRecordId in compositeRecordIds?.Distinct() ?? [])
            {
                var item = await _recordResolver(compositeRecordId.Id, compositeRecordId.EdgeId, _dynamoDb);

                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<T> GetRecordsAsync(IEnumerable<long> recordIds, IHasUserAuthorizationInfo request = null, bool isInternal = false)
    {
        if (_idResolver != null || _recordResolver == null)
        {
            await foreach (var entity in (_idResolver == null
                                              ? _dynamoDb.QueryItemsAsync<T>(recordIds.Distinct()
                                                                                      .Select(l => (object)l))
                                              : _dynamoDb.QueryItemsAsync<T>(recordIds.Distinct()
                                                                                      .Select(i => _idResolver(i,
                                                                                                               i.ToEdgeId(),
                                                                                                               _dynamoDb)))))
            {
                if (!isInternal)
                { // Validate access
                    var state = request ?? _requestStateManager.GetState();

                    await _authorizationService.VerifyAccessToAsync(entity, state);
                }

                if (entity != null)
                {
                    yield return entity;
                }
            }
        }
        else if (_recordResolver != null)
        {
            foreach (var recordId in recordIds.Distinct())
            {
                var item = await _recordResolver(recordId, null, _dynamoDb);

                if (!isInternal)
                { // Validate access
                    var state = request ?? _requestStateManager.GetState();

                    await _authorizationService.VerifyAccessToAsync(item, state);
                }

                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }

    public async Task<TAs> GetRecordAsAsync<TAs>(long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false)
        where TAs : class, ICanBeRecordLookup
    {
        var record = await GetRecordAsync(recordId, request, isInternal);

        if (record == null)
        {
            return null;
        }

        if (!(record is TAs entity))
        {
            throw new InvalidDataArgumentException($"Records for type [{ForRecordType.ToString()}] cannot be typed as [{typeof(TAs).Name}]");
        }

        return entity;
    }

    public async IAsyncEnumerable<TAs> GetRecordsAsAsync<TAs>(IEnumerable<long> recordIds, IHasUserAuthorizationInfo request = null, bool isInternal = false)
        where TAs : class, ICanBeRecordLookup
    {
        await foreach (var rawEntity in GetRecordsAsync(recordIds, request, isInternal))
        {
            if (!(rawEntity is TAs entity) || entity == null)
            {
                throw new InvalidDataArgumentException($"Records for type [{ForRecordType.ToString()}] cannot be typed as [{typeof(TAs).Name}]");
            }

            yield return entity;
        }
    }

    public async IAsyncEnumerable<TAs> GetRecordsAsAsync<TAs>(IEnumerable<DynamoItemIdEdge> compositeRecordIds)
        where TAs : class, ICanBeRecordLookup
    {
        await foreach (var rawEntity in GetRecordsAsync(compositeRecordIds))
        {
            if (rawEntity == null || !(rawEntity is TAs entity) || entity == null)
            {
                throw new InvalidDataArgumentException($"Records for type [{ForRecordType.ToString()}] cannot be typed as [{typeof(TAs).Name}]");
            }

            yield return entity;
        }
    }
}
