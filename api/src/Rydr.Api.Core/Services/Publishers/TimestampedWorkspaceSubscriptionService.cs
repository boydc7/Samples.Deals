using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;
using ServiceStack.Logging;
using Stripe;

namespace Rydr.Api.Core.Services.Publishers
{
    public class TimestampedWorkspaceSubscriptionService : TimestampCachedServiceBase<DynWorkspaceSubscription>, IWorkspaceSubscriptionService
    {
        private static readonly ILog _log = LogManager.GetLogger("TimestampedWorkspaceSubscriptionService");

        private static readonly int _trialSubscriptionLengthDays = RydrEnvironment.GetAppSetting("Subscriptions.TrialLengthDays", 15);

        private readonly IPocoDynamo _dynamoDb;
        private readonly IDeferRequestsService _deferRequestsService;

        public TimestampedWorkspaceSubscriptionService(ICacheClient cacheClient, IPocoDynamo dynamoDb, IDeferRequestsService deferRequestsService)
            : base(cacheClient, 1800)
        {
            _dynamoDb = dynamoDb;
            _deferRequestsService = deferRequestsService;
        }

        public Task<DynWorkspaceSubscription> TryGetActiveWorkspaceSubscriptionAsync(long workspaceId)
        {
            if (workspaceId <= 0)
            {
                return null;
            }

            return GetModelAsync(workspaceId,
                                 () => _dynamoDb.FromQuery<DynWorkspaceSubscription>(s => s.Id == workspaceId &&
                                                                                          Dynamo.BeginsWith(s.EdgeId, DynWorkspaceSubscription.EdgeStartsWith))
                                                .Filter(s => s.TypeId == (int)DynItemType.WorkspaceSubscription &&
                                                             s.DeletedOnUtc == null)
                                                .ExecAsync()
                                                .SingleOrDefaultAsync(s => s.IsValid())
                                                .AsTask());
        }

        public async Task<DynWorkspaceSubscription> TryGetWorkspaceSubscriptionConsistentAsync(long workspaceId, string edgeId)
        {
            var dynWorkspaceSubscription = await _dynamoDb.GetItemAsync<DynWorkspaceSubscription>(workspaceId, edgeId, true);

            if (dynWorkspaceSubscription.IsValid())
            {
                SetModel(workspaceId, dynWorkspaceSubscription);
            }
            else
            {
                FlushModel(workspaceId);
            }

            return dynWorkspaceSubscription;
        }

        public async Task UpdateWorkspaceSubscriptionAsync(DynWorkspaceSubscription workspaceSubscription, Expression<Func<DynWorkspaceSubscription>> put)
        {
            await _dynamoDb.UpdateItemAsync(workspaceSubscription, put);

            FlushModel(workspaceSubscription.Id);

            _deferRequestsService.DeferRequest(new WorkspaceSubscriptionUpdated
                                               {
                                                   SubscriptionWorkspaceId = workspaceSubscription.SubscriptionWorkspaceId,
                                                   SubscriptionId = workspaceSubscription.SubscriptionId
                                               });
        }

        public async Task PutWorkspaceSubscriptionAsync(DynWorkspaceSubscription source, bool isNew = false)
        {
            if (isNew)
            { // For any new subscriptions coming into the system, remove any existing/active system subscriptions
                await RemoveSystemSubscriptionAsync(source.SubscriptionWorkspaceId);
            }

            await _dynamoDb.PutItemDeferAsync(source, RecordType.WorkspaceSubscription);

            FlushModel(source.SubscriptionWorkspaceId);

            if (isNew)
            {
                _deferRequestsService.DeferRequest(new WorkspaceSubscriptionCreated
                                                   {
                                                       SubscriptionWorkspaceId = source.SubscriptionWorkspaceId,
                                                       SubscriptionId = source.SubscriptionId
                                                   });
            }
            else
            {
                _deferRequestsService.DeferRequest(new WorkspaceSubscriptionUpdated
                                                   {
                                                       SubscriptionWorkspaceId = source.SubscriptionWorkspaceId,
                                                       SubscriptionId = source.SubscriptionId
                                                   });
            }
        }

        public async Task DeleteWorkspaceSubscriptionAsync(DynWorkspaceSubscription existingSubscription)
        {
            if (existingSubscription == null)
            {
                return;
            }

            existingSubscription.SubscriptionType = SubscriptionType.None;
            existingSubscription.SubscriptionStatus = SubscriptionStatuses.Canceled;

            await _dynamoDb.SoftDeleteAsync(existingSubscription, UserAuthInfo.AdminAuthInfo);

            FlushModel(existingSubscription.SubscriptionWorkspaceId);

            _deferRequestsService.DeferRequest(new WorkspaceSubscriptionDeleted
                                               {
                                                   SubscriptionWorkspaceId = existingSubscription.SubscriptionWorkspaceId,
                                                   SubscriptionId = existingSubscription.SubscriptionId
                                               });
        }

        public async Task<SubscriptionType> GetActiveWorkspaceSubscriptionTypeAsync(DynWorkspace workspace)
        {
            if (workspace == null || workspace.IsDeleted())
            {
                return SubscriptionType.None;
            }

            if (workspace.WorkspaceType == WorkspaceType.Admin)
            {
                return SubscriptionType.Unlimited;
            }

            var workspaceSubscription = await TryGetActiveWorkspaceSubscriptionAsync(workspace.Id);

            if (!workspaceSubscription.IsValid())
            {
                return SubscriptionType.None;
            }

            if (workspaceSubscription.SubscriptionType == SubscriptionType.Trial)
            {
                if (workspaceSubscription.SubscriptionStartDate().AddDays(_trialSubscriptionLengthDays + 1).Date < DateTimeHelper.UtcNow)
                { // Trial that has expired, delete it and return none
                    await DeleteWorkspaceSubscriptionAsync(workspaceSubscription);

                    return SubscriptionType.None;
                }

                // Active trial...
                return SubscriptionType.Trial;
            }

            // In all other cases, it's just what it says it is
            return workspaceSubscription.SubscriptionType;
        }

        public async Task AddSystemSubscriptionAsync(long workspaceId, SubscriptionType subscriptionType)
        {
            var utcNow = DateTimeHelper.UtcNowTs;

            var systemSubscription = new DynWorkspaceSubscription
                                     {
                                         SubscriptionWorkspaceId = workspaceId,
                                         SubscriptionId = WorkspaceService.RydrSystemSubscriptionId,
                                         SubscriptionType = subscriptionType,
                                         SubscriptionStartedOn = utcNow,
                                         ReferenceId = utcNow.ToStringInvariant(),
                                         SubscriptionStatus = "active",
                                         IsSystemSubscription = true,
                                         TypeId = (int)DynItemType.WorkspaceSubscription
                                     };

            var existingSystemSubscription = await TryGetWorkspaceSubscriptionConsistentAsync(workspaceId, systemSubscription.EdgeId);

            systemSubscription.DynWorkspaceSubscriptionId = existingSystemSubscription?.DynWorkspaceSubscriptionId ?? Sequences.Provider.Next();

            systemSubscription.UpdateDateTimeTrackedValues(existingSystemSubscription);

            await PutWorkspaceSubscriptionAsync(systemSubscription, !existingSystemSubscription.IsValid());
        }

        public async Task RemoveSystemSubscriptionAsync(long workspaceId)
        {
            var existingSystemSubscription = await TryGetWorkspaceSubscriptionConsistentAsync(workspaceId, DynWorkspaceSubscription.BuildEdgeId(WorkspaceService.RydrSystemSubscriptionId));

            if (existingSystemSubscription == null)
            {
                return;
            }

            Guard.AgainstInvalidData(!existingSystemSubscription.IsSystemSubscription, "Existing system subscription in invalid state");

            await DeleteWorkspaceSubscriptionAsync(existingSystemSubscription);
        }

        public async Task<bool> ChargeCompletedRequestUsageAsync(DynDealRequest forDealRequest, bool forceRecharge = false, long forceUsageTimestamp = 0)
        {
            // Valid and completed (or cancelled, in which case we assume it was completed before cancelled for purposes of billing)
            if (forDealRequest == null ||
                (!forceRecharge && forDealRequest.UsageChargedOn > DateTimeHelper.MinApplicationDateTs) ||
                (forDealRequest.RequestStatus != DealRequestStatus.Completed &&
                 forDealRequest.RequestStatus != DealRequestStatus.Cancelled))
            {
                return false;
            }

            var dealWorkspace = await WorkspaceService.DefaultWorkspaceService.TryGetWorkspaceAsync(forDealRequest.DealWorkspaceId);

            var workspaceSubscriptionType = await GetActiveWorkspaceSubscriptionTypeAsync(dealWorkspace);

            // Only agency-like workspace subscriptions charge per deal completion (i.e. managed businesses inside the workspace)
            if (!workspaceSubscriptionType.IsAgencySubscriptionType())
            {
                return false;
            }

            // Agency like workspace subscription, go see if this publisher account is managed or not
            var publisherSubscription = await WorkspaceService.DefaultWorkspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(dealWorkspace.Id, forDealRequest.DealPublisherAccountId);

            if (!publisherSubscription.IsValid() || !publisherSubscription.SubscriptionType.IsManagedSubscriptionType())
            {
                return false;
            }

            var workspaceSubscription = await TryGetActiveWorkspaceSubscriptionAsync(dealWorkspace.Id);

            // Managed publisher within an agency workspace
            var stripe = await StripeService.GetInstanceAsync();

            // Only thing we today bill on a usage basis is completed requests...
            var usageIdempotencyKey = string.Concat(SubscriptionUsageType.CompletedRequest.ToString(), "|", forDealRequest.ToString(), "|", forceUsageTimestamp);
            var managedPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService.GetPublisherAccountAsync(forDealRequest.DealPublisherAccountId);
            var requestPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService.GetPublisherAccountAsync(forDealRequest.PublisherAccountId);

            _log.InfoFormat("Incrementing subscription usage for managing workspace [{0}], managed PublisherAccount [{1}], completed by creator [{2}]",
                            dealWorkspace.Id, managedPublisherAccount.DisplayName(), requestPublisherAccount.DisplayName());

            try
            {
                var usageTimestamp = forceUsageTimestamp.Gz(forDealRequest.ReferenceId.ToLong());

                await stripe.IncrementUsageAsync(publisherSubscription.StripeSubscriptionId, SubscriptionUsageType.CompletedRequest, usageIdempotencyKey,
                                                 customMonthlyFeeCents: publisherSubscription.CustomMonthlyFeeCents ?? 0,
                                                 customPerPostFeeCents: publisherSubscription.CustomPerPostFeeCents ?? 0,
                                                 usageTimestamp: usageTimestamp);
            }
            catch(Exception x) when (_log.LogExceptionReturnFalse(x, $"Could not successfully increment subscription usage for managing workspace [{dealWorkspace.Id}], managed PublisherAccount [{managedPublisherAccount.DisplayName()}], completed by creator [{requestPublisherAccount.DisplayName()}]"))
            {   // Unreachable, exception logged and just bubbles out
                throw;
            }

            _deferRequestsService.DeferLowPriRequest(new SubscriptionUsageIncremented
                                                     {
                                                         UsageType = SubscriptionUsageType.CompletedRequest,
                                                         DealId = forDealRequest.DealId,
                                                         DealRequestPublisherAccountId = forDealRequest.PublisherAccountId,
                                                         SubscriptionWorkspaceId = dealWorkspace.Id,
                                                         WorkspaceSubscriptionId = workspaceSubscription.Id,
                                                         ManagedPublisherAccountId = publisherSubscription.PublisherAccountId,
                                                         SubscriptionId = publisherSubscription.StripeSubscriptionId,
                                                         UsageTimestamp = forDealRequest.ReferenceId.ToLong(),
                                                         CustomerId = publisherSubscription.StripeCustomerId,
                                                         WorkspaceSubscriptionType = workspaceSubscriptionType,
                                                         PublisherSubscriptionType = publisherSubscription.SubscriptionType,
                                                         MonthOfService = forDealRequest.ReferenceId.ToDateTime(),
                                                         Amount = 1
                                                     }.WithAdminRequestInfo());

            return true;
        }
    }
}
