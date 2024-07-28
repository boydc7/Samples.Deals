using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Auth;
using Rydr.Api.Dto.Enums;
using ServiceStack;

namespace Rydr.Api.Core.Extensions;

public static class UserExtensions
{
    public static readonly IUserService DefaultUserService = RydrEnvironment.Container.Resolve<IUserService>();

    public static User ToUser(this DynUser source)
    {
        var to = source.ConvertTo<User>();

        to.Id = source.UserId;
        to.AuthPublisherType = source.LastAuthPublisherType;

        if (to.FullName.IsNullOrEmpty())
        {
            to.FullName = source.FullName.Coalesce(source.Email).Coalesce(source.UserName);
        }

        return to;
    }

    public static string FullName(this DynUser source)
        => string.Concat(source.FirstName, " ", source.LastName).Trim();

    public static IEnumerable<RydrUser> ToRydrUsers(this DynUser source)
    {
        var to = source.ConvertTo<RydrUser>();

        to.Email = source.Email ?? "";
        to.UserName = source.UserName;
        to.Id = source.UserId;
        to.AuthProviderUid = source.AuthProviderUid.Coalesce(string.Concat("rydr_", source.UserName));
        to.LastAuthPublisherType = source.LastAuthPublisherType;

        yield return to;
    }

    public static WorkspaceUser ToWorkspaceUser(this DynUser user, WorkspaceRole workspaceRole)
        => new()
           {
               UserId = user.UserId,
               Name = user.DisplayName.Coalesce(user.FullName),
               UserName = user.AuthProviderUserName.Coalesce(user.UserName),
               UserEmail = user.Email,
               Avatar = user.Avatar,
               WorkspaceRole = workspaceRole
           };
}
