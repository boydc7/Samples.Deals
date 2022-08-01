using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using ServiceStack;

namespace Rydr.Api.Core.Transforms
{
    public static class WorkspaceTransforms
    {
        private static readonly IRequestStateManager _requestStateManager = RydrEnvironment.Container.Resolve<IRequestStateManager>();
        private static readonly IUserNotificationService _userNotificationService = RydrEnvironment.Container.Resolve<IUserNotificationService>();
        private static readonly IWorkspaceSubscriptionService _workspaceSubscriptionService = RydrEnvironment.Container.Resolve<IWorkspaceSubscriptionService>();
        private static readonly IWorkspacePublisherSubscriptionService _workspacePublisherSubscriptionService = RydrEnvironment.Container.Resolve<IWorkspacePublisherSubscriptionService>();

        public static WorkspaceSubscription ToWorkspaceSubscription(this DynWorkspaceSubscription source)
        {
            if (source == null)
            {
                return null;
            }

            var to = source.ConvertTo<WorkspaceSubscription>();

            to.BillingCycleAnchor = source.BillingCycleAnchor.ToDateTime();
            to.SubscriptionStartedOn = source.SubscriptionStartedOn.ToDateTime();
            to.SubscriptionEndsOn = source.SubscriptionEndsOn.ToDateTime();
            to.SubscriptionTrialStartedOn = source.SubscriptionTrialStartedOn.ToDateTime();
            to.SubscriptionTrialEndsOn = source.SubscriptionTrialEndsOn.ToDateTime();
            to.SubscriptionCanceledOn = source.SubscriptionCanceledOn.ToDateTime();

            return to;
        }

        public static async Task<Workspace> ToWorkspaceAsync(this DynWorkspace source, long forWorkspaceUserId,
                                                             IEnumerable<DynPublisherAccount> withPublisherAccounts = null)
        {
            var to = source.ConvertTo<Workspace>();

            to.WorkspaceFeatures = (WorkspaceFeature)source.WorkspaceFeatures.Nz((long)WorkspaceFeature.Default);

            var publisherAccountInfos = new List<WorkspacePublisherAccountInfo>(25);

            foreach (var publisherAccount in withPublisherAccounts ?? Enumerable.Empty<DynPublisherAccount>())
            {
                var workspacePublisherAccount = await ToWorkspacePublisherAccountInfoAsync(publisherAccount, source);

                publisherAccountInfos.Add(workspacePublisherAccount);
            }

            to.PublisherAccountInfo = publisherAccountInfos.AsListReadOnly().NullIfEmpty();

            to.WorkspaceRole = forWorkspaceUserId <= 0
                                   ? WorkspaceRole.User
                                   : forWorkspaceUserId == source.OwnerId
                                       ? WorkspaceRole.Admin
                                       : await WorkspaceService.DefaultWorkspaceService.GetWorkspaceUserRoleAsync(source.Id, forWorkspaceUserId);

            to.SubscriptionType = await _workspaceSubscriptionService.GetActiveWorkspaceSubscriptionTypeAsync(source);

            return to;
        }

        public static async Task<WorkspacePublisherAccountInfo> ToWorkspacePublisherAccountInfoAsync(this DynPublisherAccount source, DynWorkspace forWorkspace)
        {
            var workspacePublisherAccountInfo = new WorkspacePublisherAccountInfo
                                                {
                                                    PublisherAccountProfile = source.ToPublisherAccountProfile(),
                                                    UnreadNotifications = _userNotificationService.GetUnreadCount(source.PublisherAccountId, source.GetContextWorkspaceId(forWorkspace)),
                                                    FollowerCount = (long)(source.Metrics?.GetValueOrDefault(PublisherMetricName.FollowedBy) ?? 0),
                                                    SubscriptionType = await _workspacePublisherSubscriptionService.GetPublisherSubscriptionTypeAsync(forWorkspace.Id, source.PublisherAccountId)
                                                };

            return workspacePublisherAccountInfo;
        }

        public static DynWorkspace ToDynWorkspace(this Workspace source, DynWorkspace existing = null,
                                                  PublisherType createdViaPublisherType = PublisherType.Unknown,
                                                  string createdViaPublisherId = null)
        {
            var to = source.ConvertTo<DynWorkspace>();

            if (existing == null)
            { // New one
                var state = _requestStateManager.GetState();

                to.Id = Sequences.Provider.Next();
                to.EdgeId = to.Id.ToEdgeId();
                to.DynItemType = DynItemType.Workspace;
                to.WorkspaceFeatures = ((long)source.WorkspaceFeatures).Nz((long)WorkspaceFeature.Default);

                to.UpdateDateTimeTrackedValues(source, state);

                to.OwnerId = state.UserId;
                to.WorkspaceId = to.Id;
            }
            else
            {
                to.TypeId = existing.TypeId;
                to.Id = existing.Id;
                to.EdgeId = existing.EdgeId;
                to.WorkspaceFeatures = ((long)source.WorkspaceFeatures).Nz(existing.WorkspaceFeatures).Nz((long)WorkspaceFeature.Default);

                to.UpdateDateTimeDeleteTrackedValues(existing);

                to.WorkspaceId = existing.WorkspaceId;
                to.OwnerId = existing.OwnerId;
            }

            to.Name = source.Name.Coalesce(existing?.Name);

            // Cannot change the type
            to.WorkspaceType = existing?.WorkspaceType ?? (source.WorkspaceType == WorkspaceType.Unspecified
                                                               ? WorkspaceType.Personal
                                                               : source.WorkspaceType);

            to.CreatedViaPublisherId = (existing?.CreatedViaPublisherId ?? createdViaPublisherId).ToNullIfEmpty();
            to.CreatedViaPublisherType = existing?.CreatedViaPublisherType ?? createdViaPublisherType;

            return to;
        }

        public static IEnumerable<RydrWorkspace> ToRydrWorkspace(this DynWorkspace source)
        {
            var to = source.ConvertTo<RydrWorkspace>();

            to.StripeCustomerId = source.StripeCustomerId;
            to.ActiveCampaignCustomerId = source.ActiveCampaignCustomerId;
            to.WorkspaceType = source.WorkspaceType;

            return to.AsEnumerable();
        }

        public static IEnumerable<RydrWorkspaceSubscription> ToRydrWorkspaceSubscription(this DynWorkspaceSubscription source)
        {
            var to = source.ConvertTo<RydrWorkspaceSubscription>();

            to.Id = source.DynWorkspaceSubscriptionId;
            to.WorkspaceId = source.SubscriptionWorkspaceId;
            to.SubscriptionType = source.SubscriptionType;
            to.UnitPrice = Math.Round(source.UnitPriceCents / 100.0, 4);
            to.SubscriptionInterval = source.Interval;
            to.IntervalCount = source.IntervalCount ?? 0;
            to.Email = source.SubscriptionEmail;
            to.CustomerId = source.SubscriptionCustomerId;

            to.BillingCycleAnchor = source.BillingCycleAnchor > DateTimeHelper.MinApplicationDateTs
                                        ? source.BillingCycleAnchor.ToDateTime()
                                        : (DateTime?)null;

            to.StartedOn = source.SubscriptionStartedOn.ToDateTime();
            to.EndsOn = source.SubscriptionEndsOn.ToDateTime();
            to.TrialStartedOn = source.SubscriptionStartedOn.ToDateTime();
            to.TrialEndsOn = source.SubscriptionTrialEndsOn.ToDateTime();
            to.CanceledOn = source.SubscriptionCanceledOn.ToDateTime();
            to.CreatedOn = source.CreatedOn;
            to.ModifiedOn = source.ModifiedOn;
            to.DeletedOn = source.DeletedOn;

            return to.AsEnumerable();
        }

        public static IEnumerable<RydrWorkspacePublisherSubscription> ToRydrWorkspacePublisherSubscription(this DynWorkspacePublisherSubscription source)
        {
            var to = source.ConvertTo<RydrWorkspacePublisherSubscription>();

            to.WorkspaceId = source.SubscriptionWorkspaceId;
            to.PublisherAccountId = source.PublisherAccountId;
            to.WorkspaceSubscriptionId = source.DynWorkspaceSubscriptionId;
            to.CreatedOn = source.CreatedOn;
            to.ModifiedOn = source.ModifiedOn;
            to.DeletedOn = source.DeletedOn;
            to.SubscriptionType = source.SubscriptionType;
            to.SubscriptionId = source.StripeSubscriptionId;
            to.CustomerId = source.StripeCustomerId;
            to.CustomMonthlyFee = Math.Round((source.CustomMonthlyFeeCents ?? 0) / 100.0d, 2);
            to.CustomPerPostFee = Math.Round((source.CustomPerPostFeeCents ?? 0) / 100.0d, 2);

            return to.AsEnumerable();
        }

        public static DynWorkspacePublisherSubscriptionDiscount ToDynWorkspacePublisherSubscriptionDiscount(this ManagedSubscriptionDiscount source,
                                                                                                            DynWorkspacePublisherSubscription workspacePublisherSubscription)
        {
            if (source == null || workspacePublisherSubscription == null)
            {
                return null;
            }

            var dynDiscount = new DynWorkspacePublisherSubscriptionDiscount
                              {
                                  WorkspaceSubscriptionId = workspacePublisherSubscription.DynWorkspaceSubscriptionId,
                                  EdgeId = DynWorkspacePublisherSubscriptionDiscount.BuildEdgeId(workspacePublisherSubscription.PublisherAccountId, source.UsageType),
                                  DynItemType = DynItemType.WorkspacePublisherSubscriptionDiscount,
                                  WorkspaceId = workspacePublisherSubscription.SubscriptionWorkspaceId,
                                  ReferenceId = DateTimeHelper.UtcNowTs.ToStringInvariant(),
                                  PublisherAccountId = workspacePublisherSubscription.PublisherAccountId,
                                  UsageType = source.UsageType,
                                  PercentOff = source.PercentOff,
                                  StartsOn = source.StartsOnInclusive.StartOfMonth().ToUnixTimestamp(),
                                  EndsOn = source.EndsOnExclusive.Date.ToUnixTimestamp()
                              };

            dynDiscount.UpdateDateTimeTrackedValues();

            return dynDiscount;
        }
    }
}
