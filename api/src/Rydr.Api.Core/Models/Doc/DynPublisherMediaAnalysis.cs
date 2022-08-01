using System.Collections.Generic;
using System.Runtime.Serialization;
using Amazon.Comprehend;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Files;
using Rydr.Api.Dto.Interfaces;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Doc
{
    public class DynPublisherMediaAnalysis : DynItem, IHasPublisherAccountId
    {
        // Hash/Id: PublisherMediaId
        // Range/Edge: (int)DynItemType.PublisherMediaAnalysis, PublisherType, mediaId (from the publisher, i.e. fb)
        // RefId = PublisherAccountId
        // Expires =
        // OwnerId:
        // WorkspaceId:
        // StatusId:

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherMediaId
        {
            get => Id;
            set => Id = value;
        }

        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public long PublisherAccountId => ReferenceId.ToLong();

        [ExcludeNullValue]
        public List<ValueWithConfidence> PopularEntities { get; set; }

        [ExcludeNullValue]
        public List<ValueWithConfidence> Moderations { get; set; }

        [ExcludeNullValue]
        public List<ValueWithConfidence> ImageLabels { get; set; }

        [ExcludeNullValue]
        public Dictionary<string, long> ImageFacesEmotions { get; set; }

        [ExcludeNullValue]
        public string Sentiment { get; set; }

        public double MixedSentiment { get; set; }
        public double PositiveSentiment { get; set; }
        public double NeutralSentiment { get; set; }
        public double NegativeSentiment { get; set; }

        public long ImageFacesCount { get; set; }
        public long ImageFacesAgeSum { get; set; }
        public long ImageFacesMales { get; set; }
        public long ImageFacesFemales { get; set; }
        public long ImageFacesSmiles { get; set; }
        public long ImageFacesBeards { get; set; }
        public long ImageFacesMustaches { get; set; }
        public long ImageFacesEyeglasses { get; set; }
        public long ImageFacesSunglasses { get; set; }

        public bool IsPositiveSentimentType() => Sentiment?.EqualsOrdinalCi(SentimentType.POSITIVE.Value) ?? false;
        public bool IsNegativeSentimentType() => Sentiment?.EqualsOrdinalCi(SentimentType.NEGATIVE.Value) ?? false;
        public bool IsNeutralSentimentType() => Sentiment?.EqualsOrdinalCi(SentimentType.NEUTRAL.Value) ?? false;
        public bool IsMixedSentimentType() => Sentiment?.EqualsOrdinalCi(SentimentType.MIXED.Value) ?? false;

        [ExcludeNullValue]
        public Dictionary<string, string> HumanResponseLocations { get; set; }

        public static string BuildEdgeId(PublisherType type, string mediaId) => string.Concat((int)DynItemType.PublisherMediaAnalysis, "|", type, "|", mediaId);
    }
}
