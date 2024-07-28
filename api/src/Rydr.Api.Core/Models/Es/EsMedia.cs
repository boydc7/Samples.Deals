using Nest;
using Rydr.Api.Core.Models.Supporting;

namespace Rydr.Api.Core.Models.Es;

[ElasticsearchType(IdProperty = nameof(PublisherMediaId))]
public class EsMedia
{
    [Number(NumberType.Long, Coerce = true)]
    public long PublisherMediaId { get; set; }

    [Text(Analyzer = "english", SearchAnalyzer = "english", Norms = true, Boost = 2.0)]
    public string Tags { get; set; }

    [Text(Analyzer = "english", SearchAnalyzer = "english", Norms = true)]
    public string SearchValue { get; set; }

    [Number(NumberType.Long, Coerce = true)]
    public long PublisherAccountId { get; set; }

    [Keyword(Index = true, IgnoreAbove = 50, Norms = true, Normalizer = "rydrkeyword")]
    public string MediaId { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int PublisherType { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ContentType { get; set; }

    [Keyword(Index = true, IgnoreAbove = 50, Norms = true, Normalizer = "rydrkeyword")]
    public string Sentiment { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public long ImageFacesCount { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesAgeAvg { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesMales { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesFemales { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesSmiles { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesBeards { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesMustaches { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesEyeglasses { get; set; }

    [Number(NumberType.Integer, Coerce = true)]
    public int ImageFacesSunglasses { get; set; }

    // Non-data members
    public static DocumentPath<EsMedia> GetDocumentPath(long forPublisherMediaId)
        => new DocumentPath<EsMedia>(new Id(forPublisherMediaId)).Index(ElasticIndexes.MediaAlias);
}
