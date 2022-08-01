using System.Collections.Generic;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Models
{
    public class SendMessage
    {
        public long DialogId { get; set; }
        public HashSet<RecordTypeId> Members { get; set; } // ContactIds and possibly currently one additional record type identifier
        public string Message { get; set; }
        public RecordTypeId ForRecord { get; set; }
        public long SentByPublisherAccountId { get; set; }
    }

    public class DialogMessageNotification
    {
        public long DialogId { get; set; }
        public string DialogName { get; set; }
        public string DialogKey { get; set; }
        public long MessageId { get; set; }
        public string Message { get; set; }
        public long SentBy { get; set; }
        public long SentByPublisherAccountId { get; set; }
        public long SentOn { get; set; }
        public RecordTypeId ForRecord { get; set; }
    }
}
