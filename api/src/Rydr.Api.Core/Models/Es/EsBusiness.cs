using System.Collections.Generic;
using Nest;
using Rydr.Api.Core.Models.Supporting;

// ReSharper disable InconsistentNaming

namespace Rydr.Api.Core.Models.Es
{
    [ElasticsearchType(IdProperty = nameof(PublisherAccountId))]
    public class EsBusiness
    {
        [Text(Analyzer = "english", SearchAnalyzer = "english", Norms = true)]
        public string SearchValue { get; set; }

        [Keyword(Index = true, Boost = 3.0, Normalizer = "rydrkeyword")]
        public List<string> Tags { get; set; }

        [Boolean]
        public bool IsDeleted { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long PublisherAccountId { get; set; }

        [Keyword(Index = false, Norms = true, Normalizer = "rydrkeyword")]
        public string AccountId { get; set; }

        [Number(NumberType.Integer, Index = false, Coerce = true)]
        public int PublisherType { get; set; }

        [Number(NumberType.Integer, Index = false, Coerce = true)]
        public int PublisherLinkType { get; set; }

        // Auto-mapped to a geo_point ES type
        public GeoLocation Location { get; set; }

        // Non-data members
        public static DocumentPath<EsBusiness> GetDocumentPath(long publisherAccountId)
            => new DocumentPath<EsBusiness>(new Id(publisherAccountId)).Index(ElasticIndexes.BusinessesAlias);
    }
}
