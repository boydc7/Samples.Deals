using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth
{
    public class WorkspaceUserPublisherAccountAuthorizer : IAuthorizer
    {
        private readonly IWorkspaceService _workspaceService;

        public WorkspaceUserPublisherAccountAuthorizer(IWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
        }

        public bool CanExplicitlyAuthorize => true;
        public bool CanUnauthorize => true;

        public async Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
            where T : ICanBeAuthorized
        {
            if (state.WorkspaceId <= 0 || state.UserId <= 0)
            {
                return AuthorizerResult.Unspecified;
            }

            // A request publisher account state value is not always required, which is why we do not fail on it here - if it is present however, the
            // user must have permission to it within the workspace context as well
            var hasAccessToReqPub = state.RequestPublisherAccountId <= 0 ||
                                    await _workspaceService.UserHasAccessToAccountAsync(state.WorkspaceId, state.UserId, state.RequestPublisherAccountId);

            if (!hasAccessToReqPub)
            {
                // ReSharper disable once MethodHasAsyncOverload
                return AuthorizerResult.Unauthorized("You do not have access to the state or resource requested. Code [wparpiux]");
            }

            if ((toObject is IHasPublisherAccountId publisherAccountIdObj) && publisherAccountIdObj.PublisherAccountId > 0)
            {
                if (await _workspaceService.UserHasAccessToAccountAsync(state.WorkspaceId, state.UserId, publisherAccountIdObj.PublisherAccountId))
                { // Has access to this publisher account inside the workspace context, allow explicit reads in this case (we only opt-in to read-intent
                    // for a few endpoints, not all reads...so in those cases, a read intent with permission inside the workspace allows read operations
                    return AuthorizerResult.ExplicitRead;
                }

                // Use of a pub account in the header has to be valid, however access to a particular object can be explicitly allowed/authorized in other manners
                // even when a user inside the workspace doesn't have direct access
                return new AuthorizerResult
                       {
                           FailLevel = AuthorizerFailLevel.FailUnlessExplicitlyAuthorized,
                           AuthException = new UnauthorizedException("You do not have access to the state or resource requested. Code [wpapobjiux]")
                       };
            }

            return AuthorizerResult.Unspecified;
        }
    }
}
