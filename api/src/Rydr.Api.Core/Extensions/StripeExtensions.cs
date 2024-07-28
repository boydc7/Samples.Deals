using Rydr.Api.Core.Interfaces.Services;
using Stripe;

namespace Rydr.Api.Core.Extensions;

public static class StripeExtensions
{
    public static async Task<SubscriptionItem> GetManagedPlanSubscriptionItemAsync(this ISubscriptionPlanService subscriptionPlanService,
                                                                                   Subscription stripeSubscription)
    {
        foreach (var subItem in stripeSubscription.Items.Where(i => i.Plan != null &&
                                                                    i.Plan.Id.HasValue()))
        {
            if (!await subscriptionPlanService.IsManagedPlanIdAsync(subItem.Plan.Id))
            {
                continue;
            }

            return subItem;
        }

        return null;
    }
}
