using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services
{
    public class DynAssociationService : DynAssociationServiceBase<DynAssociation>, IAssociationService
    {
        public DynAssociationService(IPocoDynamo dynamoDb, IRequestStateManager requestStateManager) : base(dynamoDb, requestStateManager) { }
    }

    public abstract class DynAssociationServiceBase<T>
        where T : DynAssociation
    {
        protected readonly IPocoDynamo _dynamoDb;
        private readonly IRequestStateManager _requestStateManager;

        public DynAssociationServiceBase(IPocoDynamo dynamoDb,
                                         IRequestStateManager requestStateManager)
        {
            _dynamoDb = dynamoDb;
            _requestStateManager = requestStateManager;
        }

        /// <summary>
        ///     Returns IDs of the "idRecordType" type that are associated with the "withRecordId" and "withRecordType" values.
        /// </summary>
        /// <param name="toRecordType">The type of record to return ID values for</param>
        /// <param name="fromRecordId">The ID of the record to which the returned IDs are associated with</param>
        /// <param name="fromRecordType">
        ///     The type of record for the withRecordId value, or the type of record to which the returned
        ///     IDs are associated with
        /// </param>
        public IAsyncEnumerable<string> GetAssociatedIdsAsync(long fromRecordId, RecordType toRecordType, RecordType? fromRecordType = null)
        {
            var query = _dynamoDb.FromQuery<DynAssociation>(a => a.Id == fromRecordId);

            if (fromRecordType.HasValue)
            {
                query.Filter(a => a.DynEdgeRecordType == (int)toRecordType &&
                                  a.TypeId == (int)DynItemType.Association &&
                                  a.DeletedOnUtc == null &&
                                  a.DynIdRecordType == (int)fromRecordType);
            }
            else
            {
                query.Filter(a => a.DynEdgeRecordType == (int)toRecordType &&
                                  a.TypeId == (int)DynItemType.Association &&
                                  a.DeletedOnUtc == null);
            }

            return query.ExecColumnAsync(a => a.EdgeId);
        }

        public IAsyncEnumerable<T> GetAssociationsAsync(long fromRecordId, long minToId, long maxToId, params RecordType[] toRecordTypes)
        {
            var minEdgeId = minToId.Gz(1).ToEdgeId();
            var maxEdgeId = maxToId.Gz(long.MaxValue).ToEdgeId();
            var recordTypeInts = toRecordTypes.Select(r => (int)r).AsList();

            return _dynamoDb.FromQuery<T>(a => a.Id == fromRecordId &&
                                               Dynamo.Between(a.EdgeId, minEdgeId, maxEdgeId))
                            .Filter(a => a.TypeId == (int)DynItemType.Association &&
                                         a.DeletedOnUtc == null &&
                                         recordTypeInts.Contains(a.DynEdgeRecordType))
                            .ExecAsync();
        }

        public IAsyncEnumerable<T> GetAssociationsToAsync(string toRecordId, RecordType? fromRecordType = null, RecordType? toRecordType = null)
            => _dynamoDb.GetItemsFromAsync<T, DynItemEdgeIdGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(i => i.EdgeId == toRecordId &&
                                                                                                                                Dynamo.BeginsWith(i.TypeReference,
                                                                                                                                                  string.Concat((int)DynItemType.Association, "|")))
                                                                                 .Filter(i => i.DeletedOnUtc == null)
                                                                                 .Select(i => new
                                                                                              {
                                                                                                  i.Id,
                                                                                                  i.EdgeId
                                                                                              })
                                                                                 .ExecAsync())
                        .Where(a => (!fromRecordType.HasValue || a.IdRecordType == fromRecordType.Value) &&
                                    (!toRecordType.HasValue || a.EdgeRecordType == toRecordType.Value));

        public async Task<bool> IsAssociatedAsync(long fromRecordId, string toRecordId)
        {
            var association = await _dynamoDb.GetItemAsync<T>(fromRecordId, toRecordId);

            return association != null &&
                   !association.IsDeleted() &&
                   association.DynItemType == DynItemType.Association;
        }

        public async Task<T> GetAssociationAsync(long fromRecordId, string toRecordId)
        {
            var association = await _dynamoDb.ExecDelayedAsync(d => d.GetItemAsync<T>(fromRecordId, toRecordId));

            Guard.AgainstRecordNotFound(association == null || association.DynItemType != DynItemType.Association, string.Concat("Association|", fromRecordId, "|", toRecordId));

            return association;
        }

        public async Task DeleteAssociationAsync(RecordType fromRecordType, long fromRecordId, RecordType toRecordType, string toRecordId)
        {
            var association = await GetAssociationAsync(fromRecordId, toRecordId);

            Guard.AgainstRecordNotFound(association.IdRecordType != fromRecordType || association.EdgeRecordType != toRecordType);

            await DeleteAssociationAsync(association);
        }

        public Task DeleteAssociationAsync(T association)
            => _dynamoDb.DeleteItemAsync<T>(association.Id, association.EdgeId);

        public async Task DeleteAssociationsToAsync(RecordType toRecordType, string toRecordId, RecordType fromRecordType = RecordType.Unknown)
        {
            await foreach (var batchToDelete in _dynamoDb.GetItemsFromAsync<T, DynItemEdgeIdGlobalIndex>(_dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(e => e.EdgeId == toRecordId &&
                                                                                                                                                                 Dynamo.BeginsWith(e.TypeReference,
                                                                                                                                                                                   string.Concat((int)DynItemType.Association, "|")))
                                                                                                                  .Filter(e => e.DeletedOnUtc == null)
                                                                                                                  .Select(x => new
                                                                                                                               {
                                                                                                                                   x.Id,
                                                                                                                                   x.EdgeId
                                                                                                                               })
                                                                                                                  .ExecAsync())
                                                         .ToBatchesOfAsync(100, true))
            {
                if (batchToDelete == null)
                {
                    continue;
                }

                await _dynamoDb.DeleteItemsAsync<T>(batchToDelete.Select(t =>
                                                                         {
                                                                             Guard.AgainstInvalidData(t.EdgeRecordType != toRecordType, "Type mismatch in associations (trt)");

                                                                             if (fromRecordType != RecordType.Unknown)
                                                                             {
                                                                                 Guard.AgainstInvalidData(t.IdRecordType != fromRecordType, "Type mismatch in association deletion (frt)");
                                                                             }

                                                                             return t.ToDynamoId();
                                                                         }));
            }
        }

        public async Task DeleteAssociationsFromAsync(RecordType fromRecordType, long fromRecordId, RecordType toRecordType = RecordType.Unknown)
        {
            await foreach (var batchToDelete in _dynamoDb.GetItemsFromAsync<T, DynAssociation>(_dynamoDb.FromQuery<DynAssociation>(d => d.Id == fromRecordId)
                                                                                                        .Filter(d => d.DeletedOnUtc == null &&
                                                                                                                     d.TypeId == (int)DynItemType.Association &&
                                                                                                                     d.DynIdRecordType == (int)fromRecordType)
                                                                                                        .Select(x => new
                                                                                                                     {
                                                                                                                         x.Id,
                                                                                                                         x.EdgeId
                                                                                                                     })
                                                                                                        .ExecAsync())
                                                         .ToBatchesOfAsync(100, true))
            {
                if (batchToDelete == null)
                {
                    continue;
                }

                await _dynamoDb.DeleteItemsAsync<T>(batchToDelete.Select(t =>
                                                                         {
                                                                             if (toRecordType != RecordType.Unknown)
                                                                             {
                                                                                 Guard.AgainstInvalidData(t.EdgeRecordType != toRecordType, "Type mismatch in association deletion");
                                                                             }

                                                                             return t.ToDynamoId();
                                                                         }));
            }
        }

        public Task<bool> UnDeleteAssociationAsync(T association)
        {
            if (association == null || !association.IsDeleted())
            {
                return Task.FromResult(false);
            }

            return _dynamoDb.SoftUnDeleteAsync(association);
        }

        public async Task<T> AssociateAsync(T association, bool sourceRecordUpdatable = false)
        {
            var state = _requestStateManager.GetState();

            Guard.AgainstNullArgument(association == null, nameof(association));

            await association.VerifyAccessToAssociatedAsync(state);

            association.UpdateDateTimeTrackedValues(state);

            if (association.WorkspaceId <= 0)
            {
                association.WorkspaceId = state.WorkspaceId;
            }

            // Verify there is no existing association by ID combination or source...
            var existingById = await _dynamoDb.GetItemAsync<T>(association.Id, association.EdgeId);

            if (existingById != null)
            { // Existing by IDs...ensure state/source/etc. match, and if so un-delete it and return
                Guard.AgainstInvalidData(existingById.DynItemType != DynItemType.Association || existingById.IdRecordType != association.IdRecordType ||
                                         existingById.EdgeRecordType != association.EdgeRecordType ||
                                         (!sourceRecordUpdatable && !existingById.ReferenceId.EqualsOrdinalCi(association.ReferenceId)),
                                         $"Existing association by Id/Edge exists for IDs passed however state information is mismatched - code [{(int)existingById.IdRecordType}-{(int)association.IdRecordType}|{(int)existingById.EdgeRecordType}-{(int)association.EdgeRecordType}|{existingById.ReferenceId}-{association.ReferenceId}|{association.Id}->{association.EdgeId}");

                // Update if the source record is updatable and refId is different
                // OR
                // If they match and the incoming association has additional info...
                if ((sourceRecordUpdatable && !existingById.ReferenceId.EqualsOrdinalCi(association.ReferenceId)) ||
                    (existingById.ReferenceId.EqualsOrdinalCi(association.ReferenceId) && association.ExpiresAt.HasValue))
                { // Just put the new one over the top of the existing one
                    await _dynamoDb.PutItemAsync(association);

                    return association;
                }

                // Just un-delete the existing one
                await UnDeleteAssociationAsync(existingById);

                return existingById;
            }

            // Add the new one
            await _dynamoDb.PutItemAsync(association);

            return association;
        }
    }
}
