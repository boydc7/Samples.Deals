using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth;

public class RequestStateAccessIntentMatchAuthorizer : IAuthorizer
{
    private readonly IRequestStateManager _requestStateManager;

    public RequestStateAccessIntentMatchAuthorizer(IRequestStateManager requestStateManager)
    {
        _requestStateManager = requestStateManager;
    }

    public bool CanExplicitlyAuthorize => true;
    public bool CanUnauthorize => false;

    public Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
        where T : ICanBeAuthorized
    {
        if (!(state is IRequestState requestState))
        {
            requestState = _requestStateManager.GetState();
        }

        if (requestState.IsSystemRequest || requestState.UserType == UserType.Admin)
        { // Admin...
            return AuthorizerResult.ExplicitlyAuthorizedAsync;
        }

        if (requestState.Intent == AccessIntent.Write)
        {
            return AuthorizerResult.UnspecifiedAsync;
        }

        if (toObject.WorkspaceId != UserAuthInfo.PublicWorkspaceId &&
            toObject.OwnerId != UserAuthInfo.PublicOwnerId &&
            !toObject.IsPubliclyReadable())
        {
            return AuthorizerResult.UnspecifiedAsync;
        }

        // Valid as the object is publicly available so long as we are not in an update/delete situation - explicit read is available
        return AuthorizerResult.ExplicitReadAsync;
    }
}
