using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynHashtag : DynItem, IHasNameAndIsRecordLookup
    {
        // Hash/Id = Unique Id
        // Range/Edge = Hashtag name
        // RefId = Id (same as Hash)
        // OwnerId: PublicOwnerId
        // WorkspaceId:
        // StatusId: HashtagType

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public string Name
        {
            get => EdgeId;
            set => EdgeId = value;
        }

        [StringLength(500)]
        [ExcludeNullValue]
        public string PublisherId { get; set; }

        [DynamoDBIgnore]
        public HashtagType HashtagType
        {
            get => Enum.TryParse<HashtagType>(StatusId, true, out var status)
                       ? status
                       : HashtagType.Hashtag;

            set => StatusId = value.ToString();
        }

        public PublisherType PublisherType { get; set; }

        [ExcludeNullValue]
        public HashSet<MediaStat> Stats { get; set; }
    }
}
