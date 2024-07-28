using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth;

public class RequestUserAuthInfoMatchAuthorizer : IAuthorizer
{
    public bool CanExplicitlyAuthorize => true;
    public bool CanUnauthorize => true;

    public Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
        where T : ICanBeAuthorized
    {
        if (state.UserId == UserAuthInfo.AdminUserId || state.WorkspaceId == UserAuthInfo.AdminWorkspaceId ||
            state.RoleId == UserAuthInfo.AdminWorkspaceId || state.RoleId == UserAuthInfo.AdminUserId)
        { // Admin
            return AuthorizerResult.ExplicitlyAuthorizedAsync;
        }

        if (toObject.CreatedBy <= 0 ||
            (state.UserId <= 0 && state.RoleId <= 0 && state.WorkspaceId <= 0 && state.RequestPublisherAccountId <= 0))
        {
            return AuthorizerResult.UnauthorizedAsync("You do not have access to the resource requested. Code [racidzro]");
        }

        if (state.UserId == toObject.CreatedBy || state.UserId == toObject.ModifiedBy ||
            state.UserId == toObject.OwnerId || state.RoleId == toObject.CreatedBy ||
            state.RoleId == toObject.ModifiedBy || state.RoleId == toObject.OwnerId ||
            state.RoleId == toObject.OwnerId)
        { // Valid match by user or workspace or role to created/modified/workspace/owner
            return AuthorizerResult.ExplicitlyAuthorizedAsync;
        }

        if (state.WorkspaceId == toObject.WorkspaceId || state.WorkspaceId == toObject.OwnerId ||
            state.RoleId == toObject.WorkspaceId || state.WorkspaceId == toObject.CreatedWorkspaceId ||
            state.WorkspaceId == toObject.ModifiedWorkspaceId || state.RoleId == toObject.CreatedWorkspaceId ||
            state.RoleId == toObject.ModifiedWorkspaceId)
        { // Workspace match is a general unspecified, soft-grant of access pending nothing else failing...
            return AuthorizerResult.ExplicitReadAsync;
        }

        // Everything else is a fail unless explicitly authorized some other way
        return Task.FromResult(new AuthorizerResult
                               {
                                   FailLevel = AuthorizerFailLevel.FailUnlessExplicitlyAuthorized,
                                   AuthException = new UnauthorizedException($"You do not have access to the resource requested. Code [{state.WorkspaceId}-{toObject.WorkspaceId}||{state.UserId}-{toObject.CreatedBy}]")
                               });
    }
}
