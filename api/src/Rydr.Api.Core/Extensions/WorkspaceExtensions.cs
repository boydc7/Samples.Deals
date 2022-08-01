using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Stripe;

namespace Rydr.Api.Core.Extensions
{
    public static class WorkspaceExtensions
    {
        public static readonly HashSet<long> RydrWorkspaceIds = RydrEnvironment.IsReleaseEnvironment
                                                                    ? new HashSet<long>
                                                                      {
                                                                          10861571,
                                                                          12872709
                                                                      }
                                                                    : new HashSet<long>
                                                                      {
                                                                          1951077
                                                                      };

        public static bool IsRydrWorkspace(this DynWorkspace workspace)
            => workspace != null && RydrWorkspaceIds.Contains(workspace.Id);

        public static bool IsRydrWorkspace(long workspaceId)
            => workspaceId > 0 && RydrWorkspaceIds.Contains(workspaceId);

        public static Dictionary<string, string> GetStripeSubscriptionPublisherAccountsMetas(this DynWorkspaceSubscription source, IEnumerable<long> activePublisherAccountIds)
            => GetStripeSubscriptionPublisherAccountsMetas(source.SubscriptionWorkspaceId, activePublisherAccountIds);

        public static async ValueTask<bool> IsWorkspaceAdmin(this IWorkspaceService workspaceService, DynWorkspace workspace, long userId)
            => workspace != null && (workspace.OwnerId == userId ||
                                     await workspaceService.GetWorkspaceUserRoleAsync(workspace.Id, userId) == WorkspaceRole.Admin);

        public static async Task<bool> IsWorkspaceAdmin(this IWorkspaceService workspaceService, long workspaceId, long userId)
            => await workspaceService.GetWorkspaceUserRoleAsync(workspaceId, userId) == WorkspaceRole.Admin;

        public static async Task<DynWorkspace> TryGetPersonalWorkspaceAsync(this IWorkspaceService workspaceService, long forUserId, Func<DynWorkspace, bool> predicate = null)
        {
            await foreach (var existingWorkspace in workspaceService.GetWorkspacesOwnedByAsync(forUserId, WorkspaceType.Personal)
                                                                    .Where(w => predicate == null || predicate(w))
                                                                    .Where(w => w.WorkspaceType == WorkspaceType.Personal))
            {
                return existingWorkspace;
            }

            return null;
        }

        public static Dictionary<string, string> GetStripeSubscriptionPublisherAccountsMetas(long workspaceId, IEnumerable<long> activePublisherAccountIds)
        {
            var batchNumber = 0;

            var meta = activePublisherAccountIds.ToBatchesOf(25)
                                                .Where(b => !b.IsNullOrEmpty())
                                                .Select(b =>
                                                        {
                                                            batchNumber++;

                                                            return (KeyName: string.Concat("PublisherAccounts-", batchNumber.ToStringInvariant()
                                                                                                                            .PadLeft(3, '0')),
                                                                    Csv: string.Join(',', b));
                                                        })
                                                .ToDictionarySafe(t => t.KeyName, t => t.Csv)
                       ??
                       new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            meta["WorkspaceId"] = workspaceId.ToStringInvariant();

            return meta;
        }

        public static async Task TryDeleteActiveWorkspaceSubscriptionAsync(this IWorkspaceSubscriptionService service, long workspaceId)
        {
            var workspaceSubscription = await service.TryGetActiveWorkspaceSubscriptionAsync(workspaceId);

            if (workspaceSubscription != null)
            {
                await service.DeleteWorkspaceSubscriptionAsync(workspaceSubscription);
            }
        }

        public static Task<SubscriptionType> GetActiveWorkspaceSubscriptionTypeAsync(this IWorkspaceSubscriptionService service, long workspaceId)
            => service.GetActiveWorkspaceSubscriptionTypeAsync(WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(workspaceId));

        public static bool IsValid(this DynWorkspaceSubscription subscription)
            => subscription != null && !subscription.IsDeleted();

        public static bool IsPaidSubscription(this DynWorkspaceSubscription subscription)
            => IsValid(subscription) && !subscription.IsSystemSubscription && subscription.SubscriptionType.IsPaidSubscriptionType();

        public static bool IsMultiUserSubscription(this DynWorkspaceSubscription subscription)
            => subscription != null && (IsPaidSubscription(subscription) ||
                                        (subscription.SubscriptionType == SubscriptionType.Trial ||
                                         subscription.SubscriptionType == SubscriptionType.Unlimited));

        public static bool IsValid(this DynWorkspacePublisherSubscription subscription)
            => subscription != null && !subscription.IsDeleted();

        public static long GetContextWorkspaceId(this IHasRydrAccountType source, long workspaceId)
        {
            if (workspaceId <= 0 || (source != null && source.IsInfluencer()))
            {
                return 0;
            }

            return GetContextWorkspaceId(source, WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(workspaceId));
        }

        public static long GetContextWorkspaceId(this IHasRydrAccountType source, DynWorkspace workspace)
            => source != null && source.IsInfluencer()
                   ? 0
                   : GetContextWorkspaceId(workspace);

        public static long GetContextWorkspaceId(this IHasWorkspaceId contextSource, RydrAccountType rydrAccountTypeContext = RydrAccountType.None)
        {
            if (rydrAccountTypeContext != RydrAccountType.None && rydrAccountTypeContext.IsInfluencer())
            {
                return 0;
            }

            return GetContextWorkspaceId(WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(contextSource?.WorkspaceId ?? 0));
        }

        public static long GetContextWorkspaceId(this DynWorkspace workspace, RydrAccountType rydrAccountTypeContext = RydrAccountType.None)
        {
            if (workspace == null || (rydrAccountTypeContext != RydrAccountType.None && rydrAccountTypeContext.IsInfluencer()))
            {
                return 0;
            }

            return workspace.WorkspaceType == WorkspaceType.Personal
                       ? 0
                       : workspace.Id;
        }

        public static SubscriptionType ToSubscriptionType(this Subscription source, SubscriptionType defaultType = SubscriptionType.None)
        {
            if (source == null || source.Quantity.GetValueOrDefault() <= 0)
            {
                return SubscriptionType.None;
            }

            return source.Status switch
            {
                SubscriptionStatuses.Active => defaultType == SubscriptionType.None
                                                   ? SubscriptionType.PayPerBusiness
                                                   : defaultType,
                SubscriptionStatuses.Trialing => SubscriptionType.Trial,
                SubscriptionStatuses.Canceled => SubscriptionType.None,
                SubscriptionStatuses.Incomplete => SubscriptionType.None,
                SubscriptionStatuses.IncompleteExpired => SubscriptionType.None,
                SubscriptionStatuses.Unpaid => SubscriptionType.None,
                SubscriptionStatuses.PastDue => SubscriptionType.None,
                _ => throw new ArgumentOutOfRangeException(nameof(source.Status))
            };
        }

        public static DynWorkspaceSubscription ToDynWorkspaceSubscription(this Subscription subscription, long workspaceId,
                                                                          DynWorkspaceSubscription existing = null,
                                                                          SubscriptionType subscriptionType = SubscriptionType.None)
        {
            Guard.AgainstInvalidData(existing?.WorkspaceId ?? workspaceId, workspaceId, "WorkspaceIds invalid");

            workspaceId = existing?.SubscriptionWorkspaceId ?? workspaceId;

            var to = existing ?? new DynWorkspaceSubscription
                                 {
                                     TypeId = (int)DynItemType.WorkspaceSubscription,
                                     WorkspaceId = workspaceId
                                 };

            to.SubscriptionWorkspaceId = workspaceId;
            to.SubscriptionCustomerId = subscription.CustomerId.Coalesce(existing?.SubscriptionCustomerId);

            to.SubscriptionId = subscription.Id;

            to.SubscriptionType = subscriptionType == SubscriptionType.None
                                      ? ToSubscriptionType(subscription, existing?.SubscriptionType ?? SubscriptionType.None)
                                      : subscriptionType;

            to.Quantity = (subscription.Quantity ?? existing?.Quantity).GetValueOrDefault();
            to.UnitPriceCents = (subscription.Plan?.Amount).Gz(existing?.UnitPriceCents);
            to.Interval = (subscription.Plan?.Interval).Coalesce(existing?.Interval);
            to.IntervalCount = (subscription.Plan?.IntervalCount).Gz(existing?.IntervalCount ?? 0);
            to.ProductId = (subscription.Plan?.ProductId).Coalesce(existing?.ProductId);
            to.PlanId = (subscription.Plan?.Id).Coalesce(existing?.PlanId);
            to.SubscriptionStatus = subscription.Status.Coalesce(existing?.SubscriptionStatus);
            to.SubscriptionEmail = (subscription.Customer?.Email).Coalesce(existing?.SubscriptionEmail);

            to.SubscriptionStartedOn = (subscription.StartDate > DateTimeHelper.MinApplicationDate
                                           ? subscription.StartDate
                                           : (subscription.BillingCycleAnchor ?? subscription.Created)).ToUnixTimestamp().Gz(existing?.SubscriptionStartedOn ?? 0);
            to.SubscriptionEndsOn = (subscription.EndedAt ?? subscription.CancelAt ?? subscription.CanceledAt).ToUnixTimestamp().Gz(existing?.SubscriptionEndsOn ?? 0);
            to.SubscriptionTrialStartedOn = subscription.TrialStart.ToUnixTimestamp().Gz(existing?.SubscriptionTrialStartedOn ?? 0);
            to.SubscriptionTrialEndsOn = subscription.TrialEnd.ToUnixTimestamp().Gz(existing?.SubscriptionTrialEndsOn ?? 0);
            to.SubscriptionCanceledOn = (subscription.CancelAt ?? subscription.CanceledAt).ToUnixTimestamp().Gz(existing?.SubscriptionCanceledOn ?? 0);

            to.BillingCycleAnchor = subscription.BillingCycleAnchor.ToUnixTimestamp() ?? 0;

            to.ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant();

            if (existing == null || existing.DynWorkspaceSubscriptionId <= 0)
            {
                to.DynWorkspaceSubscriptionId = Sequences.Provider.Next();
            }
            else
            {
                to.DynWorkspaceSubscriptionId = existing.DynWorkspaceSubscriptionId;
            }

            if ((existing != null && existing.IsDeleted()) || subscription.Status.EqualsOrdinalCi(SubscriptionStatuses.Canceled))
            {
                to.UpdateDateTimeDeleteTrackedValues(existing);
            }
            else
            {
                to.UpdateDateTimeTrackedValues(existing);
            }

            return to;
        }

        public static DynWorkspacePublisherSubscription ToDynWorkspacePublisherSubscription(this DynWorkspaceSubscription dynWorkspaceSubscription,
                                                                                            long publisherAccountId, SubscriptionType subscriptionType,
                                                                                            double customMonthlyFee = double.MinValue, double customPerPostFee = double.MinValue)
        {
            var to = new DynWorkspacePublisherSubscription
                     {
                         SubscriptionWorkspaceId = dynWorkspaceSubscription.WorkspaceId,
                         DynWorkspaceSubscriptionId = dynWorkspaceSubscription.DynWorkspaceSubscriptionId,
                         PublisherAccountId = publisherAccountId,
                         ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant(),
                         TypeId = (int)DynItemType.WorkspacePublisherSubscription,
                         SubscriptionType = subscriptionType,
                         CustomMonthlyFeeCents = subscriptionType.IsManagedCustomPlan()
                                                     ? (int?)Math.Truncate(Math.Round(customMonthlyFee, 2) * 100).NullIf(d => d <= 0)
                                                     : null,
                         CustomPerPostFeeCents = subscriptionType.IsManagedCustomPlan()
                                                     ? (int?)Math.Truncate(Math.Round(customPerPostFee, 2) * 100).NullIf(d => d <= 0)
                                                     : null
                     };

            if (dynWorkspaceSubscription.IsDeleted())
            {
                to.UpdateDateTimeDeleteTrackedValues(dynWorkspaceSubscription);
            }
            else
            {
                to.UpdateDateTimeTrackedValues(dynWorkspaceSubscription);
            }

            return to;
        }
    }
}
