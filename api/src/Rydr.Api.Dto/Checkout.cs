using System;
using System.Collections.Generic;
using System.IO;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Web;

namespace Rydr.Api.Dto
{
    [Route("/checkout/{workspaceidentifier}", "POST")]
    public class PostCheckoutSession : CheckoutSessionSubsriptionBase, IReturn<OnlyResultResponse<CheckoutSession>>
    {
        // NOTE: This particular request is typically only useful from a browser/client app (i.e. not going to do much in postman)
        //         as it basically interacts with the stripeApi on the server, returns a session identifier that then allows the
        //         client to load the native stripe checkout flow, and later a webhook fires on completion that we handle...
    }

    [Route("/checkout/{workspaceidentifier}/publishers", "POST")]
    public class PostCheckoutPublisherSubscription : CheckoutSessionSubsriptionBase, IReturnVoid { }

    public abstract class CheckoutSessionSubsriptionBase : RequestBase, IPost, IHasWorkspaceIdentifier
    {
        public string WorkspaceIdentifier { get; set; }
        public IReadOnlyList<long> PublisherAccountIds { get; set; }
    }

    [Route("/checkout/{workspaceidentifier}/managed", "POST")]
    public class PostCheckoutManagedSubscription : RequestBase, IReturnVoid, IHasPublisherAccountId, IHasWorkspaceIdentifier
    {
        public string WorkspaceIdentifier { get; set; }
        public long PublisherAccountId { get; set; }
        public SubscriptionType SubscriptionType { get; set; }
        public long CustomerWorkspaceId { get; set; }
        public long CustomerUserId { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public bool BackdateToStartOfMonth { get; set; }
        public DateTime? BackdateTo { get; set; }
        public string RydrEmployeeSignature { get; set; }
        public string RydrEmployeeLogin { get; set; }
        public double CustomSubscriptionMonthlyFee { get; set; }
        public double CustomSubscriptionPerPostFee { get; set; }
        public ManagedSubscriptionDiscount MonthlyFeeDiscount { get; set; }
        public ManagedSubscriptionDiscount PerPostDiscount { get; set; }
    }

    [Route("/checkout/{workspaceidentifier}/manageddiscount", "POST")]
    public class PostCheckoutManagedSubscriptionDiscount : RequestBase, IReturnVoid, IHasPublisherAccountId, IHasWorkspaceIdentifier
    {
        public string WorkspaceIdentifier { get; set; }
        public long PublisherAccountId { get; set; }
        public ManagedSubscriptionDiscount Discount { get; set; }
    }

    [Route("/stripe/webhooks", "POST")]
    public class PostStripeWebhook : IRequiresRequestStream, IReturnVoid
    {
        public Stream RequestStream { get; set; }
    }

    // DEFERRED actions
    [Route("/internal/checkout/completed", "POST")]
    public class CheckoutCompleted : RequestBase, IReturnVoid, IPost
    {
        public string CheckoutSessionId { get; set; }
        public string ClientReferenceId { get; set; }
        public string StripeCustomerId { get; set; }
        public string StripeSubscriptionId { get; set; }
        public string StripeSetupIntentId { get; set; }
    }

    [Route("/internal/checkout/customerdeleted", "POST")]
    public class CheckoutCustomerDeleted : RequestBase, IReturnVoid, IPost
    {
        public string StripeCustomerId { get; set; }
    }

    [Route("/internal/checkout/customercreated", "POST")]
    public class CheckoutCustomerCreated : RequestBase, IReturnVoid, IPost
    {
        public string StripeCustomerId { get; set; }
    }

    [Route("/internal/checkout/customerupdated", "POST")]
    public class CheckoutCustomerUpdated : RequestBase, IReturnVoid, IPost
    {
        public string StripeCustomerId { get; set; }
    }

    [Route("/internal/checkout/subscriptiondeleted", "POST")]
    public class CheckoutSubscriptionDeleted : RequestBase, IReturnVoid, IPost
    {
        public string StripeSubscriptionId { get; set; }
    }

    [Route("/internal/checkout/subscriptionupdated", "POST")]
    public class CheckoutSubscriptionUpdated : RequestBase, IReturnVoid, IPost
    {
        public string StripeSubscriptionId { get; set; }
    }

    [Route("/internal/checkout/subscriptioncreated", "POST")]
    public class CheckoutSubscriptionCreated : RequestBase, IReturnVoid, IPost
    {
        public string StripeSubscriptionId { get; set; }
    }

    [Route("/internal/checkout/applyinvoicediscounts", "POST")]
    public class CheckoutApplyInvoiceDiscounts : RequestBase, IReturnVoid, IPost
    {
        public string StripeSubscriptionId { get; set; }
        public string StripeInvoiceId { get; set; }
    }

    public class ManagedSubscriptionDiscount
    {
        public SubscriptionUsageType UsageType { get; set; }
        public int PercentOff { get; set; }
        public DateTime StartsOnInclusive { get; set; }
        public DateTime EndsOnExclusive { get; set; }
    }

    public class CheckoutSession
    {
        public string CheckoutSessionId { get; set; }
        public string StripePublishableApiKey { get; set; }
    }
}
