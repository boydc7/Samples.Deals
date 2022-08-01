using System;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Models.Supporting
{
    public class ServerNotificationPushNotificationObject
    {
        public PublisherAccountInfo FromPublisherAccount { get; set; }
        public PublisherAccountInfo ToPublisherAccount { get; set; }
        public RecordTypeId ForRecord { get; set; }
        public string NotificationType { get; set; }
        public DateTime OccurredOn { get; set; }
        public long WorkspaceId { get; set; }
    }
}
