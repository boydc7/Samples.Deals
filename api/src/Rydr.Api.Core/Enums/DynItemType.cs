using Rydr.Api.Core.Configuration;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Enums;

[EnumAsInt]
public enum DynItemType
{
    Null, // 0
    Credential, // 1
    File,
    FileObject,
    FileModerationLabel,
    FileObjectLabel, // 5
    FileObjectFace,
    PublisherApp,
    PublisherAccount,
    PublisherAppAccount,
    Place, // 10
    Deal,
    Hashtag,
    Message,
    Dialog,
    DialogMember, // 15
    InviteRequest,
    Association,
    FirebasePushToken,
    DealRequest,
    DealStat, // 20
    DealStatusChange,
    DealRequestStatusChange,
    Notification,
    Authorization,
    AccountLocation, // 25
    PublisherMedia,
    PublisherMediaStat,
    PublisherMediaComment,
    DailyStatSnapshot,
    User, // 30
    DailyStat,
    InviteToken,
    PublisherMediaAnalysis,
    PublisherAccountStat,
    DealGroup, // 35
    Workspace,
    WorkspaceUser,
    WorkspaceSubscription,
    MediaLabel,
    NotificationSubscription, // 40
    WorkspacePublisherSubscription,
    ApprovedMedia,
    WorkspacePublisherSubscriptionDiscount,
    CustomSubscriptionPrice,
}

public static class DynItemTypeHelpers
{
    public static readonly string DynamoItemsTableName = string.Concat(RydrEnvironment.GetAppSetting("AWS.Dynamo.TableNamePrefix", "dev_"), "Items");

    public static readonly string DynamoItemMapsTableName = string.Concat(RydrEnvironment.GetAppSetting("AWS.Dynamo.TableNamePrefix", "dev_"), "ItemMaps");
}
