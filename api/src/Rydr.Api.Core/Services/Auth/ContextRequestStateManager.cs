using System;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Services.Auth
{
    public class ContextRequestStateManager : IRequestStateManager
    {
        private readonly Func<IRequestPreFlightState> _requestPreFlightStateFactory;

        public ContextRequestStateManager(Func<IRequestPreFlightState> requestPreFlightStateFactory)
        {
            _requestPreFlightStateFactory = requestPreFlightStateFactory;
        }

        public IRequestState GetState()
        {
            var stackState = ContextStack.CurrentState;

            var state = stackState.HasUserState()
                            ? stackState
                            : _requestPreFlightStateFactory();

            return state;
        }

        public void UpdateStateIntent(AccessIntent toIntent)
        {
            var state = GetState();

            state.Intent = toIntent;
        }

        public void UpdateStateToSystemRequest()
        {
            var state = GetState();

            state.IsSystemRequest = true;

            if (state.RoleId <= 0)
            {
                state.RoleId = UserAuthInfo.AdminUserId;
            }
        }

        public void UpdateState(long userId, long workspaceId, long publisherAccountId, long roleId, IRequestBase requestToUpdate = null)
        {
            var state = GetState();

            Guard.AgainstUnauthorized(state == null ||
                                      ((state.UserId > 0 || state.WorkspaceId > 0 || state.RequestPublisherAccountId > 0)
                                       &&
                                       requestToUpdate == null),
                                      "RequestState is in a valid state, cannot be updated");

            if (userId > 0)
            {
                state.UserId = userId;
            }

            if (workspaceId > 0)
            {
                state.WorkspaceId = workspaceId;
            }

            if (publisherAccountId > 0)
            {
                state.RequestPublisherAccountId = publisherAccountId;
            }

            if (roleId > 0)
            {
                state.RoleId = roleId;
            }

            if (!state.HasUserState() || requestToUpdate == null)
            {
                return;
            }

            requestToUpdate.UserId = state.UserId;
            requestToUpdate.WorkspaceId = state.WorkspaceId;
            requestToUpdate.RequestPublisherAccountId = state.RequestPublisherAccountId;
            requestToUpdate.RoleId = state.RoleId;
        }
    }
}
