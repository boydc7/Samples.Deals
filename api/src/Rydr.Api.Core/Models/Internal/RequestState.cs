using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;

namespace Rydr.Api.Core.Models.Internal;

public class RequestState : IRequestPreFlightState
{
    public string RequestId { get; set; } = Guid.NewGuid().ToStringId();
    public long UserId { get; set; }
    public long WorkspaceId { get; set; }
    public long RoleId { get; set; }
    public long RequestPublisherAccountId { get; set; }
    public UserType UserType { get; set; }
    public string IpAddress { get; set; }
    public string HttpVerb { get; set; }
    public bool IsSystemRequest { get; set; }
    public AccessIntent Intent { get; set; }
    public string ValidationMessage { get; set; }

    public long ContextPublisherAccountId { get; set; }

    public bool HasUserState() => UserId > 0;

    public void MergeWith(IRequestState other)
    {
        if (other == null)
        {
            return;
        }

        if (other.RequestId.HasValue())
        {
            RequestId = other.RequestId;
        }

        UserId = UserId.Gz(other.UserId);
        WorkspaceId = WorkspaceId.Gz(other.WorkspaceId);
        RoleId = RoleId.Gz(other.RoleId);
        RequestPublisherAccountId = RequestPublisherAccountId.Gz(other.RequestPublisherAccountId);
        IpAddress = IpAddress.Coalesce(other.IpAddress);
        HttpVerb = HttpVerb.Coalesce(other.HttpVerb);
        IsSystemRequest = IsSystemRequest || other.IsSystemRequest;
        ContextPublisherAccountId = ContextPublisherAccountId.Gz(other.ContextPublisherAccountId);

        if ((int)other.UserType > (int)UserType)
        {
            UserType = other.UserType;
        }
    }
}
