using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Interfaces.Internal;

public interface IRequestState : IHasUserAuthorizationInfo
{
    string RequestId { get; set; }
    string IpAddress { get; set; }
    UserType UserType { get; set; }
    string HttpVerb { get; set; }
    AccessIntent Intent { get; set; }
    bool HasUserState();
    string ValidationMessage { get; set; }
    long ContextPublisherAccountId { get; set; }

    void MergeWith(IRequestState other);
}

public interface IRequestPreFlightState : IRequestState { }

public interface IRequestStateManager
{
    IRequestState GetState();

    void UpdateState(long userId, long workspaceId, long publisherAccountId, long roleId, IRequestBase requestToUpdate = null);

    void UpdateStateToSystemRequest();

    void UpdateStateIntent(AccessIntent toIntent);
}
