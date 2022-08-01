using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynNotification : DynItem, IHasPublisherAccountId
    {
        // Hash/Id: ToPublisherAccountId
        // Range/Edge: NotificationType / ForRecord combination
        // RefId = InWorkspaceId | Time created combo
        // Expires = 95 days after creation
        // OwnerId =
        // WorkspaceId = WorkspaceId the notification applies in (deal created workspace for example)

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long ToPublisherAccountId
        {
            get => Id;
            set => Id = value;
        }

        public long FromPublisherAccountId { get; set; }

        [ExcludeNullValue]
        public RecordTypeId ForRecord { get; set; }

        public ServerNotificationType NotificationType { get; set; }

        [ExcludeNullValue]
        public string Title { get; set; }

        [ExcludeNullValue]
        public string Message { get; set; }

        public bool IsRead { get; set; }

        public static string BuildEdgeId(ServerNotificationType type, RecordTypeId forRecord)
            => BuildEdgeId(type, forRecord?.ToString());

        public static string BuildEdgeId(ServerNotificationType type, string forRecordIdString)
            => string.Concat("Notification|", type, "-", forRecordIdString ?? "NULL");

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId => ToPublisherAccountId;
    }
}
