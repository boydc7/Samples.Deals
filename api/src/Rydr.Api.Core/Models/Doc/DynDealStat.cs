using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynDealStat : DynStat
    {
        // Hash/Id = DealId
        // Range/Edge = Deal.PublisherAccountId | DealStatType combination
        // RefId: Deal.PublisherAccountId
        // OwnerId:
        // WorkspaceId: Workspace that owns the deal
        // StatusId:

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long DealId
        {
            get => Id;
            set => Id = value;
        }

        public static string BuildEdgeId(long dealPublisherAccountId, DealStatType statType) => BuildEdgeId(dealPublisherAccountId, statType.ToString());
        public static string BuildEdgeId(long dealPublisherAccountId, string statTypeString) => string.Concat(dealPublisherAccountId, "|", statTypeString);
    }

    public class DynPublisherAccountStat : DynStat
    {
        // Hash/Id = PublisherAccountId
        // Range/Edge = DynItemType.PublisherAccountStat | constant additive (i.e. "dealstats") | refWorkspaceId | DealStatType combination
        // RefId: PublisherAccountId
        // OwnerId:
        // WorkspaceId: RefWorkspaceId that makes up the edge - for influencers, this is 0...for biz, the deal workspace
        // StatusId:

        public static string BuildEdgeId(DynItemType statsForType, long workspaceId, DealStatType statType)
            => BuildEdgeId(statsForType.ToString(), workspaceId, statType.ToString());

        public static string BuildEdgeId(string statsForType, long workspaceId, string statTypeSuffix)
            => string.Concat((int)DynItemType.PublisherAccountStat, "|", statsForType, "|", workspaceId, "|", statTypeSuffix);
    }

    public abstract class DynStat : DynItem, IHasPublisherAccountId
    {
        [ExcludeNullValue]
        public string Value { get; set; }

        [ExcludeNullValue]
        public long? Cnt { get; set; }

        public long PublisherAccountId { get; set; }
        public DealStatType StatType { get; set; }
    }
}
