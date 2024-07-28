using Rydr.ActiveCampaign;
using Rydr.ActiveCampaign.Models;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Admin;
using Rydr.Api.Dto.Enums;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;
using Stripe;

namespace Rydr.Api.Services.Services;

public class CheckoutServiceInteral : BaseInternalOnlyApiService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IMapItemService _mapItemService;
    private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;
    private readonly IWorkspacePublisherSubscriptionService _workspacePublisherSubscriptionService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IPublisherAccountService _publisherAccountService;
    private readonly ISubscriptionPlanService _subscriptionPlanService;

    public CheckoutServiceInteral(IWorkspaceService workspaceService,
                                  IMapItemService mapItemService,
                                  IWorkspaceSubscriptionService workspaceSubscriptionService,
                                  IWorkspacePublisherSubscriptionService workspacePublisherSubscriptionService,
                                  IRydrDataService rydrDataService,
                                  IDeferRequestsService deferRequestsService,
                                  IPublisherAccountService publisherAccountService,
                                  ISubscriptionPlanService subscriptionPlanService)
    {
        _workspaceService = workspaceService;
        _mapItemService = mapItemService;
        _workspaceSubscriptionService = workspaceSubscriptionService;
        _workspacePublisherSubscriptionService = workspacePublisherSubscriptionService;
        _rydrDataService = rydrDataService;
        _deferRequestsService = deferRequestsService;
        _publisherAccountService = publisherAccountService;
        _subscriptionPlanService = subscriptionPlanService;
    }

    public async Task Post(CheckoutSubscriptionDeleted request)
    {
        var workspaceSubscription = await _dynamoDb.GetItemByEdgeIntoAsync<DynWorkspaceSubscription>(DynItemType.WorkspaceSubscription,
                                                                                                     DynWorkspaceSubscription.BuildEdgeId(request.StripeSubscriptionId),
                                                                                                     true);

        var workspace = await _workspaceService.TryGetWorkspaceAsync(workspaceSubscription?.SubscriptionWorkspaceId ?? 0);

        if (workspaceSubscription != null && !workspace.IsRydrWorkspace())
        {
            // Ensure the workspace subscription is deleted for the sub in question
            await _workspaceSubscriptionService.DeleteWorkspaceSubscriptionAsync(workspaceSubscription);

            return;
        }

        // No workspace subscription for this identifier, which means it is definitly not a payPerBusiness type subscription, probably managed
        var managedWorkspaceSubscription = await _workspacePublisherSubscriptionService.GetManagedPublisherSubscriptionAsync(request.StripeSubscriptionId);

        if (managedWorkspaceSubscription != null)
        {
            await _workspacePublisherSubscriptionService.DeletePublisherSubscriptionAsync(managedWorkspaceSubscription);
        }
    }

    public async Task Post(CheckoutSubscriptionCreated request)
    {
        var stripe = await StripeService.GetInstanceAsync();

        var stripeSubscription = await stripe.GetSubscriptionAsync(request.StripeSubscriptionId);

        if (stripeSubscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled))
        {
            return;
        }

        // Find the subscription type and sync the items up
        var monthlySubscriptionItem = await stripeSubscription.Items.FirstOrDefaultAsync(si => _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(si.Plan?.Id),
                                                                                         t => t != SubscriptionType.None);

        if (monthlySubscriptionItem == null)
        {
            // Something is wrong here
            _deferRequestsService.DeferLowPriRequest(new ValidateWorkspaceSubscription
                                                     {
                                                         StripeSubscriptionId = request.StripeSubscriptionId,
                                                         Message = "CheckoutSubscriptionCreated - no monthly subscription plan item could be found in the subscription"
                                                     });

            return;
        }

        var subscriptionType = await _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(monthlySubscriptionItem.Plan.Id);

        // Managed or unmanaged subscription
        if (subscriptionType.IsManagedSubscriptionType())
        {
            // Have to have a valid workspace and publisherAccount metadata
            stripeSubscription.Metadata.TryGetValue("WorkspaceId", out var managingWorkspaceId);
            stripeSubscription.Metadata.TryGetValue("PublisherAccountId", out var managedPublisherAccountId);

            var dynManagingWorkspace = await _workspaceService.TryGetWorkspaceAsync(managingWorkspaceId.ToLong(0));
            var dynManagedPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(managedPublisherAccountId.ToLong(0));

            if (dynManagingWorkspace == null || dynManagingWorkspace.IsDeleted() ||
                dynManagedPublisherAccount == null || dynManagedPublisherAccount.IsDeleted())
            {
                // Something is wrong here
                _deferRequestsService.DeferLowPriRequest(new ValidateWorkspaceSubscription
                                                         {
                                                             StripeSubscriptionId = request.StripeSubscriptionId,
                                                             Message = $"CheckoutSubscriptionCreated - managed subscriptionType [{subscriptionType.ToString()}] found but no managing workspace and/or managed publisher account found"
                                                         });

                return;
            }

            if (subscriptionType == SubscriptionType.ManagedCustom)
            { // If billed in arrears, we have to manually apply the first month pro-ration (for advance types, stripe calculates for us)
                await ApplyFirstMonthSubscriptionFeeProrationCreditAsync(stripeSubscription, monthlySubscriptionItem, dynManagingWorkspace, dynManagedPublisherAccount, stripe);
            }

            await ApplyDiscounts(stripeSubscription, dynManagingWorkspace, dynManagedPublisherAccount, stripe: stripe);

            // Add the new subscription, if it is new
            var existingPublisherSubscription = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(dynManagingWorkspace.Id, dynManagedPublisherAccount.PublisherAccountId);

            stripeSubscription.Metadata.TryGetValue("SubscriptionStartedOn", out var subscriptionStartedOnString);

            if ((existingPublisherSubscription?.StripeSubscriptionId).EqualsOrdinalCi(stripeSubscription.Id, false))
            {
                _log.DebugFormat("Existing subscription [{0}] was created from RYDR, ignoring SubscriptionCreated processing", stripeSubscription.Id);

                // If this subscription was back-dated, charge up any usage from start to now
                if (subscriptionStartedOnString.HasValue())
                {
                    var subscriptionStartedOn = subscriptionStartedOnString.ToDateTime();
                    var utcNow = _dateTimeProvider.UtcNow;

                    if (subscriptionStartedOn >= utcNow.StartOfMonth() && subscriptionStartedOn < utcNow.Date)
                    {
                        _deferRequestsService.DeferLowPriRequest(new ChargeCompletedUsage
                                                                 {
                                                                     WorkspaceIdentifier = dynManagingWorkspace.Id.ToStringInvariant(),
                                                                     PublisherIdentifier = dynManagedPublisherAccount.PublisherAccountId.ToStringInvariant(),
                                                                     StartDate = subscriptionStartedOn,
                                                                     EndDate = utcNow,
                                                                     ForceNowUsageTimestamp = true
                                                                 }.WithAdminRequestInfo());
                    }
                }
            }
            else
            {
                var customPerPostSubscriptionItem = stripeSubscription.Items.SingleOrDefault(si => si.Plan.ProductId.EqualsOrdinalCi(CustomSubscriptionPlanService.CustomPerPostProductId));

                var subscriptionCreated = await _workspacePublisherSubscriptionService.AddManagedSubscriptionAsync(dynManagingWorkspace.Id, dynManagedPublisherAccount, subscriptionType,
                                                                                                                   stripeSubscription.CustomerId, stripeSubscription.Id,
                                                                                                                   subscriptionStartedOnString.ToDateNullable(),
                                                                                                                   customMonthlyFee: Math.Round((monthlySubscriptionItem.Price?.UnitAmount ?? 0) / 100d, 2),
                                                                                                                   customPerPostFee: Math.Round((customPerPostSubscriptionItem?.Price?.UnitAmount ?? 0) / 100d, 2));

                if (!subscriptionCreated)
                {
                    _log.WarnFormat("SubscriptionCreated hook fired for subId [{0}] but could not create RYDR WorkspacePublisherSubscription.", stripeSubscription.Id);
                }
            }
        }

        // TODO: Handle adding a non-managed subscription at strip
    }

    public async Task Post(CheckoutSubscriptionUpdated request)
    {
        // Always ensure the subscription status in our workspaceSub is accurate
        var existingWorkspaceSubscription = await _dynamoDb.GetItemByEdgeIntoAsync<DynWorkspaceSubscription>(DynItemType.WorkspaceSubscription,
                                                                                                             DynWorkspaceSubscription.BuildEdgeId(request.StripeSubscriptionId),
                                                                                                             true);

        if (existingWorkspaceSubscription != null)
        {
            await ProcessPayPerBusinessSubscriptionUpdate(existingWorkspaceSubscription);

            return;
        }

        // No workspace subscription found matching the subscription id, probably a managed subscription
        // No workspace subscription for this identifier, which means it is definitly not a payPerBusiness type subscription, probably managed
        var managedWorkspaceSubscription = await _workspacePublisherSubscriptionService.GetManagedPublisherSubscriptionAsync(request.StripeSubscriptionId);

        if (managedWorkspaceSubscription == null || managedWorkspaceSubscription.IsDeleted())
        {
            return;
        }

        var stripe = await StripeService.GetInstanceAsync();

        var stripeSubscription = await stripe.GetSubscriptionAsync(request.StripeSubscriptionId);

        if (stripeSubscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled))
        { // Cancelled means delete our local one
            await _workspacePublisherSubscriptionService.DeletePublisherSubscriptionAsync(managedWorkspaceSubscription);

            return;
        }

        // Find the subscription type and sync the items up
        var monthlySubscriptionItem = await stripeSubscription.Items.FirstOrDefaultAsync(i => _subscriptionPlanService.IsManagedPlanIdAsync(i.Plan?.Id));

        if (monthlySubscriptionItem == null)
        {
            // Something is wrong here
            _deferRequestsService.DeferLowPriRequest(new ValidateWorkspaceSubscription
                                                     {
                                                         SubscriptionWorkspaceId = managedWorkspaceSubscription.SubscriptionWorkspaceId,
                                                         StripeSubscriptionId = request.StripeSubscriptionId,
                                                         Message = "CheckoutSubscriptionUpdated - no monthly subscription plan item could be found in the subscription"
                                                     });

            return;
        }

        var subscriptionType = await _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(monthlySubscriptionItem.Plan.Id);

        if (managedWorkspaceSubscription.SubscriptionType != subscriptionType)
        {
            managedWorkspaceSubscription.SubscriptionType = subscriptionType;
            managedWorkspaceSubscription.Dirty();
        }

        SubscriptionItem subscriptionItemToValidate = null;

        if (subscriptionType.IsManagedCustomPlan())
        {
            if (monthlySubscriptionItem.Price.UnitAmount.HasValue)
            {
                if (managedWorkspaceSubscription.CustomMonthlyFeeCents.GetValueOrDefault() != monthlySubscriptionItem.Price.UnitAmount.Value)
                {
                    managedWorkspaceSubscription.CustomMonthlyFeeCents = (int)monthlySubscriptionItem.Price.UnitAmount.Value;
                    managedWorkspaceSubscription.Dirty();
                }
            }

            foreach (var stripeItem in stripeSubscription.Items)
            {
                if (stripeItem.Price.ProductId.EqualsOrdinalCi(CustomSubscriptionPlanService.CustomPerPostProductId) && stripeItem.Price.UnitAmount.HasValue)
                {
                    if (managedWorkspaceSubscription.CustomPerPostFeeCents.GetValueOrDefault() != stripeItem.Price.UnitAmount.Value)
                    {
                        managedWorkspaceSubscription.CustomPerPostFeeCents = (int)stripeItem.Price.UnitAmount.Value;
                        managedWorkspaceSubscription.Dirty();
                    }
                }
                else if (!stripeItem.Id.EqualsOrdinalCi(monthlySubscriptionItem.Id))
                {
                    subscriptionItemToValidate = stripeItem;
                }
            }
        }
        else
        {
            var planIds = (await _subscriptionPlanService.GetPlanIdsForSubscriptionTypeAsync(subscriptionType)).AsListReadOnly();
            var stripePlanIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Ensure the managed subscription aligns
            foreach (var stripeItem in stripeSubscription.Items)
            {
                if (!planIds.Contains(stripeItem.Plan.Id) || !stripePlanIds.Add(stripeItem.Plan.Id) || stripeItem.Quantity > 1)
                {
                    subscriptionItemToValidate = stripeItem;

                    break;
                }
            }
        }

        if (subscriptionItemToValidate != null)
        { // Something wrong with this subscription
            _deferRequestsService.DeferLowPriRequest(new ValidateWorkspaceSubscription
                                                     {
                                                         SubscriptionWorkspaceId = managedWorkspaceSubscription.SubscriptionWorkspaceId,
                                                         StripeSubscriptionId = request.StripeSubscriptionId,
                                                         Message = $"CheckoutSubscriptionUpdated - subscription items contains invalid/unrecognized plan item [{subscriptionItemToValidate.Plan.Id}]"
                                                     });
        }

        if (managedWorkspaceSubscription.IsDirty)
        {
            await _workspacePublisherSubscriptionService.PutPublisherSubscriptionAsync(managedWorkspaceSubscription);
        }
    }

    public Task Post(CheckoutCustomerUpdated request)
        => OnCustomerUpsert(request.StripeCustomerId);

    public Task Post(CheckoutCustomerCreated request)
        => OnCustomerUpsert(request.StripeCustomerId);

    public async Task Post(CheckoutCustomerDeleted request)
    {
        var workspacePublisherIds = (await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<RydrWorkspacePublisherAccountId>(@"
SELECT   w.Id AS WorkspaceId, 0 AS PublisherAccountId
FROM     Workspaces w
WHERE    w.StripeCustomerId = @CustomerId
         AND NOT EXISTS
         (
         SELECT  NULL
         FROM    WorkspacePublisherSubscriptions wpsw
         WHERE   wpsw.WorkspaceId = w.Id
                 AND wpsw.DeletedOn IS NULL
         )
UNION
SELECT   ws.WorkspaceId AS WorkspaceId, 0 AS PublisherAccountId
FROM     WorkspaceSubscriptions ws
WHERE    ws.CustomerId = @CustomerId
         AND NOT EXISTS
         (
         SELECT  NULL
         FROM    WorkspacePublisherSubscriptions wpss
         WHERE   wpss.WorkspaceId = ws.Id
                 AND wpss.DeletedOn IS NULL
         )
UNION
SELECT   wps.WorkspaceId AS WorkspaceId, wps.PublisherAccountId AS PublisherAccountId
FROM     WorkspacePublisherSubscriptions wps
WHERE    wps.CustomerId IS NOT NULL
         AND wps.CustomerId = @CustomerId;
",
                                                                                                                                 new
                                                                                                                                 {
                                                                                                                                     CustomerId = request.StripeCustomerId
                                                                                                                                 }))
                                    ).AsListReadOnly();

        if (workspacePublisherIds.IsNullOrEmptyReadOnly())
        {
            return;
        }

        foreach (var workspacePublisherId in workspacePublisherIds.Where(w => w.WorkspaceId > 0))
        {
            if (workspacePublisherId.PublisherAccountId > 0)
            {
                var workspacePublisherSubscription = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(workspacePublisherId.WorkspaceId,
                                                                                                                                workspacePublisherId.PublisherAccountId);

                if (workspacePublisherSubscription != null)
                {
                    await _workspacePublisherSubscriptionService.DeletePublisherSubscriptionAsync(workspacePublisherSubscription);
                }
            }
            else
            {
                // Ensure the stripe info is cleared for this workspace
                var workspace = await _workspaceService.TryGetWorkspaceAsync(workspacePublisherId.WorkspaceId);

                if (!workspace.IsRydrWorkspace())
                {
                    if (workspace != null && workspace.StripeCustomerId.HasValue())
                    {
                        await _workspaceService.UpdateWorkspaceAsync(workspace, () => new DynWorkspace
                                                                                      {
                                                                                          StripeCustomerId = null
                                                                                      });
                    }

                    await _dynamoDb.FromQuery<DynWorkspaceSubscription>(s => s.Id == workspacePublisherId.WorkspaceId &&
                                                                             Dynamo.BeginsWith(s.EdgeId, DynWorkspaceSubscription.EdgeStartsWith))
                                   .Filter(s => s.TypeId == (int)DynItemType.WorkspaceSubscription &&
                                                s.DeletedOnUtc == null)
                                   .QueryAsync(_dynamoDb)
                                   .Where(s => !s.IsDeleted())
                                   .EachAsync(s => _workspaceSubscriptionService.DeleteWorkspaceSubscriptionAsync(s));
                }
            }
        }
    }

    public async Task Post(CheckoutCompleted request)
    {
        var workspaceId = request.ClientReferenceId.ToLong(0);

        var workspace = await _workspaceService.TryGetWorkspaceAsync(workspaceId);

        if (workspace == null || workspace.IsDeleted())
        {
            _log.WarnFormat("CheckoutCompleted invalid workspace, does not exist or is deleted, workspaceId [{0}], request info [{1}]", workspaceId, request.ToJsv().Left(500));

            return;
        }

        var sessionMap = await _mapItemService.TryGetMapAsync(workspaceId, DynItemMap.BuildEdgeId(DynItemType.WorkspaceSubscription, request.CheckoutSessionId));
        Guard.AgainstRecordNotFound(sessionMap == null, $"CheckoutCompleted invalid info, no session map found for sessionId [{request.CheckoutSessionId}], request info [{request.ToJsv().Left(500)}]");

        var existingSubscriptionId = sessionMap.Items.IsNullOrEmptyRydr() || !sessionMap.Items.ContainsKey("ExistingSubscriptionId")
                                         ? null
                                         : sessionMap.Items["ExistingSubscriptionId"].ToNullIfEmpty();

        var dynWorkspaceSubscription = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(workspace.Id);

        if (dynWorkspaceSubscription.IsSystemSubscription)
        { // For processing purposes, if the active subscription is a system subscription, we treat it as non-existent
            dynWorkspaceSubscription = null;
        }

        // Existing subscription, if present, must match the workspace
        // If we have a setupIntent value, we must also have an existing subscription
        Guard.AgainstInvalidData(!(dynWorkspaceSubscription?.SubscriptionId).EqualsOrdinalCi(existingSubscriptionId), "Subscription identifiers must match if present - workspace/existing");
        Guard.AgainstInvalidData(dynWorkspaceSubscription != null && request.StripeSubscriptionId.HasValue() && !dynWorkspaceSubscription.SubscriptionId.EqualsOrdinalCi(request.StripeSubscriptionId), "Subscription identifiers must match if present = workspace/request");
        Guard.AgainstInvalidData(request.StripeSetupIntentId.HasValue() && existingSubscriptionId.IsNullOrEmpty(), "SetupIntent present, missing existing subscription identifier");

        if (request.StripeSetupIntentId.HasValue())
        {
            await UpdateExistingSubscriptionPaymentMethodAsync(request.StripeSetupIntentId, dynWorkspaceSubscription.SubscriptionCustomerId, existingSubscriptionId);
        }
        else
        {
            var pubAccountIds = sessionMap.Items["PublisherAccountIds"].Split(",");

            dynWorkspaceSubscription = await ProcessNewPayBerBusinessSubscriptionAsync(pubAccountIds.Select(s => s.ToLong(0)).AsListReadOnly(),
                                                                                       request.StripeSubscriptionId, workspace, dynWorkspaceSubscription);
        }

        // Remove the session map
        await _mapItemService.DeleteMapAsync(sessionMap.Id, sessionMap.EdgeId);

        // Update any invoice items that may have won a race into the db for this new subscription
        await _rydrDataService.ExecAdHocAsync(@"
UPDATE   Invoices 
SET      WorkspaceId = @WorkspaceId, 
         WorkspaceSubscriptionId = @WorkspaceSubscriptionId
WHERE    WorkspaceId <= 0
         AND WorkspaceSubscriptionId <= 0
         AND CustomerId = @CustomerId
         AND SubscriptionId = @SubscriptionId;
",
                                              new
                                              {
                                                  WorkspaceId = dynWorkspaceSubscription.SubscriptionWorkspaceId,
                                                  WorkspaceSubscriptionId = dynWorkspaceSubscription.DynWorkspaceSubscriptionId,
                                                  CustomerId = dynWorkspaceSubscription.SubscriptionCustomerId,
                                                  dynWorkspaceSubscription.SubscriptionId
                                              });
    }

    public async Task Post(CheckoutApplyInvoiceDiscounts request)
    {
        var stripe = await StripeService.GetInstanceAsync();

        var stripeSubscription = await stripe.GetSubscriptionAsync(request.StripeSubscriptionId);

        var stripeInvoice = request.StripeInvoiceId.IsNullOrEmpty()
                                ? null
                                : await stripe.GetInvoiceAsync(request.StripeInvoiceId);

        if (stripeSubscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled) &&
            (stripeInvoice == null || !stripeInvoice.Status.EqualsOrdinalCi("draft")))
        {
            return;
        }

        stripeSubscription.Metadata.TryGetValue("WorkspaceId", out var managingWorkspaceId);
        stripeSubscription.Metadata.TryGetValue("PublisherAccountId", out var managedPublisherAccountId);

        var dynManagingWorkspace = await _workspaceService.TryGetWorkspaceAsync(managingWorkspaceId.ToLong(0));
        var dynManagedPublisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(managedPublisherAccountId.ToLong(0));

        await ApplyDiscounts(stripeSubscription, dynManagingWorkspace, dynManagedPublisherAccount, request.StripeInvoiceId, stripe);
    }

    private async Task UpdateExistingSubscriptionPaymentMethodAsync(string stripeSetupIntentId, string stripeCustomerId, string stripeSubscriptionId)
    {
        var stripe = await StripeService.GetInstanceAsync();

        var setupIntent = await stripe.GetSetupIntent(stripeSetupIntentId);

        var stripeCustomerCards = await stripe.GetCustomerCreditCardsAsync(stripeCustomerId);

        // Do any of the customers stored cards already match the one being added/updated? If so, nothing to do
        if ((setupIntent.PaymentMethod.Card?.Fingerprint).HasValue() &&
            !stripeCustomerCards.IsNullOrEmpty() &&
            stripeCustomerCards.Any(pc => pc.Fingerprint.EqualsOrdinal(setupIntent.PaymentMethod.Card.Fingerprint) &&
                                          pc.ExpMonth == setupIntent.PaymentMethod.Card.ExpMonth &&
                                          pc.ExpYear == setupIntent.PaymentMethod.Card.ExpYear &&
                                          pc.AddressZip.EqualsOrdinalCi(setupIntent.PaymentMethod.BillingDetails.Address.PostalCode)))
        {
            _log.DebugInfo("Not updated existing subscription payment method, cards match");

            return;
        }

        // Attach the payment to the customer
        await stripe.AttachPaymentMethodAsync(setupIntent.PaymentMethodId, stripeCustomerId);

        // Set this new payment as the default
        await stripe.UpdateSubscriptionDefaultPaymentMethodAsync(stripeSubscriptionId, setupIntent.PaymentMethodId);
    }

    private async Task<DynWorkspaceSubscription> ProcessNewPayBerBusinessSubscriptionAsync(IReadOnlyList<long> publisherAccountIds, string stripeSubscriptionId,
                                                                                           DynWorkspace workspace, DynWorkspaceSubscription existingWorkspaceSubscription)
    {
        var stripe = await StripeService.GetInstanceAsync();

        var stripeSubscription = await stripe.GetSubscriptionAsync(stripeSubscriptionId);

        Guard.AgainstRecordNotFound(stripeSubscription == null, $"CheckoutCompleted invalid stripe info, no subsription found at stripe for subscriptionId [{stripeSubscriptionId}]");

        var stripeCustomerId = (existingWorkspaceSubscription?.SubscriptionCustomerId).Coalesce(stripeSubscription.CustomerId);

        Guard.AgainstInvalidData(!stripeSubscription.CustomerId.EqualsOrdinalCi(stripeCustomerId), $"Mismatched workspace [{stripeCustomerId}] and stripe [{stripeSubscription.Customer}] customer identifiers");

        // Update the stripe customer and subscription info....
        await stripe.UpdateCustomerMetaDataAsync(stripeCustomerId, new Dictionary<string, string>
                                                                   {
                                                                       {
                                                                           "WorkspaceId", workspace.Id.ToStringInvariant()
                                                                       },
                                                                       {
                                                                           "WorkspaceEmail", await _workspaceService.TryGetWorkspacePrimaryEmailAddressAsync(workspace.Id)
                                                                       }
                                                                   });

        var managedPlanItem = await _subscriptionPlanService.GetManagedPlanSubscriptionItemAsync(stripeSubscription);

        // Store our copy of the workspace subscription and publisher subs
        var subscriptionType = workspace.WorkspaceType == WorkspaceType.Admin
                                   ? SubscriptionType.Unlimited
                                   : stripeSubscription.ToSubscriptionType(managedPlanItem);

        var dynWorkspaceSubscription = stripeSubscription.ToDynWorkspaceSubscription(managedPlanItem, workspace.Id, existingWorkspaceSubscription, subscriptionType);

        await _workspaceSubscriptionService.PutWorkspaceSubscriptionAsync(dynWorkspaceSubscription, true);

        // Deal with the publisher subscriptions
        foreach (var publisherAccountId in publisherAccountIds)
        {
            var dynWorkspacePublisherSubscription = dynWorkspaceSubscription.ToDynWorkspacePublisherSubscription(publisherAccountId, SubscriptionType.PayPerBusiness);

            if (dynWorkspacePublisherSubscription.IsDeleted())
            {
                await _workspacePublisherSubscriptionService.DeletePublisherSubscriptionAsync(dynWorkspacePublisherSubscription);
            }
            else
            {
                await _workspacePublisherSubscriptionService.PutPublisherSubscriptionAsync(dynWorkspacePublisherSubscription);
            }
        }

        return dynWorkspaceSubscription;
    }

    private async Task ProcessPayPerBusinessSubscriptionUpdate(DynWorkspaceSubscription existingWorkspaceSubscription)
    {
        if (existingWorkspaceSubscription.IsDeleted())
        {
            return;
        }

        var stripe = await StripeService.GetInstanceAsync();

        var stripeSubscription = await stripe.GetSubscriptionAsync(existingWorkspaceSubscription.SubscriptionId);

        if (stripeSubscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled))
        { // Cancelled means delete our local one
            await _workspaceSubscriptionService.DeleteWorkspaceSubscriptionAsync(existingWorkspaceSubscription);

            return;
        }

        var managedPlanItem = await _subscriptionPlanService.GetManagedPlanSubscriptionItemAsync(stripeSubscription);

        if ((managedPlanItem?.Quantity).Gz(0) == existingWorkspaceSubscription.Quantity)
        { // Update what we have for subscription attributes and we're all done
            var workspaceSubscription = stripeSubscription.ToDynWorkspaceSubscription(managedPlanItem, existingWorkspaceSubscription.SubscriptionWorkspaceId, existingWorkspaceSubscription);

            await _workspaceSubscriptionService.PutWorkspaceSubscriptionAsync(workspaceSubscription);
        }
        else
        { // No quantity match, so validate things are correct and adjust
            _deferRequestsService.DeferLowPriRequest(new ValidateWorkspaceSubscription
                                                     {
                                                         SubscriptionWorkspaceId = existingWorkspaceSubscription.SubscriptionWorkspaceId,
                                                         StripeSubscriptionId = existingWorkspaceSubscription.SubscriptionId,
                                                         Message = "ProcessPayPerBusinessSubscriptionUpdate - Quantity invalid/unmatched to workspace subscription"
                                                     });
        }
    }

    private async Task OnCustomerUpsert(string stripeCustomerId)
    {
        if (stripeCustomerId.IsNullOrEmpty())
        {
            return;
        }

        var stripe = await StripeService.GetInstanceAsync();
        var stripeCustomer = await stripe.GetCustomerAsync(stripeCustomerId);

        if ((stripeCustomer?.Id).IsNullOrEmpty())
        {
            _log.WarnFormat("StripeCustomer created with unknown StripeCustomerId [{0}]", stripeCustomerId);

            return;
        }

        var workspaceId = stripeCustomer.Metadata.GetValueOrDefault("WorkspaceId").ToLong(0);
        var managedPublisherAccountId = stripeCustomer.Metadata.GetValueOrDefault("PublisherAccountId").ToLong(0);

        var workspace = await _workspaceService.TryGetWorkspaceAsync(workspaceId);

        if (workspace == null)
        {
            _log.WarnFormat("StripeCustomer created with unknown workspace id - customerId [{0}], workspaceId [{1}]", stripeCustomerId, workspaceId);

            return;
        }

        var publisherAccountSubscription = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(workspace.Id, managedPublisherAccountId);

        // Add this customer identifier to active campaign if possible
        var acClient = ActiveCampaignClientFactory.Instance.GetOrCreateRydrClient();

        var acContactId = (publisherAccountSubscription?.ActiveCampaignCustomerId).Coalesce(workspace.ActiveCampaignCustomerId);

        if (acContactId.IsNullOrEmpty())
        {
            var workspaceEmail = await _workspaceService.TryGetWorkspacePrimaryEmailAddressAsync(workspace.Id);
            var customerEmail = stripeCustomer.Email.Coalesce(workspaceEmail);

            var acContact = customerEmail.HasValue()
                                ? await acClient.GetContactByEmailAsync(customerEmail)
                                : null;

            acContactId = acContact?.Id;
        }

        if (publisherAccountSubscription != null)
        {
            var updatePublisher = false;

            if (acContactId.HasValue() && !publisherAccountSubscription.ActiveCampaignCustomerId.EqualsOrdinalCi(acContactId))
            {
                publisherAccountSubscription.ActiveCampaignCustomerId = acContactId;
                updatePublisher = true;
            }

            if (!publisherAccountSubscription.StripeCustomerId.EqualsOrdinalCi(stripeCustomer.Id))
            {
                publisherAccountSubscription.StripeCustomerId = stripeCustomer.Id;
                updatePublisher = true;
            }

            if (updatePublisher)
            {
                await _workspacePublisherSubscriptionService.PutPublisherSubscriptionAsync(publisherAccountSubscription);
            }
        }

        var updateWorkspace = false;

        if (acContactId.HasValue() && !workspace.ActiveCampaignCustomerId.EqualsOrdinalCi(acContactId))
        {
            workspace.ActiveCampaignCustomerId = acContactId;
            updateWorkspace = true;
        }

        if ((publisherAccountSubscription == null || !publisherAccountSubscription.SubscriptionType.IsManagedSubscriptionType()) &&
            !workspace.StripeCustomerId.EqualsOrdinalCi(stripeCustomer.Id))
        {
            workspace.StripeCustomerId = stripeCustomer.Id;
            updateWorkspace = true;
        }

        if (updateWorkspace)
        {
            await _workspaceService.UpdateWorkspaceAsync(workspace, () => new DynWorkspace
                                                                          {
                                                                              ActiveCampaignCustomerId = workspace.ActiveCampaignCustomerId,
                                                                              StripeCustomerId = workspace.StripeCustomerId
                                                                          });
        }

        if (acContactId.HasValue())
        {
            try
            {
                await acClient.PostContactCustomFieldValueAsync(new AcContactCustomFieldValue
                                                                {
                                                                    Contact = acContactId,
                                                                    Field = "StripeCustomerId",
                                                                    Value = stripeCustomer.Id
                                                                });
            }
            catch(Exception x)
            {
                _log.Exception(x);
            }
        }
    }

    private async Task ApplyDiscounts(Subscription stripeSubscription, DynWorkspace managingWorkspace, DynPublisherAccount managedPublisherAccount,
                                      string stripeInvoiceId = null, StripeService stripe = null)
    {
        var workspaceSubscription = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(managingWorkspace?.Id ?? 0);
        var workspacePublisherSubscription = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(managingWorkspace?.Id ?? 0, managedPublisherAccount?.PublisherAccountId ?? 0);

        if (!workspaceSubscription.IsValid() || !workspacePublisherSubscription.IsValid() ||
            !workspaceSubscription.SubscriptionType.IsAgencySubscriptionType() || !workspacePublisherSubscription.SubscriptionType.IsManagedSubscriptionType())
        {
            _log.InfoFormat("Cannot process ApplyDiscounts for subscription [{0}], no valid agency workspace subscription and/or managed publisher subscription.", stripeSubscription.Id);

            return;
        }

        var discounts = await _dynamoDb.FromQuery<DynWorkspacePublisherSubscriptionDiscount>(d => d.Id == workspaceSubscription.DynWorkspaceSubscriptionId &&
                                                                                                  Dynamo.BeginsWith(d.EdgeId,
                                                                                                                    string.Concat((int)DynItemType.WorkspacePublisherSubscriptionDiscount,
                                                                                                                                  "|", managedPublisherAccount.PublisherAccountId, "|")))
                                       .Filter(d => d.DeletedOnUtc == null &&
                                                    d.PercentOff > 0 &&
                                                    d.PercentOff <= 100)
                                       .QueryAsync(_dynamoDb)
                                       .ToListReadOnly();

        if (discounts.IsNullOrEmptyReadOnly())
        {
            return;
        }

        var nowTs = _dateTimeProvider.UtcNowTs;

        var stripeInvoice = stripeInvoiceId.IsNullOrEmpty()
                                ? await stripe.GetUpcomingInvoiceAsync(stripeSubscription.Id)
                                : await stripe.GetInvoiceAsync(stripeInvoiceId);

        if (stripeInvoice == null || !stripeInvoice.SubscriptionId.EqualsOrdinalCi(stripeSubscription.Id) || !stripeInvoice.Status.EqualsOrdinalCi("draft"))
        {
            _log.DebugInfoFormat("No invoice specified, no upcoming invoice found, invoice is non-writable status, or mismatching subscription identifiers - cannot apply discounts for subscription [{0}], invoice subscription [{1}], invoice status [{2}]",
                                 stripeSubscription.Id, stripeInvoice?.SubscriptionId ?? "NULL", stripeInvoice?.Status ?? "NULL");

            return;
        }

        foreach (var discount in discounts)
        {
            var discountCreditUsageType = discount.UsageType.GetCreditTypeForUsage();

            if (discountCreditUsageType == SubscriptionUsageType.None)
            {
                _log.WarnFormat("No matching Credit UsageType for discount UsageType of [{0}], cannot process discount", discount.UsageType);

                continue;
            }

            var subscriptionDiscountItemPlanId = await _subscriptionPlanService.GetSubscriptionPlanIdForUsageTypeAsync(workspacePublisherSubscription.SubscriptionType, discount.UsageType,
                                                                                                                       workspacePublisherSubscription.CustomMonthlyFeeCents ?? 0,
                                                                                                                       workspacePublisherSubscription.CustomPerPostFeeCents ?? 0);

            if (subscriptionDiscountItemPlanId.IsNullOrEmpty())
            {
                continue;
            }

            var discountInvoiceItem = stripeInvoice.Lines.SingleOrDefault(ii => subscriptionDiscountItemPlanId.EqualsOrdinalCi(ii.Price?.Id));

            var discountInvoiceItemAmountCents = (discountInvoiceItem?.Amount).Gz(0);

            if (discountInvoiceItemAmountCents <= 0)
            {
                _log.DebugInfo("No invoice item found, or invoice item has 0 amount already");

                continue;
            }

            var discountStartDate = discount.StartsOn.ToDateTime();
            var discountEndDate = discount.EndsOn.ToDateTime();
            var invoicePeriodStartDate = stripeInvoice.PeriodStart.ToUniversalTime();

            if (invoicePeriodStartDate < discountStartDate || invoicePeriodStartDate >= discountEndDate)
            {
                _log.DebugInfoFormat("Discount for usage [{0}] found, but invoice period start of [{1}] is outside discount start/end of [{2}]-[{3}]",
                                     discount.UsageType.ToString(), invoicePeriodStartDate.ToSqlDateString(), discountStartDate.ToSqlDateString(), discountEndDate.ToSqlDateString());

                continue;
            }

            var lineItemDescription = string.Concat("Monthly discount usage ", (int)discount.UsageType);
            var existingDiscountLineItem = stripeInvoice.Lines.SingleOrDefault(l => l.Amount < 0 && l.Description.StartsWithOrdinalCi(lineItemDescription));

            var creditAmountCents = discountInvoiceItemAmountCents * (discount.PercentOff >= 100
                                                                          ? 1
                                                                          : (discount.PercentOff / 100.0)) * -1;

            _log.InfoFormat("Applying or updating discount to subscription [{0}], invoice [{1}], in the amount of [{2}] - managing workspace [{3}] managed PublisherAccount [{4}].",
                            stripeSubscription.Id, stripeInvoice.Id, Math.Round(creditAmountCents / 100.0, 2), managingWorkspace.Id, managedPublisherAccount.DisplayName());

            // Add a negative invoice line item to credit the amount of unused monthly service
            if (existingDiscountLineItem == null)
            {
                await stripe.CreateInvoiceLineItem(stripeSubscription.CustomerId, stripeInvoice.Id, stripeSubscription.Id, (int)creditAmountCents,
                                                   lineItemDescription, string.Concat(lineItemDescription, " - ", stripeInvoice.Id));
            }
            else
            {
                await stripe.UpdateInvoiceLineItem(existingDiscountLineItem.Id, (int)creditAmountCents, lineItemDescription);
            }

            _deferRequestsService.DeferLowPriRequest(new SubscriptionUsageIncremented
                                                     {
                                                         UsageType = discountCreditUsageType,
                                                         SubscriptionWorkspaceId = managingWorkspace.Id,
                                                         WorkspaceSubscriptionId = workspaceSubscription?.Id ?? 0,
                                                         ManagedPublisherAccountId = managedPublisherAccount.PublisherAccountId,
                                                         SubscriptionId = stripeSubscription.Id,
                                                         UsageTimestamp = nowTs,
                                                         CustomerId = stripeSubscription.CustomerId,
                                                         WorkspaceSubscriptionType = workspaceSubscription.SubscriptionType,
                                                         PublisherSubscriptionType = workspacePublisherSubscription.SubscriptionType,
                                                         MonthOfService = invoicePeriodStartDate,
                                                         Amount = (int)creditAmountCents
                                                     }.WithAdminRequestInfo());
        }
    }

    private async Task ApplyFirstMonthSubscriptionFeeProrationCreditAsync(Subscription stripeSubscription, SubscriptionItem monthlySubscriptionItem,
                                                                          DynWorkspace managingWorkspace, DynPublisherAccount managedPublisherAccount,
                                                                          StripeService stripe = null)
    {
        if (!stripeSubscription.Metadata.TryGetValue("SubscriptionStartedOn", out var subscriptionStartedOnString) ||
            subscriptionStartedOnString.IsNullOrEmpty())
        {
            _log.DebugInfoFormat("Not applying first month subscription prorated credit to subscription [{0}] - invalid or missing SubscriptionStartedOn", stripeSubscription.Id);

            return;
        }

        var nowUtc = _dateTimeProvider.UtcNow;
        var startOfNextMonth = nowUtc.StartOfNextMonth();
        var startOfCurrentMonth = startOfNextMonth.AddMonths(-1);

        var subscriptionStartedOn = subscriptionStartedOnString.ToDateTime();

        // If the sub start is invalid OR before/exactly at the start of the current month we're in, nothing to do
        if (subscriptionStartedOn <= startOfCurrentMonth)
        {
            _log.DebugInfoFormat("Not applying first month subscription prorated credit to subscription [{0}] - SubscriptionStartedOn before or exactly at current month, nothing to pro-rate", stripeSubscription.Id);

            return;
        }

        if (subscriptionStartedOn >= startOfNextMonth)
        {
            _log.DebugInfoFormat("Not applying first month subscription prorated credit to subscription [{0}] - SubscriptionStartedOn >= next month", stripeSubscription.Id);

            return;
        }

        if (stripeSubscription.Created < startOfCurrentMonth || stripeSubscription.Created > startOfNextMonth)
        {
            _log.DebugInfoFormat("Not applying first month subscription prorated credit to subscription [{0}] - subscription.created timestamp < current month OR > next month", stripeSubscription.Id);

            return;
        }

        monthlySubscriptionItem ??= await stripeSubscription.Items.FirstOrDefaultAsync(si => _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(si.Price?.Id),
                                                                                       t => t != SubscriptionType.None);

        // Managed subscription created, add an offsetting line item to the first invoice to account for pro-rating of the first month of service
        // only if not backdated
        // only if meta BillingStartedOn in current month
        // move elsewhere
        var monthlySubscriptionPriceCents = (monthlySubscriptionItem?.Price.UnitAmount).Gz(0);

        if (monthlySubscriptionPriceCents <= 0)
        {
            _log.DebugInfoFormat("Not applying first month subscription prorated credit to subscription [{0}] - No monthly subscription item with valid price found", stripeSubscription.Id);

            return;
        }

        var total = (startOfNextMonth - startOfCurrentMonth).TotalMinutes;
        var creditTime = (subscriptionStartedOn - startOfCurrentMonth).TotalMinutes;
        var creditAmountCents = monthlySubscriptionPriceCents * (creditTime / total) * -1;

        stripe ??= await StripeService.GetInstanceAsync();

        var invoice = stripeSubscription.LatestInvoiceId.IsNullOrEmpty()
                          ? await stripe.GetUpcomingInvoiceAsync(stripeSubscription.Id)
                          : await stripe.GetInvoiceAsync(stripeSubscription.LatestInvoiceId);

        if (invoice == null || !invoice.Lines.Any(l => l.Amount < 0 && l.Description.Contains("Initial month service fee credit")))
        {
            _log.InfoFormat("Applying first month subscription proration credit to subscription [{0}] in the amount of [{1}] - managing workspace [{2}] managed PublisherAccount [{3}].",
                            stripeSubscription.Id, Math.Round(creditAmountCents / 100.0, 2), managingWorkspace.Id, managedPublisherAccount.DisplayName());

            var workspaceSubscription = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(managingWorkspace.Id);

            // Add a negative invoice line item to credit the amount of unused monthly service
            await stripe.CreateInvoiceLineItem(stripeSubscription.CustomerId, invoice?.Id, stripeSubscription.Id, (int)creditAmountCents,
                                               "Initial month service fee credit", string.Concat("init_month_fee_credit_", (invoice?.Id).Coalesce(stripeSubscription.Id)));

            _deferRequestsService.DeferLowPriRequest(new SubscriptionUsageIncremented
                                                     {
                                                         UsageType = SubscriptionUsageType.SubscriptionFeeCredit,
                                                         SubscriptionWorkspaceId = managingWorkspace.Id,
                                                         WorkspaceSubscriptionId = workspaceSubscription?.Id ?? 0,
                                                         ManagedPublisherAccountId = managedPublisherAccount.PublisherAccountId,
                                                         SubscriptionId = stripeSubscription.Id,
                                                         UsageTimestamp = nowUtc.ToUnixTimestamp(),
                                                         CustomerId = stripeSubscription.CustomerId,
                                                         WorkspaceSubscriptionType = workspaceSubscription?.SubscriptionType ?? SubscriptionType.None,
                                                         PublisherSubscriptionType = await _subscriptionPlanService.GetSubscriptionTypeForPlanIdAsync(monthlySubscriptionItem.Price.Id),
                                                         MonthOfService = startOfCurrentMonth,
                                                         Amount = (int)creditAmountCents
                                                     }.WithAdminRequestInfo());
        }
    }

    private class RydrWorkspacePublisherAccountId
    {
        public long WorkspaceId { get; }
        public long PublisherAccountId { get; }
    }
}
