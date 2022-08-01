using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynPublisherAccountMediaAnalysis : DynItem, IHasPublisherAccountId
    {
        // Hash/Id: PublisherAccountId
        // Range/Edge: DynItemType.ofAnalysis (i.e. DynItemType.PublisherMediaAnalysis).ToString() plus suffix (i.e. |agganalysis")
        // RefId =
        // Expires =

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId
        {
            get => Id;
            set => Id = value;
        }

        public long CountTextAnalyzed { get; set; }
        public long CountTextQueued { get; set; }
        public long CountImageAnalyzed { get; set; }
        public long CountImageQueued { get; set; }
        public long CountFacesAnalyzed { get; set; }
        public long CountPostsAnalyzed { get; set; }
        public long CountStoriesAnalyzed { get; set; }

        [ExcludeNullValue]
        public List<MediaAnalysisEntity> PopularEntities { get; set; }

        [ExcludeNullValue]
        public List<MediaAnalysisEntity> Moderations { get; set; }

        [ExcludeNullValue]
        public List<MediaAnalysisEntity> ImageLabels { get; set; }

        public long PositiveSentimentOccurrences { get; set; }
        public double PositiveSentimentTotal { get; set; }

        public double MixedSentimentTotal { get; set; }
        public long MixedSentimentOccurrences { get; set; }

        public double NeutralSentimentTotal { get; set; }
        public long NeutralSentimenOccurrences { get; set; }

        public double NegativeSentimentTotal { get; set; }
        public long NegativeSentimentOccurrences { get; set; }

        public long ImageFacesAgeSum { get; set; }
        public long ImageFacesBeards { get; set; }
        public long ImageFacesMustaches { get; set; }
        public long ImageFacesEyeglasses { get; set; }
        public long ImageFacesSmiles { get; set; }
        public long ImageFacesSunglasses { get; set; }
        public long ImageFacesMales { get; set; }
        public long ImageFacesFemales { get; set; }

        [ExcludeNullValue]
        public Dictionary<string, long> ImageFacesEmotions { get; set; }
    }
}
