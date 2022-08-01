using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IWorkspacePublisherSubscriptionService
    {
        Task<DynWorkspacePublisherSubscription> GetManagedPublisherSubscriptionAsync(string subscriptionId);
        Task<DynWorkspacePublisherSubscription> GetPublisherSubscriptionAsync(long workspaceId, long publisherAccountId);
        Task<DynWorkspacePublisherSubscription> TryGetPublisherSubscriptionConsistentAsync(long workspaceId, string edgeId);
        Task<SubscriptionType> GetPublisherSubscriptionTypeAsync(long workspaceId, long publisherAccountId);
        Task PutPublisherSubscriptionAsync(DynWorkspacePublisherSubscription source);
        Task PutPublisherSubscriptionsAsync(IEnumerable<DynWorkspacePublisherSubscription> source);
        Task DeletePublisherSubscriptionAsync(DynWorkspacePublisherSubscription source);
        Task CancelSubscriptionAsync(long workspaceId, long publisherAccountId);
        Task AddSubscribedPayPerBusinessPublisherAccountsAsync(long workspaceId, IReadOnlyList<long> publisherAccountIds);
        Task<bool> AddManagedSubscriptionAsync(long workspaceId, DynPublisherAccount publisherAccount, SubscriptionType subscriptionType,
                                               string stripeCustomerId, string newStripeSubscriptionId = null,
                                               DateTime? backdateTo = null, string rydrEmployeeSig = null, string rydrEmployeeLogin = null,
                                               double customMonthlyFee = 0, double customPerPostFee = 0);
    }
}
