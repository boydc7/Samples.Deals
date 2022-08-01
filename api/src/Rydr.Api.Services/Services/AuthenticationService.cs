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
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Auth;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Auth;

namespace Rydr.Api.Services.Services
{
    [RydrNeverCacheResponse]
    [Restrict(VisibleLocalhostOnly = true)]
    public class AuthenticationPublicService : BaseApiService
    {
        private readonly IRydrUserAuthRepository _rydrUserAuthRepository;
        private readonly IUserService _userService;
        private readonly IDeferRequestsService _deferRequestsService;

        public AuthenticationPublicService(IRydrUserAuthRepository rydrUserAuthRepository,
                                           IUserService userService,
                                           IDeferRequestsService deferRequestsService)
        {
            _rydrUserAuthRepository = rydrUserAuthRepository;
            _userService = userService;
            _deferRequestsService = deferRequestsService;
        }

        public async Task<OnlyResultResponse<User>> Post(PostUser request)
        {
            var user = await RegisterUserAsync(request);

            return user.ToUser().AsOnlyResultResponse();
        }

        // NOTE: THIS IS HERE FOR BACKWARD COMPATIBILITY ONLY...NOW USE PostUser plus
        public async Task<OnlyResultResponse<ConnectedApiInfo>> Post(PostAuthenticationConnect request)
        {
            var registeredUser = await RegisterUserAsync(new PostUser
                                                         {
                                                             FirebaseId = request.FirebaseId,
                                                             FirebaseToken = request.FirebaseToken,
                                                             Name = request.Name,
                                                             Avatar = request.Avatar,
                                                             Email = request.Email,
                                                             Username = request.Username,
                                                             Phone = request.Phone,
                                                             IsEmailVerified = request.IsEmailVerified,
                                                             AuthProvider = "facebook.com"
                                                         });

            var connectUser = new PostFacebookConnectUser
                              {
                                  AuthToken = request.AuthProviderToken,
                                  AccountId = request.AuthProviderId,
                                  UserName = request.Username
                              }.PopulateWithRequestInfo(request);

            connectUser.UserId = registeredUser.UserId;

            var userInfoResponse = await _adminServiceGatewayFactory().SendAsync(connectUser);

            return userInfoResponse;
        }

        private async Task<DynUser> RegisterUserAsync(PostUser request)
        {
            Guard.AgainstUnauthorized(request.FirebaseId.IsNullOrEmpty() || request.FirebaseToken.IsNullOrEmpty(), "Invalid auth connect state - code [ruafidftknmn]");

            // Username in our system is the email if we have one, firebaseId otherwise.  However we cannot guarantee that the client will always send an email
            // across various requests even for the same firebaseId. That plus a single user can login with multiple auth providers, and hence have multiple
            // Firebase users, which we try to recognize as the same user when possible (i.e. if they all have the same email). So, to determine if a user already
            // exists, we have to check for both to map to an existing user if needed...

            var email = request.Email?.ToLowerInvariant().ToNullIfEmpty();

            // Get the existing user - check by FirebaseId first, as that is always available/required on this endpoint, and is definitley unique. But we have to support
            // a non-firebase userName method as well (for non-firebase users...admins, internal, etc.)...so check for email secondarily
            var dynUser = await _userService.GetUserByAuthUidAsync(request.FirebaseId)
                          ??
                          await _userService.GetUserByUserNameAsync(request.FirebaseId)
                          ??
                          (email.HasValue()
                               ? await _userService.GetUserByUserNameAsync(email)
                               : null);

            var userExisted = dynUser != null;

            // If creating a NEW user, prefer to use email as the user identifier if available, soas to allow future recognition of different firebase accounts being
            // the same RYDR user...
            var rydrUserName = (dynUser?.UserName).Coalesce(email).Coalesce(request.FirebaseId);

            try
            {
                var authPublisherType = request.AuthProvider.IsNullOrEmpty()
                                            ? PublisherType.Unknown
                                            : RydrTypeEnumHelpers.AuthProviderToPublisherType(request.AuthProvider);

                dynUser ??= await _rydrUserAuthRepository.CreateUserAuthAsync(new DynUser
                                                                              {
                                                                                  FullName = request.Name.ToNullIfEmpty(),
                                                                                  FirstName = request.Name.HasValue()
                                                                                                  ? request.Name.SafeSubstring(0, request.Name.IndexOf(' '))
                                                                                                  : null,
                                                                                  LastName = request.Name.HasValue()
                                                                                                 ? request.Name.SafeSubstring(request.Name.IndexOf(' '))
                                                                                                 : null,
                                                                                  Avatar = request.Avatar,
                                                                                  UserName = rydrUserName,
                                                                                  Email = email,
                                                                                  AuthProviderUserName = request.Username,
                                                                                  AuthProviderUid = request.FirebaseId,
                                                                                  UserType = UserType.User,
                                                                                  PhoneNumber = request.Phone,
                                                                                  IsEmailVerified = request.IsEmailVerified,
                                                                                  LastAuthPublisherType = authPublisherType
                                                                              });

                Guard.AgainstUnauthorized(dynUser == null, "Invalid auth connect state - code[rpacdun]");

                // Update the user model if needed
                if (userExisted)
                {
                    dynUser.DeletedBy = null;
                    dynUser.DeletedOnUtc = null;
                    dynUser.DeletedByWorkspaceId = null;
                    dynUser.AuthProviderUid = request.FirebaseId;
                    dynUser.FullName = request.Name.Coalesce(dynUser.FullName);

                    dynUser.FirstName = (request.Name.HasValue()
                                             ? request.Name.SafeSubstring(0, request.Name.IndexOf(' '))
                                             : null).Coalesce(dynUser.FirstName);

                    dynUser.LastName = (request.Name.HasValue()
                                            ? request.Name.SafeSubstring(request.Name.IndexOf(' '))
                                            : null).Coalesce(dynUser.LastName);

                    dynUser.LastAuthPublisherType = authPublisherType;
                    dynUser.ModifiedDate = DateTimeHelper.UtcNow;
                    dynUser.PhoneNumber = request.Phone.Coalesce(dynUser.PhoneNumber);
                    dynUser.Avatar = request.Avatar.Coalesce(dynUser.Avatar);

                    var userWithNewEmail = email.HasValue() && !email.EqualsOrdinalCi(dynUser.Email);

                    // Same user (matching authUid's) but changing email addresses - should happen infrequently, but when it does need to re-create the user
                    // record in dynamo to use the new email as the edge...
                    if (userWithNewEmail)
                    {
                        var existingEdgeId = dynUser.EdgeId;

                        await _dynamoDb.DeleteItemAsync<DynUser>(dynUser.Id, existingEdgeId);

                        dynUser.Email = email;
                        dynUser.UserName = email;
                    }

                    await _userService.UpdateUserAsync(dynUser);
                }

                await _userService.LinkAuthUidToUserAsync(request.FirebaseId, request.FirebaseToken, dynUser);

                if (dynUser.Email.HasValue())
                {
                    _deferRequestsService.DeferRequest(new PostExternalCrmContactUpdate
                                                       {
                                                           UserEmail = dynUser.Email,
                                                           Items = new List<ExternalCrmUpdateItem>
                                                                   {
                                                                       new ExternalCrmUpdateItem
                                                                       {
                                                                           FieldName = "LastAuthConnectDate",
                                                                           FieldValue = DateTimeHelper.UtcNow.Date.ToSqlDateString()
                                                                       }
                                                                   }
                                                       }.WithAdminRequestInfo());
                }
            }
            catch
            {
                if (!userExisted)
                {
                    await _userService.DeleteUserAsync(dynUser, hardDelete: true, authUid: request.FirebaseId);
                }

                throw;
            }

            return dynUser;
        }
    }

    [RydrNeverCacheResponse]
    public class AuthenticationService : BaseAuthenticatedApiService
    {
        private readonly IUserService _userService;

        public AuthenticationService(IUserService userService)
        {
            _userService = userService;
        }

        public async Task<OnlyResultResponse<User>> Get(GetAuthenticationUser request)
        {
            var userId = request.GetUserIdFromIdentifier();

            var dynUser = await _userService.GetUserAsync(userId);

            return dynUser.ToUser().AsOnlyResultResponse();
        }

        public async Task<OnlyResultsResponse<ConnectedApiInfo>> Get(GetAuthenticationConnectInfo request)
        {
            var userId = request.UserIdentifier.ToLong(request.UserId);

            var connectInfos = new List<ConnectedApiInfo>();

            await foreach (var workspace in WorkspaceService.DefaultWorkspaceService
                                                            .GetUserWorkspacesAsync(userId))
            {
                var owner = await _userService.TryGetUserAsync(workspace.OwnerId);

                connectInfos.Add(new ConnectedApiInfo
                                 {
                                     OwnerEmail = owner?.Email,
                                     OwnerName = owner?.FullName(),
                                     OwnerUserId = workspace.OwnerId,
                                     OwnerUserName = owner.UserName,
                                     OwnerAuthProviderId = owner.AuthProviderUid,
                                     WorkspaceId = workspace.Id,
                                     WorkspaceName = workspace.Name,
                                     DefaultPublisherAccount = workspace.DefaultPublisherAccountId > 0
                                                                   ? (await PublisherExtensions.DefaultPublisherAccountService
                                                                                               .TryGetPublisherAccountAsync(workspace.DefaultPublisherAccountId)
                                                                     ).ToPublisherAccount()
                                                                   : null
                                 });
            }

            return connectInfos.AsOnlyResultsResponse();
        }
    }

    public class AuthenticationAdminService : BaseAdminApiService
    {
        private readonly IRydrUserAuthRepository _userAuthRepository;
        private readonly IClientTokenAuthorizationService _clientTokenAuthorizationService;
        private readonly IUserService _userService;
        private readonly IRydrDataService _rydrDataService;

        public AuthenticationAdminService(IRydrUserAuthRepository rydrUserAuthRepository,
                                          IClientTokenAuthorizationService clientTokenAuthorizationService,
                                          IUserService userService,
                                          IRydrDataService rydrDataService)
        {
            _userAuthRepository = rydrUserAuthRepository;
            _clientTokenAuthorizationService = clientTokenAuthorizationService;
            _userService = userService;
            _rydrDataService = rydrDataService;
        }

        public async Task<OnlyResultResponse<ConnectedApiInfo>> Get(GetAuthenticationToken request)
        {
            var dynUser = await _userService.TryGetUserAsync(request.ForUserId);
            var apiToken = _clientTokenAuthorizationService.GetTempClientToken(dynUser.UserId);

            var personalWorkspace = await WorkspaceService.DefaultWorkspaceService.TryGetPersonalWorkspaceAsync(dynUser.UserId);

            var response = new ConnectedApiInfo
                           {
                               OwnerUserId = dynUser.UserId,
                               ApiKey = apiToken,
                               ApiKeyUserId = dynUser.UserId,
                               OwnerName = dynUser.FullName(),
                               OwnerEmail = dynUser.Email,
                               OwnerUserName = dynUser.UserName,
                               OwnerAuthProviderId = dynUser.AuthProviderUid,
                               WorkspaceId = personalWorkspace?.Id ?? 0,
                               WorkspaceName = personalWorkspace?.Name
                           };

            return response.AsOnlyResultResponse();
        }

        public async Task<OnlyResultResponse<GetAuthenticationPublisherInfoResponse>> Get(GetAuthenticationPublisherInfo request)
        {
            var response = new GetAuthenticationPublisherInfoResponse();

            // Find the workspace that was created out of this publisher
            var dynPublisherAccount = request.PublisherAccountId > 0
                                          ? await PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(request.PublisherAccountId)
                                          : await PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(request.PublisherType, request.PublisherId);

            if (dynPublisherAccount == null)
            {
                return response.AsOnlyResultResponse();
            }

            response.PublisherAccount = dynPublisherAccount.ToPublisherAccount();

            var dynWorkspace = await WorkspaceService.DefaultWorkspaceService
                                                     .TryGetWorkspaceAsync(request.InWorkspaceId.Gz(dynPublisherAccount.WorkspaceId).Gz(dynPublisherAccount.CreatedWorkspaceId));

            if (dynWorkspace == null)
            {
                return response.AsOnlyResultResponse();
            }

            response.ConnectInfo = new ConnectedApiInfo
                                   {
                                       WorkspaceId = dynWorkspace.Id,
                                       WorkspaceName = dynWorkspace.Name
                                   };

            if (dynWorkspace.DefaultPublisherAccountId > 0)
            {
                var defaultPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                       .TryGetPublisherAccountAsync(dynWorkspace.DefaultPublisherAccountId);

                response.ConnectInfo.DefaultPublisherAccount = defaultPublisherAccount?.ToPublisherAccount();
            }

            var dynWorkspaceOwner = await _userAuthRepository.GetDynUserAsync(dynWorkspace.OwnerId);

            if (dynWorkspaceOwner == null)
            {
                return response.AsOnlyResultResponse();
            }

            response.ConnectInfo.OwnerUserId = dynWorkspaceOwner.UserId;
            response.ConnectInfo.OwnerName = dynWorkspaceOwner.FullName();
            response.ConnectInfo.OwnerEmail = dynWorkspaceOwner.Email;
            response.ConnectInfo.OwnerUserName = dynWorkspaceOwner.UserName;

            DynUser dynTokenUser = null;

            if (dynPublisherAccount.IsTokenAccount())
            { // Get the personal workspace that the given token account was used to create
                var personalSpace = await _rydrDataService.TrySingleAsync<RydrWorkspace>(w => w.CreatedViaPublisherId == dynPublisherAccount.AccountId &&
                                                                                              w.WorkspaceType == WorkspaceType.Personal &&
                                                                                              w.OwnerId > 0);

                dynTokenUser = await _userService.TryGetUserAsync(personalSpace?.OwnerId ?? 0);
            }

            if (dynTokenUser == null)
            {
                dynTokenUser = dynWorkspaceOwner;
            }
            else if (dynTokenUser.Id != dynWorkspaceOwner.Id)
            { // Token user is not the workspace owner, so return a WorkspacePublisherAccount for context reference into the workspace
                var workspacePublisherAccount = await PublisherExtensions.DefaultPublisherAccountService
                                                                         .TryGetPublisherAccountAsync(request.WorkspacePublisherAccountId)
                                                ??
                                                await WorkspaceService.DefaultWorkspaceService
                                                                      .GetWorkspaceUserPublisherAccountsAsync(dynWorkspace.Id, dynTokenUser.Id)
                                                                      .FirstOrDefaultAsync();

                response.WorkspacePublisherAccount = workspacePublisherAccount?.ToPublisherAccount();
            }

            response.ConnectInfo.ApiKeyUserId = dynTokenUser.UserId;
            response.ConnectInfo.ApiKey = _userAuthRepository.GetUserApiKeys(dynTokenUser.UserId.ToStringInvariant()).FirstOrDefault(a => a.Id.HasValue())?.Id;

            if (request.IncludeTempToken && response.ConnectInfo.ApiKey.IsNullOrEmpty())
            {
                response.ConnectInfo.ApiKey = _clientTokenAuthorizationService.GetTempClientToken(dynTokenUser.Id);
            }

            return response.AsOnlyResultResponse();
        }

        public async Task<SetupNewAdminResponse> Post(PostApiKey request)
        {
            if (request.ApiKey.HasValue() && _userAuthRepository.ApiKeyExists(request.ApiKey))
            {
                throw new DuplicateRecordException("Invalid request - code [snaakipx]");
            }

            if (request.Email.HasValue())
            {
                var dynUser = await _userService.GetUserByUserNameAsync(request.Email)
                              ??
                              await _userAuthRepository.CreateUserAuthAsync(new DynUser
                                                                            {
                                                                                FullName = request.Name.ToNullIfEmpty(),
                                                                                FirstName = request.Name.HasValue()
                                                                                                ? request.Name.SafeSubstring(0, request.Name.IndexOf(' '))
                                                                                                : null,
                                                                                LastName = request.Name.HasValue()
                                                                                               ? request.Name.SafeSubstring(request.Name.IndexOf(' '))
                                                                                               : null,
                                                                                UserName = request.Email,
                                                                                Email = request.Email,
                                                                                UserType = UserType.User,
                                                                            });

                request.ToUserId = dynUser.UserId;
            }

            var authProvider = (ApiKeyAuthProvider)AuthenticateService.GetAuthProvider(ApiKeyAuthProvider.Name);

            var apiKey = request.ApiKey.HasValue()
                             ? request.ApiKey
                             : authProvider.GenerateNewApiKeys(Guid.NewGuid().ToString())
                                           .First(a => a.Id.HasValue())
                                           .Id;

            _userAuthRepository.StoreAll(new ApiKey
                                         {
                                             Id = apiKey,
                                             UserAuthId = request.ToUserId.ToStringInvariant()
                                         }.AsEnumerable());

            return new SetupNewAdminResponse
                   {
                       UserId = request.ToUserId,
                       ApiKey = apiKey
                   };
        }

        public async Task Put(PutUpdateUserType request)
        {
            var user = await _userService.GetUserAsync(request.ForUserId);

            if (user.UserType == request.ToUserType)
            {
                return;
            }

            user.UserType = request.ToUserType;

            await _userService.UpdateUserAsync(user);
        }
    }
}
