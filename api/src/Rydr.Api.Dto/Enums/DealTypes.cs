using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums;

[EnumAsInt]
public enum DealType
{
    Unknown,
    Deal,
    Event,
    Virtual
}

[EnumAsInt]
public enum DealMetaType
{
    Unknown,
    StartDate,
    EndDate,
    MediaStartDate
}

[EnumAsInt]
public enum DealStatType
{
    Unknown, // 0
    TotalRequests,
    TotalApproved,
    TotalDenied,
    TotalRedeemed,
    TotalCompleted, // 5
    TotalCancelled,
    CurrentRequested,
    CurrentApproved,
    CurrentDenied,
    CurrentRedeemed, // 10
    CurrentCompleted,
    CurrentCancelled,
    TotalInvites,
    CurrentInvites,
    PublishedDeals, // 15   -   This one is not used anywhere as a stat type to store, only used to return published deal counts in the GetPublisherAccountStats endpoint
    CompletedThisWeek, // Also not used anywhere
    CompletedLastWeek, // Also not used anywhere
    TotalDelinquent,
    CurrentDelinquent
}

[EnumAsInt]
public enum DealRestrictionType
{
    Unknown, // 0
    MinFollowerCount,
    MinEngagementRating,
    MinAge,
    MinReach
}

[EnumAsInt]
public enum DealStatus
{
    Unknown,
    Draft,
    Published,
    Paused,
    Completed,
    Deleted
}

[EnumAsInt]
public enum DealRequestStatus
{
    Unknown, // 0
    Requested,
    Invited,
    Denied,
    InProgress,
    Completed, // 5
    Cancelled,
    Redeemed,
    Delinquent
}

public enum DealSort
{
    Default,
    Newest,
    FollowerValue,
    Expiring,
    Closest
}

public enum DealTrackMetricType
{
    Unknown, // 0
    Impressed,
    Clicked,
    XClicked,
    Created,
    Updated, // 5
    Requested, // 6
    Invited,
    RequestApproved, // 8
    RequestDenied,
    RequestCompleted, // 10
    RequestCancelled,
    RequestRedeemed, // 12
    RequestReceived, // 13 - NOTE: This is an inverted side metric type, the influencer's requested metric type will invert to this for the deal owner
}

public static class DealEnumHelpers
{
    public static HashSet<string> PendingDealRequestStatuses { get; } = new(StringComparer.OrdinalIgnoreCase)
                                                                        {
                                                                            DealRequestStatus.Invited.ToString(),
                                                                            DealRequestStatus.Requested.ToString()
                                                                        };

    public static IReadOnlyList<string> PublishedPausedDealStatuses { get; } = new List<string>
                                                                               {
                                                                                   DealStatus.Published.ToString(),
                                                                                   DealStatus.Paused.ToString()
                                                                               }.AsReadOnly();

    public static HashSet<DealRequestStatus> CompletableDealRequestStatuses { get; } = new()
                                                                                       {
                                                                                           DealRequestStatus.InProgress,
                                                                                           DealRequestStatus.Redeemed
                                                                                       };

    public static IReadOnlyList<string> CompletedRedeemedDealRequestStatuses { get; } = new List<string>
                                                                                        {
                                                                                            DealRequestStatus.Completed.ToString(),
                                                                                            DealRequestStatus.Redeemed.ToString()
                                                                                        }.AsReadOnly();

    public static IReadOnlyList<string> AllDealStatTypeStrings { get; } = Enum.GetNames(typeof(DealStatType))
                                                                              .Where(n => !n.Equals(DealStatType.Unknown.ToString(), StringComparison.OrdinalIgnoreCase))
                                                                              .ToList()
                                                                              .AsReadOnly();

    public static bool HasExternalCrmIntegration(this DealTrackMetricType source)
        => source == DealTrackMetricType.Clicked || source == DealTrackMetricType.Created ||
           source == DealTrackMetricType.Requested || source == DealTrackMetricType.Invited ||
           source == DealTrackMetricType.RequestApproved || source == DealTrackMetricType.RequestCompleted ||
           source == DealTrackMetricType.RequestRedeemed;

    public static DealTrackMetricType ToDealOwnerInvertedMetricType(this DealTrackMetricType source)
        => source switch
           {
               DealTrackMetricType.Requested => DealTrackMetricType.RequestReceived,
               _ => DealTrackMetricType.Unknown
           };

    public static bool IsAfterRedeemed(this DealRequestStatus requestStatus)
        => requestStatus == DealRequestStatus.Completed ||
           requestStatus == DealRequestStatus.Cancelled ||
           requestStatus == DealRequestStatus.Delinquent;

    public static bool IsBeforeInProgress(this DealRequestStatus requestStatus)
        => ((int)requestStatus < (int)DealRequestStatus.InProgress);

    public static bool IsPendingRequest(this DealRequestStatus requestStatus)
        => PendingDealRequestStatuses.Contains(requestStatus.ToString());

    public static bool IsInfluencerCompletable(this DealStatus dealStatus)
        => dealStatus == DealStatus.Published || dealStatus == DealStatus.Paused ||
           dealStatus == DealStatus.Completed;

    public static bool IsInfluencerCompletable(this DealRequestStatus dealRequestStatus)
        => CompletableDealRequestStatuses.Contains(dealRequestStatus);

    public static DealStatType ToStatType(this DealRequestStatus dealRequestStatus)
        => dealRequestStatus switch
           {
               DealRequestStatus.Cancelled => DealStatType.TotalCancelled,
               DealRequestStatus.Unknown => DealStatType.TotalRequests,
               DealRequestStatus.Requested => DealStatType.TotalRequests,
               DealRequestStatus.Invited => DealStatType.TotalInvites,
               DealRequestStatus.Denied => DealStatType.TotalDenied,
               DealRequestStatus.Completed => DealStatType.TotalCompleted,
               DealRequestStatus.InProgress => DealStatType.TotalApproved,
               DealRequestStatus.Redeemed => DealStatType.TotalRedeemed,
               DealRequestStatus.Delinquent => DealStatType.TotalDelinquent,
               _ => throw new ArgumentOutOfRangeException(nameof(dealRequestStatus), dealRequestStatus, "Unknown/Unhandled DealRequestStatus value passed")
           };

    public static DealStatType ToCurrentStatType(this DealStatType totalDealRequestStatType)
        => totalDealRequestStatType switch
           {
               DealStatType.TotalInvites => DealStatType.CurrentInvites,
               DealStatType.TotalRequests => DealStatType.CurrentRequested,
               DealStatType.TotalApproved => DealStatType.CurrentApproved,
               DealStatType.TotalDenied => DealStatType.CurrentDenied,
               DealStatType.TotalCompleted => DealStatType.CurrentCompleted,
               DealStatType.TotalCancelled => DealStatType.CurrentCancelled,
               DealStatType.TotalRedeemed => DealStatType.CurrentRedeemed,
               DealStatType.TotalDelinquent => DealStatType.CurrentDelinquent,
               DealStatType.Unknown => DealStatType.Unknown,
               DealStatType.CurrentRequested => DealStatType.Unknown,
               DealStatType.CurrentInvites => DealStatType.Unknown,
               DealStatType.CurrentApproved => DealStatType.Unknown,
               DealStatType.CurrentDenied => DealStatType.Unknown,
               DealStatType.CurrentCompleted => DealStatType.Unknown,
               DealStatType.CurrentCancelled => DealStatType.Unknown,
               DealStatType.CurrentDelinquent => DealStatType.Unknown,
               DealStatType.CurrentRedeemed => DealStatType.Unknown,
               DealStatType.PublishedDeals => DealStatType.Unknown,
               DealStatType.CompletedThisWeek => DealStatType.Unknown,
               DealStatType.CompletedLastWeek => DealStatType.Unknown,
               _ => throw new ArgumentOutOfRangeException(nameof(totalDealRequestStatType), totalDealRequestStatType, "Unknown/Unhandled DealStatType value passed")
           };

    public static bool IsTotalStatType(this DealStatType statType)
        => statType switch
           {
               DealStatType.TotalRequests => true,
               DealStatType.TotalApproved => true,
               DealStatType.TotalDenied => true,
               DealStatType.TotalCompleted => true,
               DealStatType.TotalCancelled => true,
               DealStatType.TotalInvites => true,
               DealStatType.TotalRedeemed => true,
               DealStatType.TotalDelinquent => true,
               _ => false
           };
}
