using Rydr.Api.Dto.Interfaces;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums;

[EnumAsInt]
public enum WorkspaceType
{
    Unspecified = 0,
    Admin = 1,
    Personal = 2,
    Team = 3
}

[EnumAsInt]
public enum WorkspaceRole
{
    Unknown = 0,
    Admin = 1,
    User = 2
}

[Flags]
public enum WorkspaceFeature : long
{
    None = 0L,
    Default = None,

    // ReSharper disable once ShiftExpressionRealShiftCountIsZero
    Teams = 1L << 0, // 1    - Allows a user to see and use teams-based features, like creating new workspaces, requesting to join other teams, etc.
    BusinessFinder = 1L << 1, // 2 - allows a user to find and soft-link ig profiles and photos...should only be internal teams really...
    DealTags = 1L << 2, // 4
    BusinessTags = 1L << 3, // 8

    // LAST ONE CAN BE
    // LastOne = 1L << 62

    All = ~0L // -1
}

[Flags]
[EnumAsInt]
public enum RydrAccountType
{
    None = 0,

    // ReSharper disable once ShiftExpressionRealShiftCountIsZero
    Business = 1 << 0, // 1
    Influencer = 1 << 1, // 2
    BusinessAndInfluencer = Business | Influencer, // 3
    TokenAccount = 1 << 2, // 4
    Admin = 1 << 3, // 8
}

public static class RydrTypeEnumHelpers
{
    public static bool IsInfluencer(this IHasRydrAccountType source)
        => source.RydrAccountType.HasFlag(RydrAccountType.Influencer);

    public static bool IsInfluencer(this RydrAccountType source)
        => source.HasFlag(RydrAccountType.Influencer);

    public static bool IsBusiness(this IHasRydrAccountType source)
        => source.RydrAccountType.HasFlag(RydrAccountType.Business);

    public static bool IsBusiness(this RydrAccountType source)
        => source.HasFlag(RydrAccountType.Business);

    public static bool AllowsMultipleUsers(this WorkspaceType source)
        => source == WorkspaceType.Team || source == WorkspaceType.Admin;

    public static bool HasExternalCrmIntegration(this WorkspaceType source)
        => source == WorkspaceType.Team || source == WorkspaceType.Personal;

    public static bool HasExternalCrmIntegration(this RydrAccountType source)
        => source.HasFlag(RydrAccountType.Influencer) || source.HasFlag(RydrAccountType.Business);

    public static bool RequiresInviteCode(this WorkspaceType source)
        => source == WorkspaceType.Team;

    public static PublisherType AuthProviderToPublisherType(string authProvider)
    {
        if (string.IsNullOrEmpty(authProvider))
        {
            return PublisherType.Unknown;
        }

        if (authProvider.Contains("facebook", StringComparison.OrdinalIgnoreCase))
        {
            return PublisherType.Facebook;
        }

        if (authProvider.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            return PublisherType.Google;
        }

        if (authProvider.Contains("apple", StringComparison.OrdinalIgnoreCase))
        {
            return PublisherType.Apple;
        }

        if (authProvider.Contains("rydr", StringComparison.OrdinalIgnoreCase))
        {
            return PublisherType.Rydr;
        }

        if (authProvider.Contains("firebase", StringComparison.OrdinalIgnoreCase) ||
            authProvider.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return PublisherType.Firebase;
        }

        if (authProvider.Contains("instagram", StringComparison.OrdinalIgnoreCase))
        {
            return PublisherType.Instagram;
        }

        return PublisherType.Unknown;
    }
}
