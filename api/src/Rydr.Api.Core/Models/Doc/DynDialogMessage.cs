using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Enums;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynDialogMessage : DynItem
    {
        // Hash / Id: DialogId
        // Range / EdgeId: MessageId
        // RefId:
        // OwnerId:
        // WorkspaceId: Workspace that created the message
        // StatusId:
        // MAP (last message id sent): ForRecord.Id / DialogKey (forrecord.id being generally the dealId the dialog is for) where ReferenceNumber == dialogId, MappedItemEdgeId == message.Id.ToEdgeId

        [Ignore]
        [DynamoDBIgnore]
        [IgnoreDataMember]
        public long DialogId
        {
            get => Id;
            set => Id = value;
        }

        [Ignore]
        [DynamoDBIgnore]
        [IgnoreDataMember]
        public long MessageId
        {
            get => EdgeId.ToLong();
            set => EdgeId = value.ToEdgeId();
        }

        public long SentByPublisherAccountId { get; set; }

        [ExcludeNullValue]
        public CompressedString Message { get; set; }

        [ExcludeNullValue]
        public CompressedString Attachments { get; set; }

        public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;

        // public override bool IsPubliclyReadable() => true;
    }
}
