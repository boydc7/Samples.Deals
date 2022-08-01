using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services.Auth
{
    public class DynAuthorizeService : TimestampCachedServiceBase<AuthorizerResult>, IAuthorizeService, IAuthorizer
    {
        private static readonly List<string> _allAuthorizationPrefixes = EnumsNET.Enums.GetNames<PublisherAccountConnectionType>()
                                                                                 .Where(n => !n.EqualsOrdinalCi(PublisherAccountConnectionType.Unspecified.ToString()))
                                                                                 .Concat(new[]
                                                                                         {
                                                                                             DynAuthorization.DefaultAuthorizationType
                                                                                         })
                                                                                 .Select(n => string.Concat(n, "|"))
                                                                                 .AsList();

        private readonly IPocoDynamo _dynamoDb;

        public DynAuthorizeService(IPocoDynamo dynamoDb, ICacheClient cacheClient)
            : base(cacheClient, 1200)
        {
            _dynamoDb = dynamoDb;
        }

        public bool CanExplicitlyAuthorize => true;
        public bool CanUnauthorize => false;

        public async Task AuthorizeAsync(long fromRecordId, long toRecordId, string authType = null)
        {
            Guard.AgainstArgumentOutOfRange(fromRecordId <= 0 || toRecordId <= 0, "from and to records must be valid");

            var edgeId = DynAuthorization.BuildEdgeId(toRecordId, authType);

            var existingAuthorization = await _dynamoDb.GetItemAsync<DynAuthorization>(fromRecordId, edgeId);

            if (existingAuthorization == null || existingAuthorization.IsDeleted())
            {
                var authorization = new DynAuthorization
                                    {
                                        FromRecordId = fromRecordId,
                                        ToRecordId = toRecordId,
                                        AuthorizationType = authType.Coalesce(DynAuthorization.DefaultAuthorizationType),
                                        EdgeId = edgeId,
                                        DynItemType = DynItemType.Authorization,
                                        ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant()
                                    };

                await _dynamoDb.PutItemTrackedAsync(authorization);
            }

            SetModel(GetAuthCacheKey(fromRecordId, authType, toRecordId), AuthorizerResult.ExplicitlyAuthorized);
        }

        public async Task DeAuthorizeAsync(long fromRecordId, long toRecordId, string authType = null)
        {
            Guard.AgainstArgumentOutOfRange(fromRecordId <= 0 || toRecordId <= 0, "from and to records must be valid");

            await _dynamoDb.DeleteItemAsync<DynAuthorization>(fromRecordId, DynAuthorization.BuildEdgeId(toRecordId, authType));

            SetModel(GetAuthCacheKey(fromRecordId, authType, toRecordId), AuthorizerResult.Unspecified);
        }

        public async Task DeAuthorizeAllToFromAsync(long toFromRecordId, string forAuthType = null)
        {
            var authPrefixes = forAuthType.HasValue()
                                   ? forAuthType.AsEnumerable()
                                   : _allAuthorizationPrefixes;

            foreach (var authPrefix in authPrefixes)
            {
                var dynAuthorizationsToDelete = await _dynamoDb.FromQuery<DynAuthorization>(a => a.Id == toFromRecordId &&
                                                                                                 Dynamo.BeginsWith(a.EdgeId, authPrefix))
                                                               .Filter(a => a.TypeId == (int)DynItemType.Authorization)
                                                               .Select(x => new
                                                                            {
                                                                                x.Id,
                                                                                x.EdgeId
                                                                            })
                                                               .ExecAsync()
                                                               .Select(d => d.ToDynamoId())
                                                               .ToHashSet();

                var fromAuthorizationsToDelete = await _dynamoDb.FromQueryIndex<DynItemEdgeIdGlobalIndex>(e => e.EdgeId == string.Concat(authPrefix, toFromRecordId) &&
                                                                                                               Dynamo.BeginsWith(e.TypeReference, string.Concat((int)DynItemType.Authorization, "|")))
                                                                .Filter(a => a.TypeId == (int)DynItemType.Authorization)
                                                                .Select(x => new
                                                                             {
                                                                                 x.Id,
                                                                                 x.EdgeId
                                                                             })
                                                                .ExecAsync()
                                                                .Select(d => d.GetDynamoId())
                                                                .ToHashSet();

                dynAuthorizationsToDelete.UnionWith(fromAuthorizationsToDelete);

                await _dynamoDb.DeleteItemsAsync<DynAuthorization>(dynAuthorizationsToDelete.Select(did =>
                                                                                                    {
                                                                                                        FlushModel(string.Concat(did.Hash.ToString(), "|", did.Range.ToString()));

                                                                                                        return did;
                                                                                                    }));
            }
        }

        public async Task<bool> IsAuthorizedAsync(long fromRecordId, long toRecordId, string authType = null)
        {
            var isAuthorized = await GetModelAsync(GetAuthCacheKey(fromRecordId, authType, toRecordId),
                                                   () => _dynamoDb.GetItemAsync<DynAuthorization>(fromRecordId,
                                                                                                  DynAuthorization.BuildEdgeId(toRecordId, authType))
                                                                  .Then(da => da == null || da.IsDeleted()
                                                                                  ? AuthorizerResult.Unspecified
                                                                                  : AuthorizerResult.ExplicitlyAuthorized));

            return isAuthorized != null && isAuthorized.FailLevel == AuthorizerFailLevel.ExplicitlyAuthorized;
        }

        public Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
            where T : ICanBeAuthorized
        {
            if (typeof(T) == typeof(DynItem) || !(toObject is ICanBeAuthorizedById toObjectById))
            {
                return Task.FromResult(AuthorizerResult.Unspecified);
            }

            var idToAuthorize = toObjectById.AuthorizeId();
            var idKey = GetAuthCacheKey(state.WorkspaceId.Gz(state.UserId), null, idToAuthorize);

            return GetModelAsync(idKey,
                                 async () =>
                                 {
                                     var workspacePublisherAccount = state.WorkspaceId > 0
                                                                         ? await WorkspaceService.DefaultWorkspaceService.TryGetDefaultPublisherAccountAsync(state.WorkspaceId)
                                                                         : null;

                                     var isAuthorized = await _dynamoDb.GetItemsAsync<DynAuthorization>(GetAuthDynamoIds(idToAuthorize, state, workspacePublisherAccount))
                                                                       .AnyAsync(a => a != null && !a.IsDeleted());

                                     return isAuthorized
                                                ? AuthorizerResult.ExplicitlyAuthorized
                                                : AuthorizerResult.Unspecified;
                                 });
        }

        private IEnumerable<DynamoId> GetAuthDynamoIds(long idToAuthorize, IHasUserAuthorizationInfo state, DynPublisherAccount workspacePublisherAccount)
        {
            var distinctUserIds = new HashSet<long>
                                  {
                                      state.RequestPublisherAccountId,
                                      state.WorkspaceId,
                                      state.UserId,
                                      state.RoleId,
                                      workspacePublisherAccount?.PublisherAccountId ?? 0
                                  };

            // Authorized to the id in question if the user is authorizedTo or was contactedBy the given id...
            return distinctUserIds.Where(i => i > 0)
                                  .Select(u => new DynamoId(u, DynAuthorization.BuildEdgeId(idToAuthorize)))
                                  .Concat(distinctUserIds.Where(i => i > 0)
                                                         .Select(u => new DynamoId(u, DynAuthorization.BuildEdgeId(idToAuthorize,
                                                                                                                   PublisherAccountConnectionType.ContactedBy.ToString()))))
                                  .Concat(distinctUserIds.Where(i => i > 0)
                                                         .Select(u => new DynamoId(u, DynAuthorization.BuildEdgeId(idToAuthorize,
                                                                                                                   PublisherAccountConnectionType.DealtWith.ToString()))));
        }

        private string GetAuthCacheKey(long fromRecordId, string authType, long toRecordId)
            => string.Concat(fromRecordId, "|", authType ?? DynAuthorization.DefaultAuthorizationType, "|", toRecordId);
    }
}
