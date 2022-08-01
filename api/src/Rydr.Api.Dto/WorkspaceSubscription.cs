using System;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto
{
    [Route("/workspaces/{id}/subscriptions/active", "GET")]
    public class GetActiveWorkspaceSubscription : BaseGetRequest<WorkspaceSubscription> { }

    // INTERNAL requests

    [Route("/internal/subscriptions/deleted", "POST")]
    public class WorkspaceSubscriptionDeleted : RequestBase, IReturnVoid, IPost
    {
        public long SubscriptionWorkspaceId { get; set; }
        public string SubscriptionId { get; set; }
    }

    [Route("/internal/publishersubscriptions/deleted", "POST")]
    public class WorkspacePublisherSubscriptionDeleted : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
    {
        public long SubscriptionWorkspaceId { get; set; }
        public long PublisherAccountId { get; set; }
        public long DynWorkspaceSubscriptionId { get; set; }
    }

    [Route("/internal/subscriptions/updated", "POST")]
    public class WorkspaceSubscriptionUpdated : RequestBase, IReturnVoid, IPost
    {
        public long SubscriptionWorkspaceId { get; set; }
        public string SubscriptionId { get; set; }
    }

    [Route("/internal/subscriptions/created", "POST")]
    public class WorkspaceSubscriptionCreated : RequestBase, IReturnVoid, IPost
    {
        public long SubscriptionWorkspaceId { get; set; }
        public string SubscriptionId { get; set; }
    }

    [Route("/internal/subscriptions/usageincremented", "POST")]
    public class SubscriptionUsageIncremented : RequestBase, IReturnVoid, IPost
    {
        public long SubscriptionWorkspaceId { get; set; }
        public long WorkspaceSubscriptionId { get; set; }
        public long ManagedPublisherAccountId { get; set; }
        public string SubscriptionId { get; set; }
        public string CustomerId { get; set; }
        public SubscriptionUsageType UsageType { get; set; }
        public long UsageTimestamp { get; set; }
        public SubscriptionType WorkspaceSubscriptionType { get; set; }
        public SubscriptionType PublisherSubscriptionType { get; set; }
        public long DealId { get; set; }
        public long DealRequestPublisherAccountId { get; set; }
        public DateTime MonthOfService { get; set; }
        public int Amount { get; set; }
    }

    public class WorkspaceSubscription : BaseDateTimeDeleteTrackedDtoModel, IHasSettableId
    {
        public long Id { get; set; }
        public long WorkspaceId { get; set; }
        public string SubscriptionId { get; set; }
        public SubscriptionType SubscriptionType { get; set; }
        public long Quantity { get; set; }
        public long UnitPriceCents { get; set; }
        public string ProductId { get; set; }
        public string PlanId { get; set; }
        public string SubscriptionStatus { get; set; }
        public string SubscriptionEmail { get; set; }
        public DateTime BillingCycleAnchor { get; set; }
        public DateTime SubscriptionStartedOn { get; set; }
        public DateTime SubscriptionEndsOn { get; set; }
        public DateTime SubscriptionTrialStartedOn { get; set; }
        public DateTime SubscriptionTrialEndsOn { get; set; }
        public DateTime SubscriptionCanceledOn { get; set; }
        public string SubscriptionCustomerId { get; set; }
    }
}
