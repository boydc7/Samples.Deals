using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Messages;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using Stripe;

namespace Rydr.Api.Services.Services;

public class WorkspacesSubscriptionServiceInternal : BaseInternalOnlyApiService
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;
    private readonly IWorkspacePublisherSubscriptionService _workspacePublisherSubscriptionService;
    private readonly IDeferRequestsService _deferRequestsService;
    private readonly IRydrDataService _rydrDataService;
    private readonly IOpsNotificationService _opsNotificationService;

    public WorkspacesSubscriptionServiceInternal(IWorkspaceService workspaceService,
                                                 IWorkspaceSubscriptionService workspaceSubscriptionService,
                                                 IWorkspacePublisherSubscriptionService workspacePublisherSubscriptionService,
                                                 IDeferRequestsService deferRequestsService,
                                                 IRydrDataService rydrDataService,
                                                 IOpsNotificationService opsNotificationService)
    {
        _workspaceService = workspaceService;
        _workspaceSubscriptionService = workspaceSubscriptionService;
        _workspacePublisherSubscriptionService = workspacePublisherSubscriptionService;
        _deferRequestsService = deferRequestsService;
        _rydrDataService = rydrDataService;
        _opsNotificationService = opsNotificationService;
    }

    public async Task Post(SubscriptionUsageIncremented request)
    {
        var monthOfService = (request.MonthOfService > DateTimeHelper.MinApplicationDate
                                  ? request.MonthOfService
                                  : request.UsageTimestamp > DateTimeHelper.MinApplicationDateTs
                                      ? request.UsageTimestamp.ToDateTime()
                                      : _dateTimeProvider.UtcNow).StartOfNextMonth().AddMonths(-1).Date;

        // Ensure a usage is set for the subscription fee on the subscription, if this is one that requires such
        if (request.SubscriptionId.HasValue() &&
            request.PublisherSubscriptionType != SubscriptionType.ManagedCustomAdvance &&
            (request.UsageType == SubscriptionUsageType.CompletedRequest ||
             request.UsageType == SubscriptionUsageType.SubscriptionFeeCredit))
        {
            var stripe = await StripeService.GetInstanceAsync();

            var managedPublisherAccountSubscription = request.PublisherSubscriptionType.IsManagedCustomPlan()
                                                          ? await _workspacePublisherSubscriptionService.GetPublisherSubscriptionAsync(request.SubscriptionWorkspaceId,
                                                                                                                                       request.ManagedPublisherAccountId)
                                                          : null;

            if (managedPublisherAccountSubscription == null || managedPublisherAccountSubscription.SubscriptionType != SubscriptionType.ManagedCustomAdvance)
            {
                await stripe.IncrementUsageAsync(request.SubscriptionId, SubscriptionUsageType.SubscriptionFee,
                                                 customMonthlyFeeCents: managedPublisherAccountSubscription?.CustomMonthlyFeeCents ?? 0,
                                                 customPerPostFeeCents: managedPublisherAccountSubscription?.CustomPerPostFeeCents ?? 0,
                                                 idempotencyKey: null);

                await _rydrDataService.SaveIgnoreConflictAsync(new RydrSubscriptionUsage
                                                               {
                                                                   WorkspaceId = request.SubscriptionWorkspaceId,
                                                                   WorkspaceSubscriptionId = request.WorkspaceSubscriptionId,
                                                                   ManagedPublisherAccountId = request.ManagedPublisherAccountId,
                                                                   SubscriptionId = request.SubscriptionId,
                                                                   CustomerId = request.CustomerId,
                                                                   UsageType = SubscriptionUsageType.SubscriptionFee,
                                                                   UsageOccurredOn = request.UsageTimestamp.ToDateTime(),
                                                                   WorkspaceSubscriptionType = request.WorkspaceSubscriptionType,
                                                                   PublisherSubscriptionType = request.PublisherSubscriptionType,
                                                                   DealId = 0,
                                                                   DealRequestPublisherAccountId = 0,
                                                                   MonthOfService = monthOfService,
                                                                   Amount = request.Amount.Gz(1)
                                                               },
                                                               r => r.Id);
            }
        }

        await _rydrDataService.SaveIgnoreConflictAsync(new RydrSubscriptionUsage
                                                       {
                                                           WorkspaceId = request.SubscriptionWorkspaceId,
                                                           WorkspaceSubscriptionId = request.WorkspaceSubscriptionId,
                                                           ManagedPublisherAccountId = request.ManagedPublisherAccountId,
                                                           SubscriptionId = request.SubscriptionId,
                                                           CustomerId = request.CustomerId,
                                                           UsageType = request.UsageType,
                                                           UsageOccurredOn = request.UsageTimestamp.ToDateTime(),
                                                           WorkspaceSubscriptionType = request.WorkspaceSubscriptionType,
                                                           PublisherSubscriptionType = request.PublisherSubscriptionType,
                                                           DealId = request.DealId,
                                                           DealRequestPublisherAccountId = request.DealRequestPublisherAccountId,
                                                           MonthOfService = monthOfService,
                                                           Amount = request.Amount.Gz(1)
                                                       },
                                                       r => r.Id);
    }

    public Task Post(WorkspaceSubscriptionCreated request)
        => OnWorkspaceSubscriptionCreateOrUpdateAsync(request.SubscriptionWorkspaceId, request.SubscriptionId);

    public Task Post(WorkspaceSubscriptionUpdated request)
        => OnWorkspaceSubscriptionCreateOrUpdateAsync(request.SubscriptionWorkspaceId, request.SubscriptionId);

    public async Task Post(WorkspaceSubscriptionDeleted request)
    {
        var workspaceSubscription = await _workspaceSubscriptionService.TryGetWorkspaceSubscriptionConsistentAsync(request.SubscriptionWorkspaceId,
                                                                                                                   DynWorkspaceSubscription.BuildEdgeId(request.SubscriptionId));

        foreach (var workpsacePublisherSubscription in _dynamoDb.FromQuery<DynWorkspacePublisherSubscription>(s => s.Id == request.SubscriptionWorkspaceId &&
                                                                                                                   Dynamo.BeginsWith(s.EdgeId, DynWorkspacePublisherSubscription.EdgeStartsWith))
                                                                .Filter(s => s.TypeId == (int)DynItemType.WorkspacePublisherSubscription &&
                                                                             s.DeletedOnUtc == null &&
                                                                             s.OwnerId == workspaceSubscription.DynWorkspaceSubscriptionId)
                                                                .Exec()
                                                                .Where(s => !s.IsDeleted() &&
                                                                            s.DynWorkspaceSubscriptionId == workspaceSubscription.DynWorkspaceSubscriptionId))
        {
            await _workspacePublisherSubscriptionService.DeletePublisherSubscriptionAsync(workpsacePublisherSubscription);
        }

        if (!workspaceSubscription.IsSystemSubscription)
        {
            var stripe = await StripeService.GetInstanceAsync();

            var existingSubscription = await stripe.GetSubscriptionAsync(workspaceSubscription.SubscriptionId);

            if (existingSubscription != null && !existingSubscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled))
            {
                await stripe.CancelSubscriptionAsync(workspaceSubscription.SubscriptionId);
            }
        }

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(workspaceSubscription.SubscriptionWorkspaceId, workspaceSubscription.EdgeId)
                                                                },
                                                 Type = RecordType.WorkspaceSubscription
                                             });

        var workspace = await _workspaceService.TryGetWorkspaceAsync(workspaceSubscription.SubscriptionWorkspaceId);

        if (workspace != null)
        {
            await _workspaceService.TrackEventNotificationAsync(workspaceSubscription.SubscriptionWorkspaceId, nameof(WorkspaceSubscriptionDeleted),
                                                                workspace.WorkspaceType.ToString(), new ExternalCrmUpdateItem
                                                                                                    {
                                                                                                        FieldName = "PaidWorkspaces",
                                                                                                        FieldValue = workspaceSubscription.SubscriptionWorkspaceId.ToStringInvariant(),
                                                                                                        Remove = true
                                                                                                    });
        }
    }

    public async Task Post(WorkspacePublisherSubscriptionDeleted request)
    {
        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(request.SubscriptionWorkspaceId, DynWorkspacePublisherSubscription.BuildEdgeId(request.PublisherAccountId))
                                                                },
                                                 Type = RecordType.WorkspacePublisherSubscription
                                             });

        // Remove any discounts that applied to this customer/subscription
        await foreach (var dynDiscount in _dynamoDb.FromQuery<DynWorkspacePublisherSubscriptionDiscount>(d => d.Id == request.DynWorkspaceSubscriptionId &&
                                                                                                              Dynamo.BeginsWith(d.EdgeId,
                                                                                                                                string.Concat((int)DynItemType.WorkspacePublisherSubscriptionDiscount,
                                                                                                                                              "|", request.PublisherAccountId, "|")))
                                                   .Filter(d => d.DeletedOnUtc == null &&
                                                                d.PercentOff > 0 &&
                                                                d.PercentOff <= 100)
                                                   .QueryAsync(_dynamoDb))
        {
            await _dynamoDb.SoftDeleteAsync(dynDiscount);
        }
    }

    public async Task Post(ValidateWorkspaceSubscription request)
    {
        var logMsg = $"Message [{request.Message.Coalesce("<none>")}], subscription id [{request.StripeSubscriptionId ?? "<none>"}], subscription workspace id [{request.SubscriptionWorkspaceId.ToStringInvariant()}]";

        await _opsNotificationService.TrySendApiNotificationAsync("Subscription validation triggered", logMsg);

        _log.WarnFormat("Subscription validation triggered - reason [{0}]", logMsg);

        // TODO:
        // Should be firing this after checkout create/update...maybe after deleting sub?
        // Set the subscription metadata publisherAccountId list/csv
        // Ensure the quantity at stripe matches dyn
        // Resolve what the workspace subscription status should be
        // Cancel/Create/Update as appropriate

        //
        // var workspace = _workspaceService.GetWorkspace(request.SubscriptionWorkspaceId);
        //
        // if (request.PublisherAccountId <= 0)
        // {
        //     var workspaceSubscriptionType = await _workspaceSubscriptionService.GetActiveWorkspaceSubscriptionTypeAsync(workspace)
        //                                                                        ;
        //
        //     if (workspace.SubscriptionType != workspaceSubscriptionType)
        //     {
        //         _workspaceService.UpdateWorkspace(workspace,
        //                                           put: () => new DynWorkspace
        //                                                      {
        //                                                          SubscriptionType = workspaceSubscriptionType
        //                                                      });
        //     }
        // }
    }

    private async Task OnWorkspaceSubscriptionCreateOrUpdateAsync(long workspaceId, string subscriptionId)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId);

        var workspaceSubscription = await _workspaceSubscriptionService.TryGetWorkspaceSubscriptionConsistentAsync(workspace.Id,
                                                                                                                   DynWorkspaceSubscription.BuildEdgeId(subscriptionId));

        await _workspaceService.TrackEventNotificationAsync(workspace.Id, nameof(WorkspaceSubscriptionCreated), workspace.WorkspaceType.ToString(),
                                                            new ExternalCrmUpdateItem
                                                            {
                                                                FieldName = "PaidWorkspaces",
                                                                FieldValue = workspace.Id.ToStringInvariant(),
                                                            });

        _deferRequestsService.PublishMessage(new PostDeferredAffected
                                             {
                                                 CompositeIds = new List<DynamoItemIdEdge>
                                                                {
                                                                    new(workspace.Id, workspaceSubscription.EdgeId)
                                                                },
                                                 Type = RecordType.WorkspaceSubscription
                                             });
    }
}
