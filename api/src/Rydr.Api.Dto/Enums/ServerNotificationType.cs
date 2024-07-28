namespace Rydr.Api.Dto.Enums;

public enum ServerNotificationType
{
    Unspecified,
    All,
    Message,
    Dialog,
    DealMatched,
    DealRequested,
    DealInvited,
    DealRequestApproved,
    DealRequestDenied,
    DealRequestCancelled,
    DealRequestRedeemed,
    DealRequestCompleted,
    AccountAttention,
    WorkspaceEvent,
    DealCompletionMediaDetected,
    EmailReminders,
    EmailProductAnnouncements,
    EmailFeedback,
    EmailInvitations,
    EmailDealMatch,
    EmailMonthlySummary,
    DealRequestDelinquent,
    DealCompleted,
    DealRequestGeneric, // Push noficiation about a specific deal request which could have a variety of title/bodies, link object to a specific deal request
    DealRequestsGeneric // Push notification about a specific person's deal requests, variety of title/bodies, link to summary of requests in the app
}

public static class ServerNotificationTokens
{
    public const string ToPublisherAccountUserName = "toPublisherAccount.UserName";
    public const string FromPublisherAccountUserName = "fromPublisherAccount.UserName";
    public const string EmojiPartyPopper = "emoji.PartyPopper";
    public const string EmojiHandshake = "emoji.Handshake";
    public const string WhiteGreenCheckMark = "emoji.Checkmark";
    public const string WarningSign = "emoji.WarningSign";
    public const string HeavyExclamation = "emoji.Exclamation";
}

public static class EmojiCodePairs
{
    public const string PartyPopper = "\uD83C\uDF89";
    public const string Handshake = "\uD83E\uDD1D";
    public const string WhiteGreenCheckMark = "\u2705";
    public const string WarningSign = "\u26A0\uFE0F";
    public const string HeavyExclamation = "\u2757";
}
