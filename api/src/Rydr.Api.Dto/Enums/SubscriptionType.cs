using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums;

[EnumAsInt]
public enum SubscriptionType
{
    None = 0,
    Unlimited,
    PayPerBusiness,
    Trial, // 3
    ManagedStarter,
    ManagedCampaigns, // 5
    ManagedCustom, // 6 - Arrears billing...
    ManagedCustomAdvance // 7
}

[EnumAsInt]
public enum SubscriptionUsageType
{
    None = 0,
    CompletedRequest,
    SubscriptionFee,
    SubscriptionFeeCredit,
    CompletedRequestCredit
}

public static class SubscriptionTypeHelpers
{
    public static bool IsManagedCustomPlan(this SubscriptionType source)
        => source == SubscriptionType.ManagedCustomAdvance || source == SubscriptionType.ManagedCustom;

    public static bool IsPublisherSpecificSubscriptionType(this SubscriptionType source)
        => source != SubscriptionType.None &&
           source != SubscriptionType.Trial;

    public static bool IsActiveSubscriptionType(this SubscriptionType source)
        => source != SubscriptionType.None;

    public static bool IsPaidSubscriptionType(this SubscriptionType source)
        => source != SubscriptionType.None && source != SubscriptionType.Trial;

    public static bool IsAgencySubscriptionType(this SubscriptionType source)
        => source == SubscriptionType.Unlimited;

    public static bool IsManagedSubscriptionType(this SubscriptionType source)
        => source == SubscriptionType.ManagedCustomAdvance ||
           source == SubscriptionType.ManagedCustom ||
           source == SubscriptionType.ManagedStarter ||
           source == SubscriptionType.ManagedCampaigns;

    public static SubscriptionUsageType GetCreditTypeForUsage(this SubscriptionUsageType nonCreditType)
        => nonCreditType switch
           {
               SubscriptionUsageType.CompletedRequest => SubscriptionUsageType.CompletedRequestCredit,
               SubscriptionUsageType.SubscriptionFee => SubscriptionUsageType.SubscriptionFeeCredit,
               _ => SubscriptionUsageType.None
           };
}
