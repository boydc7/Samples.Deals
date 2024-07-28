using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Services.Publishers;

public class TimestampedUserService : TimestampCachedServiceBase<DynUser>, IUserService
{
    private readonly IPocoDynamo _dynamoDb;
    private readonly IClientTokenAuthorizationService _clientTokenAuthorizationService;
    private readonly IAuthorizeService _authorizeService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IRydrDataService _rydrDataService;

    public TimestampedUserService(ICacheClient cacheClient,
                                  IPocoDynamo dynamoDb,
                                  IClientTokenAuthorizationService clientTokenAuthorizationService,
                                  IAuthorizeService authorizeService,
                                  IDeferRequestsService deferRequestsService,
                                  IRydrDataService rydrDataService)
        : base(cacheClient, 1100)
    {
        _dynamoDb = dynamoDb;
        _clientTokenAuthorizationService = clientTokenAuthorizationService;
        _authorizeService = authorizeService;
        _deferRequestsService = deferRequestsService;
        _rydrDataService = rydrDataService;
    }

    public DynUser TryGetUser(long userId, bool retryDelayedOnNotFound = false)
    {
        if (userId <= 0)
        {
            return null;
        }

        try
        {
            return GetModel(userId,
                            () => _dynamoDb.GetItemByRef<DynUser>(userId, userId.ToStringInvariant(), DynItemType.User, true, true));
        }
        catch(RecordNotFoundException) when(retryDelayedOnNotFound)
        {
            return GetModel(userId,
                            () => _dynamoDb.ExecDelayed(d => d.GetItemByRef<DynUser>(userId, userId.ToStringInvariant(), DynItemType.User, true, true)));
        }
    }

    public Task<DynUser> TryGetUserAsync(long userId, bool retryDelayedOnNotFound = false)
    {
        if (userId <= 0)
        {
            return Task.FromResult<DynUser>(null);
        }

        try
        {
            return GetModelAsync(userId,
                                 () => _dynamoDb.GetItemByRefAsync<DynUser>(userId, userId.ToStringInvariant(), DynItemType.User, true, true));
        }
        catch(RecordNotFoundException) when(retryDelayedOnNotFound)
        {
            return GetModelAsync(userId,
                                 () => _dynamoDb.ExecDelayedAsync(d => d.GetItemByRefAsync<DynUser>(userId, userId.ToStringInvariant(), DynItemType.User, true, true)));
        }
    }

    public DynUser GetUser(long userId)
        => GetModel(userId,
                    () => _dynamoDb.GetItemByRef<DynUser>(userId, userId.ToStringInvariant(), DynItemType.User));

    public Task<DynUser> GetUserAsync(long userId)
        => GetModelAsync(userId,
                         () => _dynamoDb.GetItemByRefAsync<DynUser>(userId, userId.ToStringInvariant(), DynItemType.User));

    public IAsyncEnumerable<DynUser> GetUsersAsync(IEnumerable<DynamoItemIdEdge> userIdAndUserNames)
        => _dynamoDb.QueryItemsAsync<DynUser>(userIdAndUserNames.Select(t => new DynamoId(t.Id, t.EdgeId)));

    public Task<DynUser> GetUserByAuthUidAsync(string authUid)
        => GetModelAsync(string.Concat("byauid:", authUid),
                         async () =>
                         {
                             var longId = authUid.ToLongHashCode();

                             var dynUserMap = await MapItemService.DefaultMapItemService
                                                                  .TryGetMapAsync(longId, DynItemMap.BuildEdgeId(DynItemType.User, authUid));

                             if (dynUserMap == null)
                             {
                                 return null;
                             }

                             return await _dynamoDb.GetItemAsync<DynUser>(dynUserMap.ReferenceNumber.Value, dynUserMap.MappedItemEdgeId);
                         });

    public DynUser GetUserByAuthUid(string authUid)
        => GetModel(string.Concat("byauid:", authUid),
                    () =>
                    {
                        var longId = authUid.ToLongHashCode();

                        var dynUserMap = MapItemService.DefaultMapItemService
                                                       .TryGetMap(longId, DynItemMap.BuildEdgeId(DynItemType.User, authUid));

                        if (dynUserMap == null)
                        {
                            return null;
                        }

                        return _dynamoDb.GetItem<DynUser>(dynUserMap.ReferenceNumber.Value, dynUserMap.MappedItemEdgeId);
                    });

    public Task<DynUser> GetUserByUserNameAsync(string userName)
        => GetModelAsync(string.Concat("byun:", userName.ToLowerInvariant()),
                         () => _dynamoDb.GetItemByEdgeIntoAsync<DynUser>(DynItemType.User, userName.ToLowerInvariant(), true));

    public DynUser GetUserByUserName(string userName)
        => GetModel(string.Concat("byun:", userName.ToLowerInvariant()),
                    () => _dynamoDb.GetItemByEdgeInto<DynUser>(DynItemType.User, userName.ToLowerInvariant(), true));

    public async Task LinkAuthUidToUserAsync(string authUid, string authToken, DynUser toUser)
    {
        Guard.AgainstNullArgument(toUser == null, nameof(toUser));
        Guard.AgainstNullArgument(authUid.IsNullOrEmpty(), nameof(authUid));
        Guard.AgainstNullArgument(authToken.IsNullOrEmpty(), nameof(authToken));

        var longId = authUid.ToLongHashCode();

        var validatedUid = await _clientTokenAuthorizationService.GetUidFromTokenAsync(authToken);

        Guard.AgainstUnauthorized(!authUid.EqualsOrdinal(validatedUid), "Auth information invalid, code[ryuarauid]");

        var dynUserMap = await MapItemService.DefaultMapItemService
                                             .TryGetMapAsync(longId, DynItemMap.BuildEdgeId(DynItemType.User, authUid));

        if (dynUserMap != null)
        {
            if (dynUserMap.ReferenceNumber.Value != toUser.UserId)
            {
                throw new DuplicateRecordException($"User with that authId info already exists. code x=[{dynUserMap.ReferenceNumber.Value}],y=[{toUser.UserId}],z=[{authUid}]");
            }

            if (!dynUserMap.MappedItemEdgeId.EqualsOrdinalCi(toUser.EdgeId))
            { //  User email address can change (userId cannot) - if this occurs, update the map
                dynUserMap.MappedItemEdgeId = toUser.EdgeId;

                await MapItemService.DefaultMapItemService.PutMapAsync(dynUserMap);
            }

            // Nothin else to do...
            return;
        }

        await MapItemService.DefaultMapItemService.PutMapAsync(new DynItemMap
                                                               {
                                                                   Id = longId,
                                                                   EdgeId = DynItemMap.BuildEdgeId(DynItemType.User, authUid),
                                                                   ReferenceNumber = toUser.UserId,
                                                                   MappedItemEdgeId = toUser.EdgeId
                                                               });
    }

    public async Task DeleteUserAsync(DynUser dynUser, IHasUserAuthorizationInfo withState = null,
                                      bool hardDelete = false, string authUid = null)
    {
        if (hardDelete)
        {
            await _dynamoDb.DeleteItemAsync<DynUser>(dynUser.UserId, dynUser.EdgeId);
        }
        else
        {
            await _dynamoDb.SoftDeleteAsync(dynUser, withState);
        }

        await Try.ExecAsync(() => _rydrDataService.DeleteByIdAsync<RydrUser>(dynUser.UserId));

        if (authUid.HasValue())
        {
            var longId = authUid.ToLongHashCode();

            await MapItemService.DefaultMapItemService.DeleteMapAsync(longId, DynItemMap.BuildEdgeId(DynItemType.User, authUid));
        }

        await _authorizeService.DeAuthorizeAllToFromAsync(dynUser.UserId);

        FlushModel(dynUser.UserId);
    }

    public async Task UpdateUserAsync(DynUser dynUser)
    {
        await _dynamoDb.PutItemTrackedAsync(dynUser, dynUser);

        SetModel(dynUser.UserId, dynUser);

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 Ids = new List<long>
                                                       {
                                                           dynUser.UserId
                                                       },
                                                 Type = RecordType.User
                                             });
    }
}
