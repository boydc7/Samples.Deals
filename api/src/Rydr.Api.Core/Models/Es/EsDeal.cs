using System.Collections.Generic;
using Nest;
using Rydr.Api.Core.Models.Supporting;

namespace Rydr.Api.Core.Models.Es
{
    [ElasticsearchType(IdProperty = nameof(DealId))]
    public class EsDeal
    {
        [Text(Analyzer = "english", SearchAnalyzer = "english", Norms = true)]
        public string SearchValue { get; set; }

        [Keyword(Index = true, Boost = 3.0, Normalizer = "rydrkeyword")]
        public List<string> Tags { get; set; }

        [Boolean]
        public bool IsDeleted { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public long PublisherAccountId { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public long OwnerId { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public long WorkspaceId { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public long ContextWorkspaceId { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public long DealId { get; set; }

        [GeoPoint(IgnoreZValue = true)]
        public GeoLocation Location { get; set; }

        [Number(NumberType.Double, Coerce = true)]
        public double Value { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public long PlaceId { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long MinFollowerCount { get; set; }

        [Number(NumberType.Double, Coerce = true)]
        public double MinEngagementRating { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int MinAge { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public int DealStatus { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false, IgnoreAbove = 20)]
        public int DealType { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false)]
        public List<long> InvitedPublisherAccountIds { get; set; }

        [Boolean]
        public bool IsPrivateDeal { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long ExpiresOn { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long PublishedOn { get; set; }

        [Keyword(Index = true, IgnoreAbove = 50, Normalizer = "rydrkeyword", EagerGlobalOrdinals = true)]
        public string GroupId { get; set; }

        [Keyword(Index = true, EagerGlobalOrdinals = false)]
        public List<long> RequestedByPublisherAccountIds { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int RequestCount { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int RemainingQuantity { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long CreatedOn { get; set; }

        // Non-data members
        public static DocumentPath<EsDeal> GetDocumentPath(long forDealId)
            => new DocumentPath<EsDeal>(new Id(forDealId)).Index(ElasticIndexes.DealsAlias);
    }
}
