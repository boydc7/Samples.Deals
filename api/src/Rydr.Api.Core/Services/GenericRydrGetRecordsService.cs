using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Services
{
    public class GenericRydrGetRecordsService<T> : IGetRecordsService<T>
        where T : class, ICanBeRecordLookup
    {
        private readonly IRydrDataService _rydrDataService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IRequestStateManager _requestStateManager;

        public GenericRydrGetRecordsService(RecordType forRecordType,
                                            IRydrDataService rydrDataService,
                                            IAuthorizationService authorizationService,
                                            IRequestStateManager requestStateManager)
        {
            Guard.AgainstArgumentOutOfRange(forRecordType == RecordType.Unknown, nameof(forRecordType));

            _rydrDataService = rydrDataService;
            _authorizationService = authorizationService;
            _requestStateManager = requestStateManager;
            ForRecordType = forRecordType;
        }

        public RecordType ForRecordType { get; }

        public async Task ValidateAsync(long recordId, IHasUserAuthorizationInfo request = null)
        {
            var entity = await _rydrDataService.SingleByIdAsync<T, long>(recordId);

            // Validate access
            await _authorizationService.VerifyAccessToAsync(entity, request ?? _requestStateManager.GetState());
        }

        public async Task<T> GetRecordAsync(long recordId, IHasUserAuthorizationInfo request = null, bool isInternal = false)
        {
            var entity = await _rydrDataService.SingleByIdAsync<T, long>(recordId);

            if (!isInternal)
            { // Validate access
                await _authorizationService.VerifyAccessToAsync(entity, request ?? _requestStateManager.GetState());
            }

            return entity;
        }

        public async IAsyncEnumerable<T> GetRecordsAsync(IEnumerable<long> recordIds, IHasUserAuthorizationInfo request = null, bool isInternal = false)
        {
            var requestState = _requestStateManager.GetState();

            foreach (var entity in await _rydrDataService.SelectByIdAsync<T>(recordIds, 500))
            {
                if (!isInternal)
                { // Validate access
                    await _authorizationService.VerifyAccessToAsync(entity, request ?? requestState);
                }

                yield return entity;
            }
        }

        public IAsyncEnumerable<T> GetRecordsAsync(IEnumerable<DynamoItemIdEdge> compositeRecordIds)
            => throw new NotImplementedException();

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
                if (rawEntity == null || !(rawEntity is TAs entity) || entity == null)
                {
                    throw new InvalidDataArgumentException($"Records for type [{ForRecordType.ToString()}] cannot be typed as [{typeof(TAs).Name}]");
                }

                yield return entity;
            }
        }

        public IAsyncEnumerable<TAs> GetRecordsAsAsync<TAs>(IEnumerable<DynamoItemIdEdge> compositeRecordIds) where TAs : class, ICanBeRecordLookup
            => throw new NotImplementedException();
    }
}
