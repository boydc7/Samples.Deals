using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Services
{
    public class StaticSubscriptionPlanService : ISubscriptionPlanService
    {
        private class StaticSubscriptionPlanInfo
        {
            public string PlanId { get; set; }
            public SubscriptionUsageType UsageType { get; set; }
        }

        private static readonly string _stripeDefaultPlanId = RydrEnvironment.GetAppSetting("Stripe.DefaultPlanId", "plan_GKd8l8o4s7s0td");

        private static readonly string _stripeStarterPlanId = RydrEnvironment.GetAppSetting("Stripe.Plans.Starter", "price_1GsG0kCUCt0d3IeSMMbuiLHA");
        private static readonly string _stripeStarterPostPlanId = RydrEnvironment.GetAppSetting("Stripe.Plans.Starter.Post", "price_1GsGBPCUCt0d3IeSS1iazY3Y");

        private static readonly string _stripeCampaignsPlanId = RydrEnvironment.GetAppSetting("Stripe.Plans.Campaigns", "price_1GvmiPCUCt0d3IeS83Ar8Ngb");
        private static readonly string _stripeCampaignsPostPlanId = RydrEnvironment.GetAppSetting("Stripe.Plans.Campaigns.Post", "");

        private static readonly Dictionary<SubscriptionType, List<StaticSubscriptionPlanInfo>> _subscriptionTypePlansMap =
            new Dictionary<SubscriptionType, List<StaticSubscriptionPlanInfo>>
            {
                {
                    SubscriptionType.PayPerBusiness, new List<StaticSubscriptionPlanInfo>
                                                     {
                                                         new StaticSubscriptionPlanInfo
                                                         {
                                                             PlanId = _stripeDefaultPlanId,
                                                             UsageType = SubscriptionUsageType.SubscriptionFee
                                                         }
                                                     }
                },
                {
                    SubscriptionType.ManagedStarter, new List<StaticSubscriptionPlanInfo>
                                                     {
                                                         new StaticSubscriptionPlanInfo
                                                         {
                                                             PlanId = _stripeStarterPlanId,
                                                             UsageType = SubscriptionUsageType.SubscriptionFee
                                                         },
                                                         new StaticSubscriptionPlanInfo
                                                         {
                                                             PlanId = _stripeStarterPostPlanId,
                                                             UsageType = SubscriptionUsageType.CompletedRequest
                                                         }
                                                     }
                },
                {
                    SubscriptionType.ManagedCampaigns, new List<StaticSubscriptionPlanInfo>
                                                       {
                                                           new StaticSubscriptionPlanInfo
                                                           {
                                                               PlanId = _stripeCampaignsPlanId,
                                                               UsageType = SubscriptionUsageType.SubscriptionFee
                                                           },
                                                           new StaticSubscriptionPlanInfo
                                                           {
                                                               PlanId = _stripeCampaignsPostPlanId,
                                                               UsageType = SubscriptionUsageType.CompletedRequest
                                                           }
                                                       }
                }
            };

        private readonly Dictionary<string, SubscriptionType> _subscriptionPlanTypeMap = new Dictionary<string, SubscriptionType>(StringComparer.OrdinalIgnoreCase);

        private StaticSubscriptionPlanService()
        {
            // Build the planId => subType map
            foreach (var subscriptionPlanInfoKvp in _subscriptionTypePlansMap)
            {
                foreach (var subscriptionPlanInfo in subscriptionPlanInfoKvp.Value)
                {
                    if (subscriptionPlanInfo.UsageType == SubscriptionUsageType.SubscriptionFee)
                    {
                        _subscriptionPlanTypeMap[subscriptionPlanInfo.PlanId] = subscriptionPlanInfoKvp.Key;

                        break;
                    }
                }
            }
        }

        public static StaticSubscriptionPlanService Instance { get; } = new StaticSubscriptionPlanService();

        public string PayPerBusinessPlanId => _stripeDefaultPlanId;

        public ValueTask<IEnumerable<string>> GetPlanIdsForSubscriptionTypeAsync(SubscriptionType subscriptionType, int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0)
        {
            if (!_subscriptionTypePlansMap.ContainsKey(subscriptionType))
            {
                throw new ArgumentOutOfRangeException($"SubscriptionType of [{subscriptionType.ToString()}] is not valid for plan selection");
            }

            return new ValueTask<IEnumerable<string>>(_subscriptionTypePlansMap[subscriptionType].Select(t => t.PlanId));
        }

        public async ValueTask<bool> IsManagedPlanIdAsync(string planId)
        {
            var subscriptionType = await GetSubscriptionTypeForPlanIdAsync(planId);

            return subscriptionType.IsManagedSubscriptionType();
        }

        public ValueTask<string> GetSubscriptionPlanIdForUsageTypeAsync(SubscriptionType subscriptionType, SubscriptionUsageType usageType,
                                                                        int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0)
        {
            if (!_subscriptionTypePlansMap.TryGetValue(subscriptionType, out var subscriptionPlanInfos) ||
                subscriptionPlanInfos.IsNullOrEmpty())
            {
                return new ValueTask<string>((string)null);
            }

            // Find the plan associated with the usageType requested within this set of plans
            var planInfoItem = subscriptionPlanInfos.FirstOrDefault(pi => pi.UsageType == usageType);

            return new ValueTask<string>(planInfoItem?.PlanId);
        }

        public ValueTask<SubscriptionType> GetSubscriptionTypeForPlanIdAsync(string planId)
        {
            if (string.IsNullOrEmpty(planId))
            {
                return new ValueTask<SubscriptionType>(SubscriptionType.None);
            }

            var result = _subscriptionPlanTypeMap.ContainsKey(planId)
                             ? _subscriptionPlanTypeMap[planId]
                             : SubscriptionType.None;

            return new ValueTask<SubscriptionType>(result);
        }
    }
}
