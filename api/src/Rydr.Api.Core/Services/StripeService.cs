using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;
using Stripe;
using Stripe.Checkout;

namespace Rydr.Api.Core.Services
{
    public class StripeService
    {
        private readonly ISubscriptionPlanService _subscriptionPlanService;

        public const string ModeCreateSubscription = "subscription";
        public const string ModeUpdateSubscription = "setup";

        private static readonly ISecretService _secretService = RydrEnvironment.Container.Resolve<ISecretService>();

        private static readonly List<string> _invoiceExpandList = new List<string>
                                                                  {
                                                                      "lines"
                                                                  };

        private static readonly List<string> _subscriptionExpandList = new List<string>
                                                                       {
                                                                           "plan",
                                                                           "customer",
                                                                           "items"
                                                                       };

        private static readonly List<string> _setupIntentExpandList = new List<string>
                                                                      {
                                                                          "payment_method"
                                                                      };

        private static readonly List<string> _defaultPaymentMethodTypes = new List<string>
                                                                          {
                                                                              "card"
                                                                          };

        private static readonly List<string> _subscriptionItemExpandList = new List<string>
                                                                           {
                                                                               "data.plan"
                                                                           };

        private static readonly string _stripeSecretKeyName = RydrEnvironment.GetAppSetting("Stripe.SecretKeyName", "StripeSecretKey.Dev");
        private static readonly string _stripeAccountId = RydrEnvironment.GetAppSetting("Stripe.AccountId", "acct_1FnUY5CUCt0d3IeS");
        private static readonly string _stripeSuccessRedirect = RydrEnvironment.GetAppSetting("Stripe.SuccessRedirect", "https://teamsdev.getrydr.com/subscriptions/complete?stripe_session_id={CHECKOUT_SESSION_ID}");
        private static readonly string _stripeCancelRedirect = RydrEnvironment.GetAppSetting("Stripe.CancelRedirect", "https://teamsdev.getrydr.com/subscriptions/cancel?stripe_session_id={CHECKOUT_SESSION_ID}");

        private static bool _initialized;
        private static StripeService _instance;

        private CustomerService _customerService;
        private SessionService _sessionService;
        private SubscriptionService _subscriptionService;
        private SetupIntentService _setupIntentService;
        private PaymentMethodService _paymentMethodService;
        private CardService _cardService;
        private UsageRecordService _usageRecordService;
        private SubscriptionItemService _subscriptionItemService;
        private InvoiceService _invoiceService;
        private InvoiceItemService _invoiceItemService;
        private PriceService _priceService;

        private StripeService(ISubscriptionPlanService subscriptionPlanService)
        {
            _subscriptionPlanService = subscriptionPlanService;
        }

        public async Task<Invoice> PayInvoiceAsync(string invoiceId, string idempotencyKey)
        {
            var requestOptions = idempotencyKey.IsNullOrEmpty()
                                     ? null
                                     : new RequestOptions
                                       {
                                           IdempotencyKey = idempotencyKey
                                       };

            var result = await _invoiceService.PayAsync(invoiceId,
                                                        new InvoicePayOptions
                                                        {
                                                            Expand = _invoiceExpandList
                                                        },
                                                        requestOptions);

            return result;
        }

        public async Task<Invoice> GetUpcomingInvoiceAsync(string subscriptionId = null, string customerId = null)
        {
            var result = await _invoiceService.UpcomingAsync(new UpcomingInvoiceOptions
                                                             {
                                                                 Subscription = subscriptionId,
                                                                 Customer = customerId
                                                             });

            return result;
        }

        public async Task<Invoice> GetInvoiceAsync(string invoiceId)
        {
            var result = await _invoiceService.GetAsync(invoiceId,
                                                        new InvoiceGetOptions
                                                        {
                                                            Expand = _invoiceExpandList
                                                        });

            return result;
        }

        public async Task CreateInvoiceLineItem(string customerId, string invoiceId, string subscriptionId,
                                                int cents, string description, string idempotencyKey = null)
        {
            var requestOptions = idempotencyKey.IsNullOrEmpty()
                                     ? null
                                     : new RequestOptions
                                       {
                                           IdempotencyKey = idempotencyKey
                                       };

            await _invoiceItemService.CreateAsync(new InvoiceItemCreateOptions
                                                  {
                                                      Customer = customerId,
                                                      Amount = cents,
                                                      Currency = "USD",
                                                      Description = description,
                                                      Invoice = invoiceId,
                                                      Subscription = subscriptionId
                                                  },
                                                  requestOptions);
        }

        public Task UpdateInvoiceLineItem(string invoiceItemId, int cents, string description)
            => _invoiceItemService.UpdateAsync(invoiceItemId,
                                               new InvoiceItemUpdateOptions
                                               {
                                                   Amount = cents,
                                                   Description = description
                                               });

        public static async ValueTask<StripeService> GetInstanceAsync()
        {
            if (_instance != null && _initialized)
            {
                return _instance;
            }

            // Race condition here, but it doesn't matter, we'll wind up with one in the end, all the same
            var instance = new StripeService(RydrEnvironment.Container.Resolve<ISubscriptionPlanService>());

            await instance.InitAsync();

            _instance = instance;
            _initialized = true;

            return _instance;
        }

        public static string PublishableKey { get; } = RydrEnvironment.GetAppSetting("Stripe.PublishableKey", "pk_test_vN1mQL93RRE9NN0jgkem8BqA00810vKs1A");

        private async Task InitAsync()
        {
            StripeConfiguration.ApiKey = await _secretService.GetSecretStringAsync(_stripeSecretKeyName);
            StripeConfiguration.MaxNetworkRetries = 2;

            _customerService = new CustomerService();
            _sessionService = new SessionService();
            _subscriptionService = new SubscriptionService();
            _setupIntentService = new SetupIntentService();
            _paymentMethodService = new PaymentMethodService();
            _cardService = new CardService();
            _usageRecordService = new UsageRecordService();
            _subscriptionItemService = new SubscriptionItemService();
            _invoiceService = new InvoiceService();
            _invoiceItemService = new InvoiceItemService();
            _priceService = new PriceService();
        }

        private RequestOptions DefaultRequestOptions() => new RequestOptions
                                                          {
                                                              StripeAccount = _stripeAccountId
                                                          };

        public IAsyncEnumerable<Price> GetProductPricesAsync(string productId, string lookupKey = null)
            => _priceService.ListAutoPagingAsync(new PriceListOptions
                                                 {
                                                     Active = true,
                                                     Product = productId,
                                                     Limit = 50,
                                                     LookupKeys = lookupKey == null
                                                                      ? null
                                                                      : new List<string>(1)
                                                                        {
                                                                            lookupKey
                                                                        }
                                                 });

        public async Task<Price> CreateProductPriceAsync(string productId, int unitAmountCents, string lookupKey)
        {
            var requestOptions = new RequestOptions
                                 {
                                     IdempotencyKey = lookupKey ?? string.Concat(productId, "|", unitAmountCents),
                                 };

            var priceCreateOptions = new PriceCreateOptions
                                     {
                                         Product = productId,
                                         Currency = "USD",
                                         UnitAmount = unitAmountCents,
                                         BillingScheme = "per_unit",
                                         LookupKey = lookupKey,
                                         Recurring = new PriceRecurringOptions
                                                     {
                                                         Interval = "month",
                                                         IntervalCount = 1,
                                                         UsageType = "licensed"
                                                     }
                                     };

            var result = await _priceService.CreateAsync(priceCreateOptions, requestOptions);

            return result;
        }

        public async Task<Price> CreateProductMeteredPriceAsync(string productId, int unitAmountCents, bool sumUsage, string lookupKey)
        {
            var requestOptions = new RequestOptions
                                 {
                                     IdempotencyKey = lookupKey ?? string.Concat(productId, "|", unitAmountCents),
                                 };

            var priceCreateOptions = new PriceCreateOptions
                                     {
                                         Product = productId,
                                         Currency = "USD",
                                         UnitAmount = unitAmountCents,
                                         BillingScheme = "per_unit",
                                         LookupKey = lookupKey,
                                         Recurring = new PriceRecurringOptions
                                                     {
                                                         AggregateUsage = sumUsage
                                                                              ? "sum"
                                                                              : "last_during_period",
                                                         Interval = "month",
                                                         IntervalCount = 1,
                                                         UsageType = "metered"
                                                     }
                                     };

            var result = await _priceService.CreateAsync(priceCreateOptions, requestOptions);

            return result;
        }

        public Task<Customer> GetCustomerAsync(string stripeCustomerId)
            => _customerService.GetAsync(stripeCustomerId);

        public async Task IncrementUsageAsync(string subscriptionId, SubscriptionUsageType usageType, string idempotencyKey,
                                              int customMonthlyFeeCents, int customPerPostFeeCents,
                                              long quantity = 1, long usageTimestamp = 0)
        {
            Guard.AgainstArgumentOutOfRange(quantity > 100, "Usage quantity seems too high");

            if (subscriptionId.IsNullOrEmpty() || usageType == SubscriptionUsageType.None)
            {
                return;
            }

            var subscriptionItems = await _subscriptionItemService.ListAsync(new SubscriptionItemListOptions
                                                                             {
                                                                                 Subscription = subscriptionId,
                                                                                 Limit = 100,
                                                                                 Expand = _subscriptionItemExpandList
                                                                             });

            if ((subscriptionItems?.Data).IsNullOrEmpty())
            {
                return;
            }

            // Only managed plans incur usage charges
            var managedPlanItem = await subscriptionItems.FirstOrDefaultAsync(i => _subscriptionPlanService.IsManagedPlanIdAsync(i.Plan?.Id));

            if (managedPlanItem == null)
            {
                return;
            }

            var managedSubscriptionType = await _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(managedPlanItem.Plan.Id);

            var planIdForUsageType = await _subscriptionPlanService.GetSubscriptionPlanIdForUsageTypeAsync(managedSubscriptionType, usageType, customMonthlyFeeCents, customPerPostFeeCents);

            if (planIdForUsageType.IsNullOrEmpty())
            {
                return;
            }

            // Get the subscription item with the planId for the given usage type, and report away
            var usagePlanItem = subscriptionItems.FirstOrDefault(si => si.Plan.Id.EqualsOrdinalCi(planIdForUsageType));

            if (usagePlanItem == null)
            {
                return;
            }

            var requestOptions = idempotencyKey.IsNullOrEmpty()
                                     ? null
                                     : new RequestOptions
                                       {
                                           IdempotencyKey = idempotencyKey
                                       };

            var timestamp = usageTimestamp > DateTimeHelper.MinApplicationDateTs
                                ? usageTimestamp.ToDateTime()
                                : DateTimeHelper.UtcNow;

            await _usageRecordService.CreateAsync(usagePlanItem.Id,
                                                  new UsageRecordCreateOptions
                                                  {
                                                      Quantity = quantity.Gz(1),
                                                      Timestamp = timestamp
                                                  },
                                                  requestOptions);
        }

        public async Task<Customer> CreateCustomerAsync(long workspaceId, string email, string name, string phone,
                                                        string activeCampaignId, string managedPublisherUserName, long publisherAccountId)
        {
            var meta = new Dictionary<string, string>
                       {
                           {
                               "WorkspaceId", workspaceId.ToStringInvariant()
                           },
                           {
                               "WorkspaceEmail", email
                           },
                           {
                               "PublisherUserName", managedPublisherUserName
                           },
                           {
                               "PublisherAccountId", publisherAccountId.ToStringInvariant()
                           }
                       };

            if (activeCampaignId.HasValue())
            {
                meta.Add("ActiveCampaignId", activeCampaignId);
                meta.Add("ActiveCampaignLink", $"https://getrydr.activehosted.com/app/contacts/{activeCampaignId}");
            }

            var response = await _customerService.CreateAsync(new CustomerCreateOptions
                                                              {
                                                                  Email = email,
                                                                  Metadata = meta,
                                                                  Name = name,
                                                                  Phone = phone,
                                                                  NextInvoiceSequence = RandomProvider.GetRandomIntBeween(12135, 81351)
                                                              });

            return response;
        }

        public async Task<List<Card>> GetCustomerCreditCardsAsync(string stripeCustomerId)
        {
            var response = await _cardService.ListAsync(stripeCustomerId, new CardListOptions
                                                                          {
                                                                              Limit = 50
                                                                          });

            return response.Data;
        }

        public Task UpdateCustomerDefaultPaymentMethodAsync(string stripeCustomerId, string paymentMethodId)
            => _customerService.UpdateAsync(stripeCustomerId, new CustomerUpdateOptions
                                                              {
                                                                  DefaultSource = paymentMethodId
                                                              });

        public Task UpdateCustomerMetaDataAsync(string stripeCustomerId, Dictionary<string, string> metadata)
            => _customerService.UpdateAsync(stripeCustomerId, new CustomerUpdateOptions
                                                              {
                                                                  Metadata = metadata
                                                              });

        public async Task<Subscription> UpdateSubscriptionMetaAsync(long workspaceId, string stripeSubscriptionId, Dictionary<string, string> subscriptionMeta)
        {
            Guard.AgainstNullArgument(subscriptionMeta.IsNullOrEmptyRydr(), nameof(subscriptionMeta));

            if (workspaceId > 0)
            {
                subscriptionMeta["WorkspaceId"] = workspaceId.ToStringInvariant();
            }

            var result = await _subscriptionService.UpdateAsync(stripeSubscriptionId, new SubscriptionUpdateOptions
                                                                                      {
                                                                                          Metadata = subscriptionMeta
                                                                                      });

            return result;
        }

        public async Task<Subscription> UpdateSubscriptionQuantityAsync(long workspaceId, string stripeSubscriptionId, string stripePlanId,
                                                                        string existingSubscriptionItemId, long newQuantity,
                                                                        Dictionary<string, string> subscriptionMeta)
        {
            subscriptionMeta ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            subscriptionMeta["WorkspaceId"] = workspaceId.ToStringInvariant();

            var result = await _subscriptionService.UpdateAsync(stripeSubscriptionId, new SubscriptionUpdateOptions
                                                                                      {
                                                                                          Items = new List<SubscriptionItemOptions>
                                                                                                  {
                                                                                                      new SubscriptionItemOptions
                                                                                                      {
                                                                                                          Id = existingSubscriptionItemId,
                                                                                                          Plan = stripePlanId,
                                                                                                          Quantity = newQuantity
                                                                                                      }
                                                                                                  },
                                                                                          Metadata = subscriptionMeta
                                                                                      });

            return result;
        }

        public async Task<Subscription> CreateManagedSubscriptionAsync(long workspaceId, long publisherAccountId, string stripeCustomerId,
                                                                       SubscriptionType subscriptionType, Dictionary<string, string> subscriptionMeta,
                                                                       DateTime? backdateTo = null, int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0)
        {
            Guard.AgainstArgumentOutOfRange(!subscriptionType.IsManagedSubscriptionType(), "Only managed subscriptions can be added by this method");

            subscriptionMeta ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            subscriptionMeta["WorkspaceId"] = workspaceId.ToStringInvariant();
            subscriptionMeta["ManagedSubscriptionType"] = subscriptionType.ToString();
            subscriptionMeta["PublisherAccountId"] = publisherAccountId.ToString();

            var utcNow = DateTimeHelper.UtcNow;

            var subscriptionStartOn = (backdateTo?.ToUniversalTime() ?? utcNow);

            subscriptionMeta["SubscriptionStartedOn"] = subscriptionStartOn.ToIso8601Utc();

            var subscriptionItemIds = await _subscriptionPlanService.GetPlanIdsForSubscriptionTypeAsync(subscriptionType, customMonthlyFeeCents, customPerPostFeeCents);

            var subscriptionItems = subscriptionItemIds.Select(p => new SubscriptionItemOptions
                                                                    {
                                                                        Plan = p
                                                                    })
                                                       .AsList();

            var subscriptionCreateOptions = new SubscriptionCreateOptions
                                            {
                                                Items = subscriptionItems,
                                                Metadata = subscriptionMeta,
                                                Customer = stripeCustomerId,
                                                CollectionMethod = "send_invoice",
                                                BillingCycleAnchor = utcNow.StartOfNextMonth(),
                                                BackdateStartDate = backdateTo.HasValue
                                                                        ? subscriptionStartOn
                                                                        : (DateTime?)null,
                                                Prorate = subscriptionType == SubscriptionType.ManagedCustomAdvance,
                                                DaysUntilDue = 5
                                            };

            var result = await _subscriptionService.CreateAsync(subscriptionCreateOptions);

            return result;
        }

        public Task CancelSubscriptionAsync(string stripeSubscriptionId)
            => _subscriptionService.CancelAsync(stripeSubscriptionId, new SubscriptionCancelOptions
                                                                      {
                                                                          InvoiceNow = true,
                                                                          Prorate = true
                                                                      });

        public Task<Subscription> GetSubscriptionAsync(string stripeSubscriptionId)
            => _subscriptionService.GetAsync(stripeSubscriptionId, new SubscriptionGetOptions
                                                                   {
                                                                       Expand = _subscriptionExpandList
                                                                   });

        public Task<SetupIntent> GetSetupIntent(string setupIntentId)
            => _setupIntentService.GetAsync(setupIntentId, new SetupIntentGetOptions
                                                           {
                                                               Expand = _setupIntentExpandList
                                                           });

        public Task AttachPaymentMethodAsync(string paymentMethodId, string customerId)
            => _paymentMethodService.AttachAsync(paymentMethodId, new PaymentMethodAttachOptions
                                                                  {
                                                                      Customer = customerId
                                                                  });

        public Task UpdateSubscriptionDefaultPaymentMethodAsync(string subscriptionId, string paymentMethodId)
            => _subscriptionService.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
                                                                {
                                                                    DefaultPaymentMethod = paymentMethodId
                                                                });

        public async Task<Session> CreateCheckoutSessionAsync(long workspaceId, string customerId, IReadOnlyList<long> publisherAccountIds,
                                                              string planId = null, string existingSubscriptionId = null)
        {
            Guard.AgainstArgumentOutOfRange(customerId.IsNullOrEmpty(), "Customers should be created prior to initiating a checkout session");

            var workspaceRefId = workspaceId.ToStringInvariant();

            var opts = new SessionCreateOptions
                       {
                           SuccessUrl = _stripeSuccessRedirect,
                           CancelUrl = _stripeCancelRedirect,
                           ClientReferenceId = workspaceRefId,
                           PaymentMethodTypes = _defaultPaymentMethodTypes
                       };

            if (existingSubscriptionId.HasValue())
            {
                opts.Mode = ModeUpdateSubscription;
                opts.Customer = customerId;

                opts.SetupIntentData = new SessionSetupIntentDataOptions
                                       {
                                           Metadata = new Dictionary<string, string>
                                                      {
                                                          {
                                                              "customerId", customerId
                                                          },
                                                          {
                                                              "subscriptionId", existingSubscriptionId
                                                          },
                                                          {
                                                              "workspaceId", workspaceRefId
                                                          }
                                                      }
                                       };
            }
            else
            {
                Guard.AgainstArgumentOutOfRange(publisherAccountIds.IsNullOrEmptyReadOnly(), "New subscriptions require one or more publishers to be subscribed");

                opts.Customer = customerId;

                var stripeSubscriptionMeta = WorkspaceExtensions.GetStripeSubscriptionPublisherAccountsMetas(workspaceId, publisherAccountIds);

                opts.SubscriptionData = new SessionSubscriptionDataOptions
                                        {
                                            Items = new List<SessionSubscriptionDataItemOptions>
                                                    {
                                                        new SessionSubscriptionDataItemOptions
                                                        {
                                                            Plan = planId.Coalesce(_subscriptionPlanService.PayPerBusinessPlanId),
                                                            Quantity = publisherAccountIds.Count
                                                        }
                                                    },
                                            Metadata = stripeSubscriptionMeta
                                        };

                opts.Mode = ModeCreateSubscription;
            }

            var session = await _sessionService.CreateAsync(opts, DefaultRequestOptions());

            return session;
        }
    }
}
