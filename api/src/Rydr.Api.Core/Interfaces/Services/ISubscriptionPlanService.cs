using Rydr.Api.Dto.Enums;

namespace Rydr.Api.Core.Interfaces.Services;

public interface ISubscriptionPlanService
{
    string PayPerBusinessPlanId { get; }

    ValueTask<bool> IsManagedPlanIdAsync(string planId);
    ValueTask<IEnumerable<string>> GetPlanIdsForSubscriptionTypeAsync(SubscriptionType subscriptionType, int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0);
    ValueTask<SubscriptionType> GetSubscriptionTypeForPlanIdAsync(string planId);
    ValueTask<string> GetSubscriptionPlanIdForUsageTypeAsync(SubscriptionType subscriptionType, SubscriptionUsageType usageType, int customMonthlyFeeCents = 0, int customPerPostFeeCents = 0);
}
