using System.Linq.Expressions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IWorkspaceSubscriptionService
{
    Task<DynWorkspaceSubscription> TryGetActiveWorkspaceSubscriptionAsync(long workspaceId);
    Task<DynWorkspaceSubscription> TryGetWorkspaceSubscriptionConsistentAsync(long workspaceId, string edgeId);
    Task<SubscriptionType> GetActiveWorkspaceSubscriptionTypeAsync(DynWorkspace workspace);
    Task PutWorkspaceSubscriptionAsync(DynWorkspaceSubscription source, bool isNew = false);
    Task UpdateWorkspaceSubscriptionAsync(DynWorkspaceSubscription workspaceSubscription, Expression<Func<DynWorkspaceSubscription>> put);
    Task DeleteWorkspaceSubscriptionAsync(DynWorkspaceSubscription existingSubscription);
    Task AddSystemSubscriptionAsync(long workspaceId, SubscriptionType subscriptionType);
    Task RemoveSystemSubscriptionAsync(long workspaceId);
    Task<bool> ChargeCompletedRequestUsageAsync(DynDealRequest forDealRequest, bool forceRecharge = false, long forceUsageTimestamp = 0);
}
