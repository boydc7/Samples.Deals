using System.Text;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Services.Helpers;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;
using Stripe;
using Stripe.Checkout;

namespace Rydr.Api.Services.Services;

[Restrict(VisibleLocalhostOnly = true)]
public class CheckoutPublicService : BaseApiService
{
    private static readonly bool _isLocalEnvironment = RydrEnvironment.IsLocalEnvironment;
    private static readonly ILog _staticLog = LogManager.GetLogger("CheckoutPublicService");
    private static readonly IDeferRequestsService _deferRequestsService = RydrEnvironment.Container.Resolve<IDeferRequestsService>();
    private static readonly IRydrDataService _rydrDataService = RydrEnvironment.Container.Resolve<IRydrDataService>();
    private static readonly ISubscriptionPlanService _subscriptionPlanService = RydrEnvironment.Container.Resolve<ISubscriptionPlanService>();

    private static string _stripeSigningSecret;

    private static readonly Dictionary<string, Func<Event, Task<bool>>> _stripeWebhookTypeProcessMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {
                Events.CheckoutSessionCompleted, e => ProcessEvent<Session, CheckoutCompleted>(e, s => new CheckoutCompleted
                                                                                                       {
                                                                                                           CheckoutSessionId = s.Id,
                                                                                                           ClientReferenceId = s.ClientReferenceId,
                                                                                                           StripeCustomerId = s.CustomerId,
                                                                                                           StripeSubscriptionId = s.SubscriptionId,
                                                                                                           StripeSetupIntentId = s.SetupIntentId
                                                                                                       })
            },
            {
                Events.CustomerDeleted, e => ProcessEvent<Customer, CheckoutCustomerDeleted>(e, c => new CheckoutCustomerDeleted
                                                                                                     {
                                                                                                         StripeCustomerId = c.Id
                                                                                                     })
            },
            {
                Events.CustomerSubscriptionDeleted, e => ProcessEvent<Subscription, CheckoutSubscriptionDeleted>(e, s => new CheckoutSubscriptionDeleted
                                                                                                                         {
                                                                                                                             StripeSubscriptionId = s.Id
                                                                                                                         })
            },
            {
                Events.CustomerSubscriptionUpdated, e => ProcessEvent<Subscription, CheckoutSubscriptionUpdated>(e, s => new CheckoutSubscriptionUpdated
                                                                                                                         {
                                                                                                                             StripeSubscriptionId = s.Id
                                                                                                                         })
            },
            {
                Events.CustomerSubscriptionCreated, e => ProcessEvent<Subscription, CheckoutSubscriptionCreated>(e, s => new CheckoutSubscriptionCreated
                                                                                                                         {
                                                                                                                             StripeSubscriptionId = s.Id
                                                                                                                         })
            },
            {
                Events.CustomerCreated, e => ProcessEvent<Customer, CheckoutCustomerCreated>(e, c => new CheckoutCustomerCreated
                                                                                                     {
                                                                                                         StripeCustomerId = c.Id
                                                                                                     })
            },
            {
                Events.CustomerUpdated, e => ProcessEvent<Customer, CheckoutCustomerUpdated>(e, c => new CheckoutCustomerUpdated
                                                                                                     {
                                                                                                         StripeCustomerId = c.Id
                                                                                                     })
            },
            {
                Events.InvoiceCreated, ProcessInvoiceAsync
            },
            {
                Events.InvoiceUpcoming, ProcessInvoiceAsync
            },
            {
                Events.InvoiceFinalized, ProcessInvoiceAsync
            },
            {
                Events.InvoiceVoided, ProcessInvoiceAsync
            },
            {
                Events.InvoiceUpdated, ProcessInvoiceAsync
            },
            {
                Events.InvoicePaymentFailed, ProcessInvoiceAsync
            },
            {
                Events.InvoiceMarkedUncollectible, ProcessInvoiceAsync
            },
            {
                Events.InvoicePaymentSucceeded, ProcessInvoiceAsync
            },
            {
                Events.InvoicePaymentActionRequired, ProcessInvoiceAsync
            }
        };

    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly ISecretService _secretService;
    private readonly IOpsNotificationService _opsNotificationService;

    public CheckoutPublicService(IFileStorageProvider fileStorageProvider,
                                 ISecretService secretService,
                                 IOpsNotificationService opsNotificationService)
    {
        _fileStorageProvider = fileStorageProvider;
        _secretService = secretService;
        _opsNotificationService = opsNotificationService;
    }

    public async Task Post(PostStripeWebhook request)
    {
        var requestBody = await new StreamReader(request.RequestStream).ReadToEndAsync();

        Event stripeEvent = null;
        string eventType = null;
        var processed = false;
        Exception ex = null;
        string warnMsg = null;
        var utcNow = _dateTimeProvider.UtcNow;

        if (_stripeSigningSecret == null)
        {
            await GetStripeSigningSecretAsync();
        }

        try
        {
            var stripeSignature = Request.GetHeader("Stripe-Signature");

            if (stripeSignature.IsNullOrEmpty() && !_isLocalEnvironment)
            {
                _log.ErrorFormat("StripeWebhook missing stripe signature, will not process - event [{0}]", requestBody.Left(200));

                return;
            }

            stripeEvent = _isLocalEnvironment
                              ? EventUtility.ParseEvent(requestBody)
                              : EventUtility.ConstructEvent(requestBody, stripeSignature, _stripeSigningSecret);

            if (stripeEvent == null)
            {
                ex = new InvalidDataArgumentException($"StripeWebhook received but unable to parse event - body [{requestBody.Left(250)}]");

                return;
            }

            eventType = stripeEvent.Type;

            if (_stripeWebhookTypeProcessMap.ContainsKey(eventType))
            {
                if (_log.IsDebugEnabled)
                {
                    _log.Debug($"StripeWebhook [{eventType}] beginning for event [{stripeEvent.ToJsv().Left(500)}]");
                }

                processed = await _stripeWebhookTypeProcessMap[eventType](stripeEvent);
            }
            else
            {
                warnMsg = "Received unhandled StripeWebhook - type [{0}], event [{1}], request stored [{2}]";
            }
        }
        catch(Exception x)
        {
            ex = x;
        }
        finally
        {
            if (ex != null || !processed)
            {
                var requestFileMeta = new FileMetaData(Path.Combine(RydrFileStoragePaths.FilesRoot, "stripe", utcNow.ToString("yyyy-MM"), utcNow.ToString("dd")),
                                                       string.Concat(utcNow.ToString("HHmmssffff"), eventType.HasValue()
                                                                                                        ? "_"
                                                                                                        : null,
                                                                     eventType, ".json"))
                                      {
                                          Bytes = Encoding.UTF8.GetBytes(requestBody)
                                      };

                if (ex != null)
                {
                    _log.Exception(ex, $"PostStripeWebhook could not be handled - request stored [{requestFileMeta.FullName}]");
                }

                if (!processed)
                {
                    if (!_isLocalEnvironment)
                    {
                        requestFileMeta.Tags.Add(FileStorageTag.Privacy.ToString(), FileStorageTags.PrivacyPrivate);

                        await _fileStorageProvider.StoreAsync(requestFileMeta, new FileStorageOptions
                                                                               {
                                                                                   ContentType = "application/json",
                                                                                   Encrypt = true,
                                                                                   StorageClass = FileStorageClass.Intelligent
                                                                               });

                        await _opsNotificationService.TrySendApiNotificationAsync("StripeWebhook could not be handled",
                                                                                  $"Request stored at [{requestFileMeta.FullName}] \n <https://app.datadoghq.com/logs?live=true&query=\"PostStripeWebhook\"|Webhook Logs>");
                    }

                    if (ex == null)
                    {
                        _log.WarnFormat(warnMsg.Coalesce("Did not process StripeWebhook [{0}] for unknown reason - event [{1}], request store [{2}]"), eventType, requestBody.Left(100), requestFileMeta.FullName);
                    }
                }
            }
        }
    }

    private async Task GetStripeSigningSecretAsync()
    {
        if (_stripeSigningSecret != null)
        {
            return;
        }

        var secretKeyName = RydrEnvironment.GetAppSetting("Stripe.WebhookSigningSecretKeyName", "StripeWebhookSigningSecret.Dev");

        _stripeSigningSecret = (await _secretService.GetSecretStringAsync(secretKeyName)).Coalesce("whsec_TgwAQeNlH49HAZmqqr4f9Zx5u3O5GiLu");

#if LOCAL
        _stripeSigningSecret = "whsec_TgwAQeNlH49HAZmqqr4f9Zx5u3O5GiLu";
#endif
    }

    private static Task<bool> ProcessEvent<TStripe, TDefer>(Event stripeEvent, Func<TStripe, TDefer> deferObj)
        where TDefer : RequestBase
    {
        if (stripeEvent?.Data.Object == null ||
            !(stripeEvent.Data.Object is TStripe stripeObject) ||
            stripeObject == null)
        {
            return Task.FromResult(false);
        }

        var toDefer = deferObj(stripeObject);

        if (toDefer == null)
        {
            return Task.FromResult(false);
        }

        _deferRequestsService.DeferFifoRequest(toDefer.WithAdminRequestInfo());

        return Task.FromResult(true);
    }

    private static async Task<bool> ProcessInvoiceAsync(Event stripeEvent)
    {
        if (stripeEvent?.Data?.Object is not Invoice invoice || invoice == null)
        {
            _staticLog.WarnFormat("  ProcessInvoiceAsync cannot process invoice - invalid stripe event (null or not a valid invoice), event type of [{0}]", stripeEvent?.Type ?? "Unknown");

            return false;
        }

        _staticLog.InfoFormat("  ProcessInvoiceAsync fired for event [{0}], invoice [{1}], subscription [{2}], customer [{3}]", stripeEvent.Type, invoice.Id, invoice.SubscriptionId, invoice.CustomerId);

        if (stripeEvent.Type.EqualsOrdinalCi(Events.InvoiceUpcoming) ||
            stripeEvent.Type.EqualsOrdinalCi(Events.InvoiceCreated) ||
            invoice.Status.EqualsOrdinalCi("draft"))
        {
            _deferRequestsService.DeferFifoRequest(new CheckoutApplyInvoiceDiscounts
                                                   {
                                                       StripeSubscriptionId = invoice.SubscriptionId,
                                                       StripeInvoiceId = invoice.Id
                                                   }.WithAdminRequestInfo());

            if (stripeEvent.Type.EqualsOrdinalCi(Events.InvoiceUpcoming))
            {
                return true;
            }
        }

        var workplaceSubscriptionIndexItem = await ValidationExtensions._dynamoDb.GetItemEdgeIndexAsync(DynItemType.WorkspaceSubscription,
                                                                                                        DynWorkspaceSubscription.BuildEdgeId(invoice.SubscriptionId),
                                                                                                        ignoreRecordNotFound: true);

        var workspaceId = workplaceSubscriptionIndexItem?.Id ?? 0;
        var workspaceSubscriptionId = workplaceSubscriptionIndexItem?.OwnerId ?? 0;
        var workspacePublisherSubscriptionId = 0L;

        if (workspaceId <= 0)
        {
            var managedSubscription = await WorkspaceService.DefaultWorkspacePublisherSubscriptionService.GetManagedPublisherSubscriptionAsync(invoice.SubscriptionId);

            workspaceId = managedSubscription?.SubscriptionWorkspaceId ?? 0;
            workspaceSubscriptionId = managedSubscription?.DynWorkspaceSubscriptionId ?? 0;
            workspacePublisherSubscriptionId = managedSubscription?.PublisherAccountId ?? 0;
        }

        var stripeService = await StripeService.GetInstanceAsync();
        var stripeSubscription = await stripeService.GetSubscriptionAsync(invoice.SubscriptionId);

        var subscriptionTypeItem = await _subscriptionPlanService.GetManagedPlanSubscriptionItemAsync(stripeSubscription);

        var subscriptionType = await _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(subscriptionTypeItem?.Plan.Id);

        var rydrInvoice = invoice.ToRydrInvoice(workspaceId, workspaceSubscriptionId, workspacePublisherSubscriptionId,
                                                subscriptionType, stripeEvent.Type);

        await _rydrDataService.SaveIgnoreConflictAsync(rydrInvoice, r => r.Id,
                                                       stripeEvent.Type.EqualsOrdinalCi(Events.InvoicePaymentSucceeded) ||
                                                       stripeEvent.Type.EqualsOrdinalCi(Events.InvoicePaymentFailed) ||
                                                       stripeEvent.Type.EqualsOrdinalCi(Events.InvoicePaymentActionRequired) ||
                                                       stripeEvent.Type.EqualsOrdinalCi(Events.InvoiceMarkedUncollectible));

        return true;
    }
}

[RydrNeverCacheResponse]
[Restrict(VisibleLocalhostOnly = true)]
public class CheckoutService : BaseAuthenticatedApiService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserService _userService;
    private readonly IMapItemService _mapItemService;
    private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;
    private readonly IWorkspacePublisherSubscriptionService _workspacePublisherSubscriptionService;
    private readonly IPublisherAccountService _publisherAccountService;

    public CheckoutService(IWorkspaceService workspaceService,
                           IUserService userService,
                           IMapItemService mapItemService,
                           IWorkspaceSubscriptionService workspaceSubscriptionService,
                           IWorkspacePublisherSubscriptionService workspacePublisherSubscriptionService,
                           IPublisherAccountService publisherAccountService)
    {
        _workspaceService = workspaceService;
        _userService = userService;
        _mapItemService = mapItemService;
        _workspaceSubscriptionService = workspaceSubscriptionService;
        _workspacePublisherSubscriptionService = workspacePublisherSubscriptionService;
        _publisherAccountService = publisherAccountService;
    }

    [RequiredRole("Admin")]
    public async Task Post(PostCheckoutManagedSubscriptionDiscount request)
    {
        var publisherSubscription = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(request.GetWorkspaceIdFromIdentifier(), request.PublisherAccountId);

        await AddManagedSubscriptionDiscount(request.Discount, publisherSubscription);
    }

    [RequiredRole("Admin")]
    public async Task Post(PostCheckoutManagedSubscription request)
    {
        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(request.GetWorkspaceIdFromIdentifier());

        var publisherAccount = await _publisherAccountService.GetPublisherAccountAsync(request.PublisherAccountId);

        var publisherSubscription = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(dynWorkspace.Id,
                                                                                                               publisherAccount.PublisherAccountId);

        var stripeCustomerId = publisherSubscription?.StripeCustomerId;

        if (stripeCustomerId.IsNullOrEmpty())
        {
            var customerWorkspaceEmail = await _workspaceService.TryGetWorkspacePrimaryEmailAddressAsync(request.CustomerWorkspaceId);
            var dynCustomerUser = await _userService.TryGetUserAsync(request.CustomerUserId);

            var customerEmail = request.CustomerEmail.Coalesce(publisherAccount.Email)
                                       .Coalesce(customerWorkspaceEmail)
                                       .Coalesce(dynCustomerUser?.Email);

            var customerName = request.CustomerName.Coalesce(dynCustomerUser?.DisplayName)
                                      .Coalesce(dynCustomerUser?.FullName)
                                      .Coalesce(publisherAccount.FullName)
                                      .Coalesce(publisherAccount.UserName);

            var customerPhone = request.CustomerPhone.Coalesce(dynCustomerUser?.PhoneNumber).Left(20);

            var stripe = await StripeService.GetInstanceAsync();

            var stripeResponse = await stripe.CreateCustomerAsync(dynWorkspace.Id, customerEmail, customerName, customerPhone,
                                                                  publisherSubscription?.ActiveCampaignCustomerId, publisherAccount.UserName, publisherAccount.PublisherAccountId);

            stripeCustomerId = stripeResponse.Id;
        }

        var backdateTo = request.BackdateTo ?? (request.BackdateToStartOfMonth
                                                    ? _dateTimeProvider.UtcNow.StartOfMonth()
                                                    : null);

        if (request.MonthlyFeeDiscount != null)
        {
            await AddManagedSubscriptionDiscount(request.MonthlyFeeDiscount, publisherSubscription);
        }

        if (request.PerPostDiscount != null)
        {
            await AddManagedSubscriptionDiscount(request.PerPostDiscount, publisherSubscription);
        }

        await _workspacePublisherSubscriptionService.AddManagedSubscriptionAsync(dynWorkspace.Id, publisherAccount, request.SubscriptionType,
                                                                                 stripeCustomerId, backdateTo: backdateTo,
                                                                                 rydrEmployeeSig: request.RydrEmployeeSignature.ToNullIfEmpty(),
                                                                                 rydrEmployeeLogin: request.RydrEmployeeLogin.ToNullIfEmpty(),
                                                                                 customMonthlyFee: request.CustomSubscriptionMonthlyFee,
                                                                                 customPerPostFee: request.CustomSubscriptionPerPostFee);
    }

    public async Task Post(PostCheckoutPublisherSubscription request)
        => await _workspacePublisherSubscriptionService.AddSubscribedPayPerBusinessPublisherAccountsAsync(request.GetWorkspaceIdFromIdentifier(),
                                                                                                          request.PublisherAccountIds);

    public async Task<OnlyResultResponse<CheckoutSession>> Post(PostCheckoutSession request)
    {
        var dynWorkspace = await _workspaceService.GetWorkspaceAsync(request.GetWorkspaceIdFromIdentifier());
        var dynUser = await _userService.GetUserAsync(dynWorkspace.OwnerId);
        var dynWorkspaceSubscription = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(dynWorkspace.Id);

        var stripe = await StripeService.GetInstanceAsync();

        if (dynWorkspace.StripeCustomerId.IsNullOrEmpty())
        {
            if (dynWorkspaceSubscription.IsPaidSubscription() && dynWorkspaceSubscription.SubscriptionCustomerId.HasValue())
            {
                dynWorkspace.StripeCustomerId = dynWorkspaceSubscription.SubscriptionCustomerId;
            }
            else
            { // Create a new customer at stripe first
                var workspaceEmail = (await _workspaceService.TryGetWorkspacePrimaryEmailAddressAsync(dynWorkspace.Id)).Coalesce(dynUser.Email);

                var stripeCustomer = await stripe.CreateCustomerAsync(dynWorkspace.Id, workspaceEmail, dynUser.DisplayName.Coalesce(dynUser.FullName),
                                                                      dynUser.PhoneNumber, dynWorkspace.ActiveCampaignCustomerId, null, 0);

                dynWorkspace.StripeCustomerId = stripeCustomer?.Id;
            }

            if (dynWorkspace.StripeCustomerId.HasValue())
            {
                await _workspaceService.UpdateWorkspaceAsync(dynWorkspace, () => new DynWorkspace
                                                                                 {
                                                                                     StripeCustomerId = dynWorkspace.StripeCustomerId
                                                                                 });
            }
        }

        var stripeSession = await stripe.CreateCheckoutSessionAsync(dynWorkspace.Id,
                                                                    dynWorkspace.StripeCustomerId,
                                                                    request.PublisherAccountIds,
                                                                    dynWorkspaceSubscription.IsPaidSubscription()
                                                                        ? dynWorkspaceSubscription.SubscriptionId
                                                                        : null);

        var itemsMeta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!request.PublisherAccountIds.IsNullOrEmptyReadOnly())
        {
            itemsMeta.Add("PublisherAccountIds", string.Join(',', request.PublisherAccountIds));
        }

        if ((dynWorkspaceSubscription.IsPaidSubscription()
                 ? dynWorkspaceSubscription.SubscriptionId
                 : null).HasValue())
        {
            itemsMeta.Add("ExistingSubscriptionId", dynWorkspaceSubscription.SubscriptionId);
            itemsMeta.Add("ExistingCustomerId", dynWorkspaceSubscription.SubscriptionCustomerId);
        }

        // Put in a temporary map holder for this session
        await _mapItemService.PutMapAsync(new DynItemMap
                                          {
                                              Id = dynWorkspace.Id,
                                              EdgeId = DynItemMap.BuildEdgeId(DynItemType.WorkspaceSubscription, stripeSession.Id),
                                              ExpiresAt = _dateTimeProvider.UtcNowTs + (60 * 60 * 45),
                                              Items = itemsMeta.ToNullIfEmpty()
                                          });

        return new CheckoutSession
               {
                   CheckoutSessionId = stripeSession.Id,
                   StripePublishableApiKey = StripeService.PublishableKey
               }.AsOnlyResultResponse();
    }

    private async Task AddManagedSubscriptionDiscount(ManagedSubscriptionDiscount discount, DynWorkspacePublisherSubscription publisherSubscription)
    {
        var dynDiscount = discount.ToDynWorkspacePublisherSubscriptionDiscount(publisherSubscription);

        if (dynDiscount != null && dynDiscount.PercentOff > 0 && dynDiscount.EndsOn >= _dateTimeProvider.UtcNow.StartOfNextMonth().ToUnixTimestamp())
        {
            await _dynamoDb.PutItemAsync(dynDiscount);
        }
    }
}
