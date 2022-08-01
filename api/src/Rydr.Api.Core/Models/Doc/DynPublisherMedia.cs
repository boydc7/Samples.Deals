using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynPublisherMedia : DynItem, ICanBeAuthorizedById, IGenerateFileMediaUrls
    {
        // Hash/Id: PublisherAccountId
        // Range/Edge: Unique Id for this media (new sequence)
        // RefId = PublisherType / mediaId (id at the publisher, i.e. facebook) combination
        // OrgId = 
        // Expires = PublisherMediaValues.DaysBackToKeepMedia days from creation
        // DynItemMap = PublisherAccountId, ReferenceId

        private bool _isRydrHosted;

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId
        {
            get => Id;
            set => Id = value;
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherMediaId
        {
            get => EdgeId.ToLong();
            set => EdgeId = value.ToEdgeId();
        }

        [ExcludeNullValue]
        public string MediaId { get; set; } // Id from the publisher itself (i.e. the facebook.media.id)

        public PublisherType PublisherType { get; set; }

        [ExcludeNullValue]
        public string Caption { get; set; }

        [ExcludeNullValue]
        public string MediaType { get; set; }

        [ExcludeNullValue]
        public string MediaUrl { get; set; }

        [ExcludeNullValue]
        public string PublisherUrl { get; set; }

        public long MediaCreatedAt { get; set; }
        public long LastSyncedOn { get; set; }

        public int AnalyzePriority { get; set; }

        [ExcludeNullValue]
        public string ThumbnailUrl { get; set; }

        public long ActionCount { get; set; }
        public long CommentCount { get; set; }
        public bool IsCompletionMedia { get; set; }

        public bool IsRydrHosted
        {
            get => _isRydrHosted || MediaFileId > 0;
            set => _isRydrHosted = (value || MediaFileId > 0);
        }

        public bool IsAnalyzed { get; set; }
        public int PreBizAccountConversionMediaErrorCount { get; set; }

        public PublisherContentType ContentType { get; set; }

        public long UrlsGeneratedOn { get; set; }

        public static string BuildRefId(PublisherType type, string mediaId) => string.Concat(type, "|", mediaId);

        // Authorize by owner of the media (i.e. the publisher account id)
        public long AuthorizeId() => PublisherAccountId;

        // Ephemeral value used to determine if this needs to be written or not
        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public bool IsDirty { get; set; }

        public void Dirty() => IsDirty = true;

        // Interface implementations
        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long RydrMediaId => PublisherMediaId;

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long MediaFileId => PublisherType == PublisherType.Rydr
                                       ? MediaId.ToLong()
                                       : 0;

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public bool IsPermanentMedia => IsCompletionMedia || IsAnalyzed || IsRydrHosted || PublisherType == PublisherType.Rydr;
    }
}
