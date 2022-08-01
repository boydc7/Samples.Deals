using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth
{
    public class WorkspaceUserAuthorizer : IAuthorizer
    {
        private readonly IWorkspaceService _workspaceService;

        public WorkspaceUserAuthorizer(IWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
        }

        public bool CanExplicitlyAuthorize => true;
        public bool CanUnauthorize => true;

        public async Task<AuthorizerResult> VerifyAccessToAsync<T>(T toObject, IHasUserAuthorizationInfo state)
            where T : ICanBeAuthorized
        {
            if (state.UserId <= 0)
            {
                return AuthorizerResult.Unspecified;
            }

            // A request workspace id state value is not always required, which is why we do not fail on it here - if it is present however, the
            // user must have permission to it within their user context
            var hasAccessToReqWorkspace = state.WorkspaceId <= 0 ||
                                          await _workspaceService.UserHasAccessToWorkspaceAsync(state.WorkspaceId, state.UserId);

            if (!hasAccessToReqWorkspace)
            {
                // ReSharper disable once MethodHasAsyncOverload
                return AuthorizerResult.Unauthorized("You do not have access to the state or resource requested. Code [wuarwiuidnx]");
            }

            if ((toObject is IHasWorkspaceId workspaceIdObj) && workspaceIdObj.WorkspaceId > 0)
            {
                if (await _workspaceService.UserHasAccessToWorkspaceAsync(workspaceIdObj.WorkspaceId, state.UserId))
                { // Has access to this workspace, allow explicit reads in this case (we only opt-in to read-intent for a few endpoints, not
                    // all reads...so in those cases, a read intent with permission inside the workspace allows read operations
                    return AuthorizerResult.ExplicitRead;
                }

                // A user is trying to access something that is associated to a specific workspace that they do not have access to
                return new AuthorizerResult
                       {
                           FailLevel = AuthorizerFailLevel.FailUnlessExplicitlyAuthorized,
                           AuthException = new UnauthorizedException("You do not have access to the state or resource requested. Code [wuawidobjiux]")
                       };
            }

            return AuthorizerResult.Unspecified;
        }
    }
}
