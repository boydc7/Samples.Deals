using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IWorkspaceService
    {
        Task<List<long>> GetAllWorkspaceIdsAsync();
        long GetDefaultPublisherAppId(long workspaceId, PublisherType forPublisherType);
        DynWorkspace TryGetWorkspace(long workspaceId);
        Task<DynWorkspace> TryGetWorkspaceAsync(long workspaceId);
        Task<DynWorkspace> GetWorkspaceAsync(long workspaceId);
        Task<string> TryGetWorkspacePrimaryEmailAddressAsync(long workspaceId);
        IAsyncEnumerable<WorkspaceUser> GetWorkspaceUsersAsync(long workspaceId, int skip = 0);
        IAsyncEnumerable<DynWorkspace> GetUserWorkspacesAsync(long userId);
        IAsyncEnumerable<DynWorkspace> GetWorkspacesOwnedByAsync(long userId, WorkspaceType workspaceType = WorkspaceType.Unspecified);
        Task LinkTokenAccountAsync(DynWorkspace toDynWorkspace, DynPublisherAccount tokenPublisherAccount);
        Task DelinkTokenAccountAsync(DynWorkspace fromDynWorkspace, long tokenPublisherAccountId = 0);
        IAsyncEnumerable<long> GetAssociatedWorkspaceIdsAsync(DynPublisherAccount forPublisherAccount);
        Task<DynPublisherAccount> TryGetDefaultPublisherAccountAsync(long workspaceId);

        Task<DynWorkspace> CreateAsync(Workspace workspace, DynPublisherAccount defaultTokenAccount = null);

        Task<DynWorkspace> CreateAsync(long ownerId, string name, WorkspaceType type,
                                       PublisherType createdViaPublisherType = PublisherType.Unknown,
                                       string createdViaPublisherId = null, WorkspaceFeature features = WorkspaceFeature.Default);

        Task AssociateInviteCodeAsync(long workspaceId);

        Task LinkUserAsync(long workspaceId, long userId);
        Task DelinkUserAsync(long workspaceId, long userId);
        Task<long> GetWorkspaceUserIdAsync(long workspaceId, long userId);
        Task<WorkspaceRole> GetWorkspaceUserRoleAsync(long workspaceId, long userId);
        Task SetWorkspaceUserRoleAsync(long workspaceId, long userId, WorkspaceRole role);
        Task LinkUserToPublisherAccountAsync(long workspaceId, long userId, long publisherAccountId);
        Task DelinkUserFromPublisherAccountAsync(long workspaceId, long userId, long publisherAccountId);

        IAsyncEnumerable<DynPublisherAccount> GetWorkspaceUserPublisherAccountsAsync(long workspaceId, long userId, bool includeTokenAccounts = false);
        Task<DynPublisherAccount> TryGetWorkspaceUserPublisherAccountAsync(long workspaceId, long userId, long publisherAccountId);

        Task<bool> UserHasAccessToWorkspaceAsync(long workspaceId, long byUserId);
        Task<bool> UserHasAccessToAccountAsync(long workspaceId, long byUserId, long toPublisherAccountId);
        Task UpdateWorkspaceAsync(DynWorkspace workspace, Expression<Func<DynWorkspace>> put);
    }

    public static class WorkspaceService
    {
        public const string RydrSystemSubscriptionId = "RydrSystemSubscription";

        public static IWorkspaceService DefaultWorkspaceService { get; } = RydrEnvironment.Container.Resolve<IWorkspaceService>();
        public static IWorkspaceSubscriptionService DefaultWorkspaceSubscriptionService { get; } = RydrEnvironment.Container.Resolve<IWorkspaceSubscriptionService>();
        public static IWorkspacePublisherSubscriptionService DefaultWorkspacePublisherSubscriptionService { get; } = RydrEnvironment.Container.Resolve<IWorkspacePublisherSubscriptionService>();
    }
}
