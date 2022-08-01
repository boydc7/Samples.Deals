using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Configuration;

namespace Rydr.Api.Core.Models.Internal
{
    public class RydrUserSession : AuthUserSession
    {
        public long UserId { get; set; }
        public long RoleId { get; set; }
        public UserType UserType { get; set; }

        public bool IsAdmin => IsRydrAdmin ||
                               (!Roles.IsNullOrEmpty() && Roles.Contains(RoleNames.Admin));

        public bool IsRydrAdmin => UserType == UserType.Admin;
    }
}
