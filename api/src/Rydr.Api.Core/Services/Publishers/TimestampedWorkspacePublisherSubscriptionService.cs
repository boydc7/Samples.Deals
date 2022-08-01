using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.OrmLite.Dapper;
using Stripe;

namespace Rydr.Api.Core.Services.Publishers
{
    public class TimestampedWorkspacePublisherSubscriptionService : TimestampCachedServiceBase<DynWorkspacePublisherSubscription>, IWorkspacePublisherSubscriptionService
    {
        private readonly IPocoDynamo _dynamoDb;
        private readonly IAuthorizeService _authorizeService;
        private readonly IDeferRequestsService _deferRequestsService;
        private readonly IDistributedLockService _distributedLockService;
        private readonly IRydrDataService _rydrDataService;
        private readonly ISubscriptionPlanService _subscriptionPlanService;

        public TimestampedWorkspacePublisherSubscriptionService(ICacheClient cacheClient, IPocoDynamo dynamoDb,
                                                                IAuthorizeService authorizeService, IDeferRequestsService deferRequestsService,
                                                                IDistributedLockService distributedLockService, IRydrDataService rydrDataService,
                                                                ISubscriptionPlanService subscriptionPlanService)
            : base(cacheClient, 1800)
        {
            _dynamoDb = dynamoDb;
            _authorizeService = authorizeService;
            _deferRequestsService = deferRequestsService;
            _distributedLockService = distributedLockService;
            _rydrDataService = rydrDataService;
            _subscriptionPlanService = subscriptionPlanService;
        }

        public async Task<DynWorkspacePublisherSubscription> GetManagedPublisherSubscriptionAsync(string subscriptionId)
        {
            var workspacePublisherId = (await _rydrDataService.QueryAdHocAsync(db => db.QueryAsync<RydrWorkspacePublisherAccountId>(@"
SELECT   wps.WorkspaceId AS WorkspaceId, wps.PublisherAccountId AS PublisherAccountId
FROM     WorkspacePublisherSubscriptions wps
WHERE    wps.SubscriptionId IS NOT NULL
         AND wps.SubscriptionId = @SubscriptionId;
",
                                                                                                                                    new
                                                                                                                                    {
                                                                                                                                        SubscriptionId = subscriptionId
                                                                                                                                    }))
                                        ).SingleOrDefault();

            if (workspacePublisherId == null)
            {
                return null;
            }

            var dynWorkspacePublisherSubscription = await GetPublisherSubscriptionAsync(workspacePublisherId.WorkspaceId, workspacePublisherId.PublisherAccountId);

            return dynWorkspacePublisherSubscription.SubscriptionType.IsManagedSubscriptionType()
                       ? dynWorkspacePublisherSubscription
                       : null;
        }

        public Task<DynWorkspacePublisherSubscription> GetPublisherSubscriptionAsync(long workspaceId, long publisherAccountId)
        {
            if (workspaceId <= 0 || publisherAccountId <= 0)
            {
                return Task.FromResult<DynWorkspacePublisherSubscription>(null);
            }

            return GetModelAsync(string.Concat(workspaceId, "|", publisherAccountId),
                                 () => _dynamoDb.GetItemAsync<DynWorkspacePublisherSubscription>(workspaceId, DynWorkspacePublisherSubscription.BuildEdgeId(publisherAccountId)));
        }

        public async Task<DynWorkspacePublisherSubscription> TryGetPublisherSubscriptionConsistentAsync(long workspaceId, string edgeId)
        {
            var dynWorkspacePublisherSubscription = await _dynamoDb.GetItemAsync<DynWorkspacePublisherSubscription>(workspaceId, edgeId, true);

            SetModel(string.Concat(workspaceId, "|", DynItem.GetFinalEdgeSegment(edgeId)), dynWorkspacePublisherSubscription);

            return dynWorkspacePublisherSubscription;
        }

        public Task PutPublisherSubscriptionsAsync(IEnumerable<DynWorkspacePublisherSubscription> source)
            => _dynamoDb.PutItemsDeferAsync(source.Select(s =>
                                                          {
                                                              FlushModel(string.Concat(s.SubscriptionWorkspaceId, "|", s.PublisherAccountId));

                                                              return s;
                                                          }),
                                            RecordType.WorkspacePublisherSubscription);

        public async Task PutPublisherSubscriptionAsync(DynWorkspacePublisherSubscription source)
        {
            await _dynamoDb.PutItemDeferAsync(source, RecordType.WorkspacePublisherSubscription);

            FlushModel(string.Concat(source.SubscriptionWorkspaceId, "|", source.PublisherAccountId));
        }

        public async Task DeletePublisherSubscriptionAsync(DynWorkspacePublisherSubscription source)
        {
            await _dynamoDb.SoftDeleteAsync(source, UserAuthInfo.AdminAuthInfo);

            _deferRequestsService.DeferDealRequest(new WorkspacePublisherSubscriptionDeleted
                                                   {
                                                       SubscriptionWorkspaceId = source.SubscriptionWorkspaceId,
                                                       PublisherAccountId = source.PublisherAccountId,
                                                       DynWorkspaceSubscriptionId = source.DynWorkspaceSubscriptionId
                                                   });

            FlushModel(string.Concat(source.SubscriptionWorkspaceId, "|", source.PublisherAccountId));
        }

        public async Task CancelSubscriptionAsync(long workspaceId, long publisherAccountId)
        {
            var publisherSubscription = await GetPublisherSubscriptionAsync(workspaceId, publisherAccountId);

            if (!publisherSubscription.IsValid())
            {
                return;
            }

            if (publisherSubscription.SubscriptionType.IsManagedSubscriptionType())
            {   // Managed subscription - delete the subscription for this publisher at stripe
                using(var lockItem = _distributedLockService.TryGetKeyLock(publisherSubscription.SubscriptionWorkspaceId.ToStringInvariant(), nameof(DynWorkspaceSubscription), 60))
                {
                    if (lockItem == null)
                    {
                        throw new ResourceUnvailableException("Subscription currently locked by another process");
                    }

                    var stripe = await StripeService.GetInstanceAsync();

                    var existingSubscription = await stripe.GetSubscriptionAsync(publisherSubscription.StripeSubscriptionId);

                    if (existingSubscription != null && !existingSubscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled))
                    {
                        await stripe.CancelSubscriptionAsync(publisherSubscription.StripeSubscriptionId);
                    }

                    await DeletePublisherSubscriptionAsync(publisherSubscription);
                }
            }
            else
            {   // Pay per business then...
                await AddRemovePayPerBusinessSubscriptionAsync(workspaceId, publisherAccountId.AsEnumerable().AsListReadOnly(), true);
            }
        }

        public Task AddSubscribedPayPerBusinessPublisherAccountsAsync(long workspaceId, IReadOnlyList<long> publisherAccountIds)
            => AddRemovePayPerBusinessSubscriptionAsync(workspaceId, publisherAccountIds, false);

        public async Task<bool> AddManagedSubscriptionAsync(long workspaceId, DynPublisherAccount publisherAccount, SubscriptionType subscriptionType,
                                                            string stripeCustomerId, string newStripeSubscriptionId = null,
                                                            DateTime? backdateTo = null, string rydrEmployeeSig = null, string rydrEmployeeLogin = null,
                                                            double customMonthlyFee = 0, double customPerPostFee = 0)
        {
            Guard.AgainstArgumentOutOfRange(!subscriptionType.IsManagedSubscriptionType(), nameof(subscriptionType));

            var dynWorkspace = await WorkspaceService.DefaultWorkspaceService.GetWorkspaceAsync(workspaceId);

            // Get the subscription so we can get a consistent version later
            var workspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                              .TryGetActiveWorkspaceSubscriptionAsync(dynWorkspace.Id);

            if (!workspaceSubscription.IsValid())
            { // Already invalid/deleted subscription? if so, cancel the entire subscription and be done
                await WorkspaceService.DefaultWorkspaceSubscriptionService
                                      .DeleteWorkspaceSubscriptionAsync(workspaceSubscription);

                return false;
            }

            var existingPublisherSubscription = await GetPublisherSubscriptionAsync(dynWorkspace.Id, publisherAccount.PublisherAccountId);

            Guard.AgainstInvalidData(existingPublisherSubscription != null && !existingPublisherSubscription.IsDeleted(),
                                     "Managed business subscription can only be added for accounts that have no other active subscription");

            // Have everything we need to do the update
            using(var lockItem = _distributedLockService.TryGetKeyLock(dynWorkspace.Id.ToStringInvariant(), nameof(DynWorkspaceSubscription), 60))
            {
                if (lockItem == null)
                {
                    throw new ResourceUnvailableException("Subscription currently locked by another process");
                }

                var dynWorkspacePublisherSubscription = workspaceSubscription.ToDynWorkspacePublisherSubscription(publisherAccount.PublisherAccountId,
                                                                                                                  subscriptionType, customMonthlyFee, customPerPostFee);

                dynWorkspacePublisherSubscription.StripeCustomerId = stripeCustomerId;

                Guard.AgainstArgumentOutOfRange((dynWorkspacePublisherSubscription?.StripeCustomerId).IsNullOrEmpty(), "PublisherAccountSubscription.StripeCustomerId");

                if (newStripeSubscriptionId.IsNullOrEmpty())
                {
                    var stripe = await StripeService.GetInstanceAsync();

                    var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                   {
                                       {
                                           "PublisherUserName", publisherAccount.UserName
                                       }
                                   };

                    if (rydrEmployeeSig.HasValue())
                    {
                        metadata.Add("RydrEmployeeSignature", rydrEmployeeSig);
                    }

                    if (rydrEmployeeLogin.HasValue())
                    {
                        metadata.Add("RydrEmployeeLogin", rydrEmployeeLogin);
                    }

                    var stripeSubscription = await stripe.CreateManagedSubscriptionAsync(workspaceId, publisherAccount.PublisherAccountId,
                                                                                         dynWorkspacePublisherSubscription.StripeCustomerId, subscriptionType,
                                                                                         metadata, backdateTo: backdateTo,
                                                                                         customMonthlyFeeCents: dynWorkspacePublisherSubscription.CustomMonthlyFeeCents ?? 0,
                                                                                         customPerPostFeeCents: dynWorkspacePublisherSubscription.CustomPerPostFeeCents ?? 0);

                    newStripeSubscriptionId = stripeSubscription.Id;
                }

                dynWorkspacePublisherSubscription.StripeSubscriptionId = newStripeSubscriptionId;

                await PutPublisherSubscriptionAsync(dynWorkspacePublisherSubscription);
            }

            return true;
        }

        private async Task AddRemovePayPerBusinessSubscriptionAsync(long workspaceId, IReadOnlyList<long> publisherAccountIds, bool removeAccounts)
        {
            Guard.AgainstNullArgument(publisherAccountIds.IsNullOrEmptyReadOnly(), nameof(publisherAccountIds));

            var dynWorkspace = await WorkspaceService.DefaultWorkspaceService.GetWorkspaceAsync(workspaceId);

            // Get the subscription so we can get a consistent version later
            var workspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                              .TryGetActiveWorkspaceSubscriptionAsync(dynWorkspace.Id);

            if (!workspaceSubscription.IsValid())
            { // Already invalid/deleted subscription? if so, cancel the entire subscription and be done
                await WorkspaceService.DefaultWorkspaceSubscriptionService
                                      .DeleteWorkspaceSubscriptionAsync(workspaceSubscription);

                return;
            }

            Guard.AgainstRecordNotFound(!workspaceSubscription.IsValid(), dynWorkspace.Id.ToStringInvariant());

            // Have everything we need to do the update
            using(var lockItem = _distributedLockService.TryGetKeyLock(dynWorkspace.Id.ToStringInvariant(), nameof(DynWorkspaceSubscription), 60))
            {
                if (lockItem == null)
                {
                    throw new ResourceUnvailableException("Subscription currently locked by another process");
                }

                var stripe = await StripeService.GetInstanceAsync();

                // Get any matching existing publisher subscriptions, and trim down the passed list
                // If removing, only remove those we actually already have that are still active.
                // If adding, only add those that are not already active and valid
                var activePublisherAccountSubscriptions = await _dynamoDb.FromQuery<DynWorkspacePublisherSubscription>(ps => ps.Id == dynWorkspace.Id &&
                                                                                                                             Dynamo.BeginsWith(ps.EdgeId, DynWorkspacePublisherSubscription.EdgeStartsWith))
                                                                         .Filter(ps => ps.TypeId == (int)DynItemType.WorkspacePublisherSubscription &&
                                                                                       ps.DeletedOnUtc == null)
                                                                         .ExecAsync()
                                                                         .Where(ps => ps.IsValid())
                                                                         .Select(ps =>
                                                                                 {
                                                                                     if (publisherAccountIds.Contains(ps.PublisherAccountId) && ps.SubscriptionType != SubscriptionType.PayPerBusiness)
                                                                                     {
                                                                                         throw new ArgumentException($"PublisherAccount included for PayPerBusiness subscription update [{ps.PublisherAccountId}] already has a subscription of a different type [{ps.SubscriptionType.ToString()}].");
                                                                                     }

                                                                                     return ps.PublisherAccountId;
                                                                                 })
                                                                         .ToHashSet();

                // If workplace subscription is invalid, unsubscribe all...otherwise unsubscribe some or subscribe some...
                var workingPublisherAccountIds = removeAccounts
                                                     ? publisherAccountIds.Where(pid => activePublisherAccountSubscriptions.Contains(pid)).AsListReadOnly()
                                                     : activePublisherAccountSubscriptions.IsNullOrEmpty()
                                                         ? publisherAccountIds
                                                         : publisherAccountIds.Where(pid => !activePublisherAccountSubscriptions.Contains(pid))
                                                                              .AsListReadOnly();

                // Removing accounts and that results in all active publisher account subscriptions being removed? If so, cancel the subscription entirely
                if (workingPublisherAccountIds.IsNullOrEmptyReadOnly())
                {
                    if (removeAccounts && !workspaceSubscription.SubscriptionType.IsAgencySubscriptionType())
                    {
                        await WorkspaceService.DefaultWorkspaceSubscriptionService
                                              .DeleteWorkspaceSubscriptionAsync(workspaceSubscription);

                        return;
                    }

                    // Otherwise, there's nothing to do
                    return;
                }

                // Go get a consistent version of the subscription to ensure we have the current version
                workspaceSubscription = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                              .TryGetWorkspaceSubscriptionConsistentAsync(workspaceSubscription.Id, workspaceSubscription.EdgeId);

                Guard.AgainstRecordNotFound(!workspaceSubscription.IsValid(), dynWorkspace.Id.ToStringInvariant());

                // To update an existing plan item quantity, we need the id of the existing item in the subscription
                var stripeSubscription = await stripe.GetSubscriptionAsync(workspaceSubscription.SubscriptionId);

                var existingStripItemToUpdate = stripeSubscription?.Items?.Data?.FirstOrDefault(xi => xi.Plan.Id.EqualsOrdinalCi(_subscriptionPlanService.PayPerBusinessPlanId));

                Guard.AgainstInvalidData(existingStripItemToUpdate == null, "Subscription being updated does not include the PayPerBusiness plan item");

                if (removeAccounts)
                {
                    activePublisherAccountSubscriptions.ExceptWith(workingPublisherAccountIds);
                }
                else
                {
                    activePublisherAccountSubscriptions.IntersectWith(workingPublisherAccountIds);
                }

                var newQuantity = activePublisherAccountSubscriptions.Count;

                var stripeSubscriptionMeta = workspaceSubscription.GetStripeSubscriptionPublisherAccountsMetas(activePublisherAccountSubscriptions);

                // Update the workspace subscription at stripe, then add one for each publisher, then the workspace subscription
                var updatedStripeSubscription = await stripe.UpdateSubscriptionQuantityAsync(workspaceSubscription.SubscriptionWorkspaceId, workspaceSubscription.SubscriptionId,
                                                                                             _subscriptionPlanService.PayPerBusinessPlanId, existingStripItemToUpdate.Id,
                                                                                             newQuantity, stripeSubscriptionMeta);

                Guard.AgainstInvalidData(newQuantity, updatedStripeSubscription.Quantity.GetValueOrDefault(), $"Subscription quantity values mismatched - code [str:{stripeSubscription.Quantity.GetValueOrDefault()}|ryd:{newQuantity}]");

                await WorkspaceService.DefaultWorkspaceSubscriptionService
                                      .UpdateWorkspaceSubscriptionAsync(workspaceSubscription,
                                                                        () => new DynWorkspaceSubscription
                                                                              {
                                                                                  Quantity = newQuantity,
                                                                                  ModifiedOnUtc = DateTimeHelper.UtcNowTs
                                                                              });

                // Put or delete the publisher subscriptions
                if (removeAccounts)
                {
                    foreach (var workingPublisherAccountId in workingPublisherAccountIds)
                    {
                        await DeletePublisherSubscriptionAsync(workspaceSubscription.ToDynWorkspacePublisherSubscription(workingPublisherAccountId, SubscriptionType.PayPerBusiness));
                    }
                }
                else
                {
                    await PutPublisherSubscriptionsAsync(workingPublisherAccountIds.Select(pid => workspaceSubscription.ToDynWorkspacePublisherSubscription(pid, SubscriptionType.PayPerBusiness)));
                }
            }
        }

        public async Task<SubscriptionType> GetPublisherSubscriptionTypeAsync(long workspaceId, long publisherAccountId)
        {
            var workspaceSubscriptionType = await WorkspaceService.DefaultWorkspaceSubscriptionService
                                                                  .GetActiveWorkspaceSubscriptionTypeAsync(workspaceId);

            if (!workspaceSubscriptionType.IsPublisherSpecificSubscriptionType())
            {   // If this is anything other than a publisher-specific subscription, that's what the subscription is for all connected accounts as well
                // This would be things like none (i.e. haven't paid), trial, unlimited...
                return workspaceSubscriptionType;
            }

            var dynWorkspacePublisherSubscription = await GetPublisherSubscriptionAsync(workspaceId, publisherAccountId);

            // Managing workspace and an invalid publisher subscription?
            if ((workspaceSubscriptionType.IsAgencySubscriptionType() || workspaceSubscriptionType == SubscriptionType.Unlimited) &&
                !dynWorkspacePublisherSubscription.IsValid())
            {
                return workspaceSubscriptionType == SubscriptionType.Unlimited
                           ? SubscriptionType.Unlimited
                           : SubscriptionType.None;
            }

            // Pay per business subscription of some type, so get the specific publiserAcccount subscription info for this workspace...
            // Non existent, deleted, expired?
            if (!dynWorkspacePublisherSubscription.IsValid())
            {
                return SubscriptionType.None;
            }

            if (!(await _authorizeService.IsAuthorizedAsync(workspaceId, publisherAccountId)))
            {
                return SubscriptionType.None;
            }

            return dynWorkspacePublisherSubscription.SubscriptionType;
        }

        private class RydrWorkspacePublisherAccountId
        {
            public long WorkspaceId { get; set; }
            public long PublisherAccountId { get; set; }
        }
    }
}
