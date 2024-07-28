using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.Web;

namespace Rydr.Api.Services.Filters;

public class RequiresRequestStateAttribute : RequestFilterAttribute
{
    private static readonly Func<IRequestPreFlightState> _requestPreStateFactory = RydrEnvironment.Container.LazyResolve<IRequestPreFlightState>();

    public RequiresRequestStateAttribute()
    {
        // After authentication and role requirement, but before other filters
        Priority = -10;
    }

    public override void Execute(IRequest req, IResponse res, object requestDto)
    {
        if (!(requestDto is IRequestBase dto))
        {
            return;
        }

        RydrUserSession rydrUserSession = null;
        var isInternalOrAdminRequest = req.IsRydrRequest();

        if (!isInternalOrAdminRequest)
        {
            var session = req.GetSession();

            if (session != null)
            {
                if ((session is RydrUserSession rus))
                {
                    rydrUserSession = rus;
                    isInternalOrAdminRequest = rydrUserSession.IsAdmin;
                }

                if (!isInternalOrAdminRequest &&
                    (session.UserName.EqualsOrdinalCi(Keywords.AuthSecret) ||
                     (!session.Roles.IsNullOrEmpty() && session.Roles.Contains(RoleNames.Admin))))
                {
                    isInternalOrAdminRequest = true;
                }
            }
        }

        if (isInternalOrAdminRequest)
        {
            req.Items[Keywords.AuthSecret] = RydrEnvironment.AdminKey;
        }

        dto.UserId = isInternalOrAdminRequest && dto.UserId > 0
                         ? dto.UserId
                         : (rydrUserSession?.UserId).Nz(isInternalOrAdminRequest
                                                            ? UserAuthInfo.AdminUserId
                                                            : -1);

        var workspaceIdHeader = req.GetHeader(WebServiceExtensions.RydrWorkspaceIdName);

        dto.WorkspaceId = isInternalOrAdminRequest && dto.WorkspaceId > 0
                              ? dto.WorkspaceId
                              : workspaceIdHeader.ToLong(0).Gz(-1);

        var publisherAccountIdHeader = req.GetHeader(WebServiceExtensions.RydrPublisherAccountIdHeaderName);

        dto.RequestPublisherAccountId = isInternalOrAdminRequest && dto.RequestPublisherAccountId > 0
                                            ? dto.RequestPublisherAccountId
                                            : publisherAccountIdHeader.ToLong(0).Gz(-1);

        dto.RoleId = isInternalOrAdminRequest && dto.RoleId > 0
                         ? dto.RoleId
                         : (rydrUserSession?.RoleId).Nz(isInternalOrAdminRequest
                                                            ? UserAuthInfo.AdminUserId
                                                            : -1);

        dto.IsSystemRequest = isInternalOrAdminRequest;

        var preState = _requestPreStateFactory();

        if (preState.UserId <= 0 || preState.WorkspaceId <= 0 || preState.RequestPublisherAccountId <= 0)
        {
            preState.UserId = dto.UserId;
            preState.WorkspaceId = dto.WorkspaceId;
            preState.RequestPublisherAccountId = dto.RequestPublisherAccountId;
            preState.RoleId = dto.RoleId;
            preState.IsSystemRequest = isInternalOrAdminRequest;
            preState.UserType = rydrUserSession?.UserType ?? UserType.Unknown;
        }

        if (preState.IpAddress == null)
        {
            preState.IpAddress = req.RemoteIp.Coalesce(req.UserHostAddress) ?? string.Empty;
        }
    }
}
