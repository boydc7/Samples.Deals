using System.Collections.Generic;
using Nest;
using Rydr.Api.Core.Models.Supporting;

// ReSharper disable InconsistentNaming

namespace Rydr.Api.Core.Models.Es
{
    public static class EsCreatorScales
    {
        public const int EngagementRatingScale = 1000;
        public const int PercentageScale = 100;
        public const int AvgCostPerScale = 10000;
    }

    [ElasticsearchType(IdProperty = nameof(PublisherAccountId))]
    public class EsCreator
    {
        [Text(Analyzer = "english", SearchAnalyzer = "english", Norms = true)]
        public string SearchValue { get; set; }

        [Keyword(Index = true, Boost = 3.0, Normalizer = "rydrkeyword")]
        public List<string> Tags { get; set; }

        [Boolean]
        public bool IsDeleted { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long PublisherAccountId { get; set; }

        [Keyword(Index = true, Norms = true, Normalizer = "rydrkeyword")]
        public string AccountId { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int PublisherType { get; set; }

        // PROFILE data

        // Auto-mapped to a geo_point ES type
        public GeoLocation LastLocation { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long LastLocationModifiedOn { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int AgeRangeMin { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int AgeRangeMax { get; set; }

        // Int representation of the GenderType enum
        [Number(NumberType.Integer, Coerce = true)]
        public int Gender { get; set; }

        // ACCOUNT INSIGHT data

        [Number(NumberType.Integer, Coerce = true)]
        public int MediaCount { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long FollowedBy { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long Following { get; set; }

        // Scaled by 1000
        [Number(NumberType.Integer, Coerce = true)]
        public int StoryEngagementRating { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long StoryImpressions { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long StoryReach { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int StoryActions { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int Stories { get; set; }

        // Scaled by 1000
        [Number(NumberType.Integer, Coerce = true)]
        public int MediaEngagementRating { get; set; }

        // Scaled by 1000
        [Number(NumberType.Integer, Coerce = true)]
        public int MediaTrueEngagementRating { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long MediaImpressions { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long MediaReach { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int MediaActions { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int Medias { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AvgStoryImpressions { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AvgMediaImpressions { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AvgStoryReach { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AvgMediaReach { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int AvgStoryActions { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int AvgMediaActions { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long Follower7DayJitter { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long Follower14DayJitter { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long Follower30DayJitter { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceUsa { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceEnglish { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceSpanish { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceMale { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceFemale { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge1317 { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge18Up { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge1824 { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge25Up { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge2534 { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge3544 { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge4554 { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge5564 { get; set; }

        [Number(NumberType.Long, Coerce = true)]
        public long AudienceAge65Up { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int ImagesAvgAge { get; set; }

        // Scaled by 100 (percentage)
        [Number(NumberType.Integer, Coerce = true)]
        public int SuggestiveRating { get; set; }

        // Scaled by 100 (percentage)
        [Number(NumberType.Integer, Coerce = true)]
        public int ViolenceRating { get; set; }

        // RYDR metrics

        // Measures of how active they are on the platform

        // Scaled by 100 (they are percentages )
        [Number(NumberType.Integer, Coerce = true)]
        public int Rydr7DayActivityRating { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int Rydr14DayActivityRating { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int Rydr30DayActivityRating { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int Requests { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int CompletedRequests { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long AvgCPMr { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long AvgCPMi { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long AvgCPE { get; set; }

        // 5 buckets of metrics for ranges of different deal values

        // Bucket 1 - >0 <=5 deal value
        [Number(NumberType.Integer, Coerce = true)]
        public int? Requests1 { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int? CompletedRequests1 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMr1 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMi1 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPE1 { get; set; }

        // Bucket 2 - >5 <=10 deal value
        [Number(NumberType.Integer, Coerce = true)]
        public int? Requests2 { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int? CompletedRequests2 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMr2 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMi2 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPE2 { get; set; }

        // Bucket 3 - >10 <=25 deal value
        [Number(NumberType.Integer, Coerce = true)]
        public int? Requests3 { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int? CompletedRequests3 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMr3 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMi3 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPE3 { get; set; }

        // Bucket 4 - >25 <=100 deal value
        [Number(NumberType.Integer, Coerce = true)]
        public int? Requests4 { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int? CompletedRequests4 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMr4 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMi4 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPE4 { get; set; }

        // Bucket 5 - >100 deal value
        [Number(NumberType.Integer, Coerce = true)]
        public int? Requests5 { get; set; }

        [Number(NumberType.Integer, Coerce = true)]
        public int? CompletedRequests5 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMr5 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPMi5 { get; set; }

        // Scaled by 10000 (milli-cents)
        [Number(NumberType.Long, Coerce = true)]
        public long? AvgCPE5 { get; set; }

        // Non-data members
        public static DocumentPath<EsCreator> GetDocumentPath(long publisherAccountId)
            => new DocumentPath<EsCreator>(new Id(publisherAccountId)).Index(ElasticIndexes.CreatorsAlias);
    }
}
