using System.Collections.Generic;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Enums
{
    [EnumAsInt]
    public enum RecordType
    {
        Unknown, // 0
        User, // 1
        File,
        FileText,
        PublisherApp,
        PublisherAccount, // 5
        Place,
        Deal,
        Hashtag,
        Message,
        Dialog, // 10
        DealRequest,
        DealLink,
        PublisherMedia,
        PublisherMediaStat,
        DailyStatSnapshot, // 15
        DailyStat,
        Workspace,
        WorkspaceSubscription,
        WorkspacePublisherSubscription,
        ApprovedMedia, // 20
    }

    public static class EntityTypeHelpers
    {
        private static readonly HashSet<RecordType> _targetAssociationEntityTypes = new HashSet<RecordType>
                                                                                    {
                                                                                        RecordType.File,
                                                                                        RecordType.Dialog
                                                                                    };

        public static bool IsTargetAssociationEntityType(this RecordType type)
            => _targetAssociationEntityTypes.Contains(type);

        public static bool IsAnAssociation(this RecordType type)
            => false; //type == RecordType.Comment;
    }
}
