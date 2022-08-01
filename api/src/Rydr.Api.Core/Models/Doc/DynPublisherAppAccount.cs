using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynPublisherAppAccount : DynItem, IHasPublisherAccountId
    {
        // Hash/Id: PublisherAccountId
        // Range/Edge: PublisherAppId
        // OwnerId:
        // WorkspaceId:
        // StatusId:

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
        public long PublisherAppId
        {
            get => EdgeId.ToLong();
            set => EdgeId = value.ToEdgeId();
        }

        [ExcludeNullValue]
        public string PubAccessToken { get; set; }

        [ExcludeNullValue]
        public string PubTokenType { get; set; }

        [ExcludeNullValue]
        public string ForUserId { get; set; }

        [ExcludeNullValue]
        public HashSet<string> PubAccessTokenScopes { get; set; }

        public long TokenLastUpdated { get; set; }
        public bool IsSyncDisabled { get; set; }
        public int FailuresSinceLastSuccess { get; set; }
        public long LastFailedOn { get; set; }
        public Dictionary<string, long> SyncStepsLastFailedOn { get; set; }
        public Dictionary<string, long> SyncStepsFailCount { get; set; }

        // True if this is a publisherApp combination for a non-token'd account that just tracks sync times, counts, etc
        public bool IsShadowAppAccont { get; set; }

        public bool IsValid() => !IsDeleted() && !IsShadowAppAccont && PubAccessToken.HasValue() && !IsSyncDisabled;

        public override AccessIntent DefaultAccessIntent() => AccessIntent.ReadOnly;
    }
}
