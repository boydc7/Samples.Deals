using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.DataAccess.Repositories
{
    public class RydrDynamoFirebaseAuthUserRepository : IRydrUserAuthRepository
    {
        private static readonly List<IUserAuthDetails> _emptyAuthDetails = new List<IUserAuthDetails>();

        private readonly IPocoDynamo _dynamoDb;
        private readonly IUserService _userService;
        private readonly IClientTokenAuthorizationService _clientTokenAuthorizationService;

        public RydrDynamoFirebaseAuthUserRepository(IPocoDynamo dynamoDb,
                                                    IUserService userService,
                                                    IClientTokenAuthorizationService clientTokenAuthorizationService)
        {
            _dynamoDb = dynamoDb;
            _userService = userService;
            _clientTokenAuthorizationService = clientTokenAuthorizationService;
        }

        public void LoadUserAuth(IAuthSession session, IAuthTokens tokens)
        {
            Guard.AgainstNullArgument(session == null, nameof(session));

            ToRydrUserSession(session);
        }

        public List<IUserAuthDetails> GetUserAuthDetails(string userAuthId) => _emptyAuthDetails;

        public IUserAuthDetails CreateOrMergeAuthSession(IAuthSession authSession, IAuthTokens tokens)
            => throw new NotImplementedException("CreateOrMergeAuthSession should not be called?");

        public IUserAuth GetUserAuth(string userAuthId)
            => GetDynUser(userAuthId.ToLong());

        public IUserAuth GetUserAuth(IAuthSession authSession, IAuthTokens tokens)
            => GetDynUser(ToRydrUserSession(authSession));

        public IUserAuth GetUserAuthByUserName(string userNameOrEmail)
            => _userService.GetUserByUserName(userNameOrEmail);

        public void SaveUserAuth(IUserAuth userAuth)
            => SaveDynUserAsync(GetDynUser(userAuth)).GetAwaiter().GetResult();

        public void SaveUserAuth(IAuthSession authSession)
        {
            var rydrSession = ToRydrUserSession(authSession);

            Guard.AgainstNullArgument(rydrSession == null, nameof(rydrSession));

            var dynUser = GetDynUser(rydrSession);

            SaveDynUserAsync(dynUser).GetAwaiter().GetResult();
        }

        // We do not support anything but api key auth...
        public bool TryAuthenticate(string userName, string password, out IUserAuth userAuth)
        {
            userAuth = null;

            return false;
        }

        public bool TryAuthenticate(Dictionary<string, string> digestHeaders, string privateKey, int nonceTimeOut, string sequence, out IUserAuth userAuth)
        {
            userAuth = null;

            return false;
        }

        public IUserAuth CreateUserAuth(IUserAuth newUser, string password)
            => throw new NotImplementedException("CreateUserAuth not valid for use");

        public async Task<DynUser> CreateUserAuthAsync(DynUser newUser)
        {
            Guard.AgainstNullArgument(newUser.UserName.IsNullOrEmpty(), "UserName must be specified");
            Guard.AgainstArgumentOutOfRange(newUser.UserId > 0, "ID cannot be specified");
            Guard.AgainstArgumentOutOfRange(newUser.UserType == UserType.Unknown, "Invalid UserType");

            var existingUser = await GetDynUserByUserNameAsync(newUser.UserName);

            if (existingUser != null)
            {
                throw new DuplicateRecordException("User with that username already exists");
            }

            var now = DateTimeHelper.UtcNowTs;

            newUser.UserId = Sequences.Next();
            newUser.ReferenceId = newUser.UserId.ToStringInvariant();
            newUser.TypeId = (int)DynItemType.User;
            newUser.PasswordHash = 7.Times(Guid.NewGuid).Join("|-|").ToShaBase64();
            newUser.Salt = 7.Times(Guid.NewGuid).Join("|-|").ToShaBase64();
            newUser.CreatedBy = newUser.UserId;
            newUser.CreatedWorkspaceId = newUser.UserId;
            newUser.ModifiedBy = newUser.UserId;
            newUser.ModifiedWorkspaceId = newUser.UserId;
            newUser.CreatedOnUtc = now;
            newUser.ModifiedOnUtc = now;

            if (newUser.Roles == null)
            {
                newUser.Roles = new List<string>();
            }

            newUser.Roles.AddIfNotExists(newUser.UserType.ToString());

            await SaveDynUserAsync(newUser);

            return newUser;
        }

        public IUserAuth UpdateUserAuth(IUserAuth existingUser, IUserAuth newUser)
            => throw new NotImplementedException("UpdateUserAuth not valid for use");

        public IUserAuth UpdateUserAuth(IUserAuth existingUser, IUserAuth newUser, string password)
            => throw new NotImplementedException("UpdateUserAuth not valid for use");

        public void DeleteUserAuth(string userAuthId)
            => DeleteUserAuth(userAuthId, null);

        public void DeleteUserAuth(string userAuthId, IHasUserAuthorizationInfo withState, bool hardDelete = false)
        {
            var userId = userAuthId.ToLong();

            var dynUser = _userService.TryGetUser(userId);

            if (dynUser != null)
            {
                _userService.DeleteUserAsync(dynUser, withState, hardDelete).GetAwaiter().GetResult();
            }
        }

        public void GetRolesAndPermissions(string userAuthId, out ICollection<string> roles, out ICollection<string> permissions)
        {
            var userId = userAuthId.ToLong();

            var dynUser = _userService.TryGetUser(userId, true);

            roles = GetRydrUserRoles(dynUser).AsList();
            permissions = dynUser?.Permissions;
        }

        public ICollection<string> GetRoles(string userAuthId)
        {
            var userId = userAuthId.ToLong();

            var dynUser = _userService.TryGetUser(userId, true);

            return GetRydrUserRoles(dynUser).AsList();
        }

        public ICollection<string> GetPermissions(string userAuthId)
        {
            var userId = userAuthId.ToLong();

            var dynUser = _userService.GetUser(userId);

            return dynUser.Permissions;
        }

        public bool HasRole(string userAuthId, string role)
        {
            if (!role.HasValue())
            {
                return false;
            }

            var userId = userAuthId.ToLong();

            var dynUser = _userService.TryGetUser(userId, true);

            return dynUser != null &&
                   (string.Equals(dynUser.UserType.ToString(), role, StringComparison.OrdinalIgnoreCase) ||
                    (!dynUser.Roles.IsNullOrEmpty() && dynUser.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)));
        }

        public bool HasPermission(string userAuthId, string permission)
        {
            if (!permission.HasValue())
            {
                return false;
            }

            var userId = userAuthId.ToLong();

            var dynUser = _userService.TryGetUser(userId, true);

            return !(dynUser?.Permissions).IsNullOrEmpty() && dynUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        public void AssignRoles(string userAuthId, ICollection<string> roles = null, ICollection<string> permissions = null)
        {
            var userId = userAuthId.ToLong();

            if (userId <= 0)
            {
                return;
            }

            var dynUser = _userService.TryGetUser(userId, true);

            if (dynUser == null)
            {
                return;
            }

            if (roles != null && roles.Count > 0)
            {
                dynUser.Roles = (dynUser.Roles ?? Enumerable.Empty<string>()).Union(roles).AsList().NullIfEmpty();
            }

            if (permissions != null && permissions.Count > 0)
            {
                dynUser.Permissions = (dynUser.Permissions ?? Enumerable.Empty<string>()).Union(permissions).AsList().NullIfEmpty();
            }

            _userService.UpdateUserAsync(dynUser).GetAwaiter().GetResult();
        }

        public void UnAssignRoles(string userAuthId, ICollection<string> roles = null, ICollection<string> permissions = null)
        {
            var userId = userAuthId.ToLong();

            if (userId <= 0)
            {
                return;
            }

            var dynUser = _userService.TryGetUser(userId);

            if (dynUser == null)
            {
                return;
            }

            if (roles != null && roles.Count > 0)
            {
                roles.Each(r => dynUser.Roles.RemoveAll(x => x.EqualsOrdinalCi(r)));

                if (dynUser.Roles.IsNullOrEmpty())
                {
                    dynUser.Roles = null;
                }
            }

            if (permissions != null && permissions.Count > 0)
            {
                permissions.Each(p => dynUser.Permissions.RemoveAll(x => x.EqualsOrdinalCi(p)));

                if (dynUser.Permissions.IsNullOrEmpty())
                {
                    dynUser.Permissions = null;
                }
            }

            _userService.UpdateUserAsync(dynUser).GetAwaiter().GetResult();
        }

        public void Clear()
            => throw new NotImplementedException("Purposely not implementing Clear() method on auth repo");

        public void InitSchema() { }

        public void InitApiKeySchema() { }

        public bool ApiKeyExists(string apiKey)
        {
            if (apiKey.IsNullOrEmpty() || apiKey.Length > 90)
            {
                return false;
            }

            var longId = apiKey.ToLongHashCode();

            var dynUserMap = MapItemService.DefaultMapItemService.TryGetMap(longId, DynItemMap.BuildEdgeId(DynItemType.Credential, apiKey));

            return dynUserMap != null;
        }

        public ApiKey GetApiKey(string apiKey)
        {
            var isRydrApiKey = apiKey.Length <= 90;

            // Start with seeing if this is a valid firebase token
            var firebaseUid = isRydrApiKey
                                  ? null
                                  : _clientTokenAuthorizationService.GetUidFromTokenAsync(apiKey).GetAwaiter().GetResult().ToNullIfEmpty();

            if (!isRydrApiKey)
            {
                if (firebaseUid.IsNullOrEmpty())
                {
                    return null;
                }

                var firebaseDynUser = _userService.GetUserByAuthUid(firebaseUid);

                return firebaseDynUser == null || firebaseDynUser.IsDeleted() || firebaseDynUser.UserId <= 0
                           ? null
                           : new ApiKey
                             {
                                 Id = apiKey,
                                 UserAuthId = firebaseDynUser.UserId.ToStringInvariant()
                             };
            }

            var cacheKey = string.Concat("RydrDynGfbAuthGetApiKey|", apiKey);

            var apiKeyModel = InMemoryCacheClient.Default.Get<ApiKey>(cacheKey);
            var nowUtc = DateTimeHelper.UtcNow;

            if (apiKeyModel != null)
            {
                if (apiKeyModel.ExpiryDate.HasValue && apiKeyModel.ExpiryDate.Value <= nowUtc)
                {
                    InMemoryCacheClient.Default.Remove(cacheKey);
                    apiKeyModel = null;
                }
                else
                {
                    return apiKeyModel;
                }
            }

            var longId = apiKey.ToLongHashCode();

            var dynItemMap = MapItemService.DefaultMapItemService.TryGetMap(longId, DynItemMap.BuildEdgeId(DynItemType.Credential, apiKey));

            if (dynItemMap != null && (dynItemMap.ExpiresAt <= 0 || dynItemMap.ExpiresAt > nowUtc.ToUnixTimestamp()))
            {
                apiKeyModel = new ApiKey
                              {
                                  Id = apiKey,
                                  UserAuthId = dynItemMap.ReferenceNumber.Value.ToStringInvariant(),
                                  ExpiryDate = dynItemMap.ExpiresAt > 0
                                                   ? dynItemMap.ExpiresAt.ToDateTime()
                                                   : (DateTime?)null
                              };

                InMemoryCacheClient.Default.Set(cacheKey,
                                                apiKeyModel,
                                                CacheExpiry.GetCacheExpireTime(CacheConfig.LongConfig.DurationSeconds,
                                                                               CacheConfig.LongConfig.MinutesPastMidnight)
                                                           .Value);
            }

            return apiKeyModel;
        }

        public List<ApiKey> GetUserApiKeys(string userId)
        {
            var userIdLong = userId.ToLong();

            return _dynamoDb.FromQuery<DynItemMap>(m => m.Id == userIdLong &&
                                                        Dynamo.BeginsWith(m.EdgeId, string.Concat((int)DynItemType.Credential, "|apikey|")))
                            .Exec()
                            .Where(m => m.ExpiresAt <= 0 || m.ExpiresAt > DateTimeHelper.UtcNowTs)
                            .Take(25)
                            .Select(m => new ApiKey
                                         {
                                             Id = DynItemMap.GetFinalEdgeSegment(m.MappedItemEdgeId),
                                             UserAuthId = m.ReferenceNumber.Value.ToStringInvariant()
                                         })
                            .AsList();
        }

        public void StoreAll(IEnumerable<ApiKey> apiKeys)
        {
            foreach (var apiKey in apiKeys.Where(k => k.Id.HasValue() && k.Id.Length <= 90))
            {
                var userId = apiKey.UserAuthId.ToLong();

                if (userId <= 0)
                {
                    continue;
                }

                var dynUser = _userService.TryGetUserAsync(userId, true).GetAwaiter().GetResult();

                if (dynUser == null)
                {
                    continue;
                }

                var longId = apiKey.Id.ToLongHashCode();

                var expiresAt = apiKey.ExpiryDate?.ToUnixTimestamp() ?? 0;

                if (expiresAt > 0 && expiresAt <= DateTimeHelper.UtcNowTs)
                {
                    continue;
                }

                var dynItemMapKey = new DynItemMap
                                    {
                                        Id = longId,
                                        EdgeId = DynItemMap.BuildEdgeId(DynItemType.Credential, apiKey.Id),
                                        ReferenceNumber = dynUser.UserId,
                                        MappedItemEdgeId = dynUser.EdgeId,
                                        ExpiresAt = expiresAt
                                    };

                var dynItemMapUser = new DynItemMap
                                     {
                                         Id = dynUser.UserId,
                                         EdgeId = DynItemMap.BuildEdgeId(DynItemType.Credential, string.Concat("apikey|", Sequences.Next())),
                                         ReferenceNumber = dynItemMapKey.Id,
                                         MappedItemEdgeId = dynItemMapKey.EdgeId,
                                         ExpiresAt = expiresAt
                                     };

                MapItemService.DefaultMapItemService.PutMap(dynItemMapKey);
                MapItemService.DefaultMapItemService.PutMap(dynItemMapUser);
            }
        }

        private DynUser GetDynUser(RydrUserSession rydrSession)
        {
            Guard.AgainstNullArgument(rydrSession == null, nameof(rydrSession));

            if (rydrSession.UserId > 0)
            {
                return _userService.TryGetUser(rydrSession.UserId, true);
            }

            if (!rydrSession.UserName.HasValue() && !rydrSession.UserAuthName.HasValue() && !rydrSession.Email.HasValue())
            {
                return null;
            }

            return _userService.GetUserByUserName(rydrSession.UserName.Coalesce(rydrSession.UserAuthName.Coalesce(rydrSession.Email)));
        }

        private DynUser GetDynUser(IUserAuth authUser)
        {
            if (authUser == null)
            {
                return null;
            }

            if (!(authUser is DynUser dynUser))
            {
                throw new InvalidApplicationStateException("Authorization user is invalid - code [rydraudui]");
            }

            if (dynUser.UserId > 0 && dynUser.UserName.HasValue() && dynUser.UserType != UserType.Unknown)
            {
                return dynUser;
            }

            dynUser = dynUser.UserId > 0
                          ? _userService.TryGetUser(dynUser.UserId, true)
                          : _userService.GetUserByUserName(dynUser.UserName);

            if (dynUser == null || dynUser.IsDeleted())
            {
                throw new InvalidApplicationStateException("Authorization user is invalid - code [rydrdynuau]");
            }

            return dynUser;
        }

        private RydrUserSession ToRydrUserSession(IAuthSession authSession)
        {
            if (authSession == null)
            {
                return null;
            }

            if (!(authSession is RydrUserSession rydrAuthSession))
            {
                throw new InvalidApplicationStateException("Authorization session is invalid - code [rasid-rti]");
            }

            if (rydrAuthSession.UserId > 0 && rydrAuthSession.RoleId > 0 && rydrAuthSession.UserType != UserType.Unknown)
            {
                return rydrAuthSession;
            }

            var dynUser = _userService.TryGetUser(rydrAuthSession.UserId.Gz(rydrAuthSession.UserAuthId.ToLong()), true);

            if (dynUser == null)
            {
                throw new InvalidApplicationStateException("Authorization session is invalid - code [rdynuaid]");
            }

            rydrAuthSession.PopulateSession(dynUser, this);

            rydrAuthSession.UserId = dynUser.UserId;
            rydrAuthSession.RoleId = dynUser.RoleId;
            rydrAuthSession.UserType = dynUser.UserType;
            rydrAuthSession.UserName = dynUser.UserName;
            rydrAuthSession.UserAuthName = dynUser.UserName;
            rydrAuthSession.Roles = GetRydrUserRoles(dynUser).AsList();

            return rydrAuthSession;
        }

        public async Task<DynUser> GetDynUserAsync(long userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            var dynUser = await _userService.TryGetUserAsync(userId, true);

            return dynUser == null || dynUser.IsDeleted()
                       ? null
                       : dynUser;
        }

        private DynUser GetDynUser(long userId)
        {
            if (userId <= 0)
            {
                return null;
            }

            var dynUser = _userService.TryGetUser(userId, true);

            return dynUser == null || dynUser.IsDeleted()
                       ? null
                       : dynUser;
        }

        public async Task<DynUser> GetDynUserByUserNameAsync(string userName)
        {
            if (!userName.HasValue())
            {
                return null;
            }

            var existingUser = await _userService.GetUserByUserNameAsync(userName);

            return existingUser;
        }

        private IEnumerable<string> GetRydrUserRoles(DynUser dynUser)
        {
            var yieldedType = false;
            var userType = dynUser?.UserType.ToString();

            if (!(dynUser?.Roles).IsNullOrEmpty())
            {
                foreach (var role in dynUser.Roles)
                {
                    if (string.Equals(role, userType, StringComparison.OrdinalIgnoreCase))
                    {
                        yieldedType = true;
                    }

                    yield return role;
                }
            }

            if (dynUser != null && userType != null && !yieldedType)
            {
                yield return userType;
            }
        }

        private Task SaveDynUserAsync(DynUser dynUser)
        {
            Guard.AgainstNullArgument(dynUser == null, nameof(dynUser));
            Guard.AgainstNullArgument(dynUser.UserId <= 0, "UserId");
            Guard.AgainstNullArgument(dynUser.UserType == UserType.Unknown, "UserType");
            Guard.AgainstNullArgument(!dynUser.UserName.HasValue(), "UserName");

            dynUser.ModifiedDate = DateTimeHelper.UtcNow;

            if (dynUser.CreatedDate <= DateTimeHelper.MinApplicationDate)
            {
                dynUser.CreatedDate = DateTimeHelper.UtcNow;
            }

            return _userService.UpdateUserAsync(dynUser);
        }
    }
}
