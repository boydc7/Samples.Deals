using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynDialogMember : DynAssociation
    {
        [Ignore]
        [DynamoDBIgnore]
        [IgnoreDataMember]
        public long MemberId
        {
            get => Id;
            set => Id = value;
        }

        [Ignore]
        [DynamoDBIgnore]
        [IgnoreDataMember]
        public long DialogId
        {
            get => EdgeId.ToLong();
            set => EdgeId = value.ToEdgeId();
        }
    }
}
