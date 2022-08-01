using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Models.Supporting
{
    public class UserAuthInfo : IHasUserAuthorizationInfo
    {
        private bool _isSystemRequest;

        public UserAuthInfo() { }

        public UserAuthInfo(long userId, long workspaceId, long publisherAccountId, long roleId = 0)
        {
            UserId = userId;
            WorkspaceId = workspaceId;
            RequestPublisherAccountId = publisherAccountId;
            RoleId = roleId;
        }

        public long UserId { get; set; }
        public long WorkspaceId { get; set; }
        public long RequestPublisherAccountId { get; set; }
        public long RoleId { get; set; }

        public bool IsSystemRequest
        {
            get => _isSystemRequest || UserId == AdminUserId || WorkspaceId == AdminWorkspaceId || RoleId == AdminUserId;
            set => _isSystemRequest = value;
        }

        public static UserAuthInfo AdminAuthInfo { get; } = new UserAuthInfo(AdminUserId, AdminWorkspaceId, 0, AdminUserId)
                                                            {
                                                                IsSystemRequest = true
                                                            };

        public const long AdminUserId = GlobalItemIds.AuthAdminUserId;
        public const long AdminWorkspaceId = GlobalItemIds.AuthAdminWorkspaceId;
        public const long RydrWorkspaceId = GlobalItemIds.AuthRydrWorkspaceId;
        public const long PublicWorkspaceId = GlobalItemIds.PublicWorkspaceId;
        public const long PublicOwnerId = GlobalItemIds.PublicOwnerId;
    }
}
