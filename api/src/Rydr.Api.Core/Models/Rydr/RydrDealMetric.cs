using ServiceStack.DataAnnotations;

namespace Rydr.Api.Core.Models.Rydr
{
    [PostCreateTable(@"
DROP TABLE DealMetrics;
CREATE TABLE DealMetrics
(
Timestamp BIGINT NOT NULL,
MetricType INT NOT NULL,
UserId BIGINT NOT NULL,
WorkspaceId BIGINT NOT NULL,
Latitude DECIMAL(18,8) NOT NULL,
Longitude DECIMAL(18,8) NOT NULL,
DealId BIGINT NOT NULL,
DealPublisherAccountId BIGINT NOT NULL,
DealValue DECIMAL(18,8) NOT NULL,
DealPlaceId BIGINT NOT NULL,
DealReceivePlaceId BIGINT NOT NULL,
DealStatus INT NOT NULL,
IsPrivateDeal INT NOT NULL,
DealMinAge INT NOT NULL,
DealDistanceMiles DECIMAL(18,8) NOT NULL,
DealPublisherAgeRangeMin INT NOT NULL,
DealPublisherAgeRangeMax INT NOT NULL,
DealPublisherGender INT NOT NULL,
RequestPublisherAccountId BIGINT NOT NULL,
RequestAgeRangeMin INT NOT NULL,
RequestAgeRangeMax INT NOT NULL,
RequestGender INT NOT NULL,
RequestFollowedBy INT NOT NULL,
RequestRecentStories INT NOT NULL,
RequestRecentMedias INT NOT NULL,
RequestRecentStoryImpressions BIGINT NOT NULL,
RequestRecentMediaImpressions BIGINT NOT NULL,
RequestRecentStoryReach BIGINT NOT NULL,
RequestRecentMediaReach BIGINT NOT NULL,
RequestStoryEngagementRating DECIMAL(18,8) NOT NULL,
RequestEngagementRating DECIMAL(18,8) NOT NULL,
RequestTrueEngagementRating DECIMAL(18,8) NOT NULL,
PRIMARY KEY (Timestamp, DealPublisherAccountId, MetricType, RequestPublisherAccountId, DealId, DealStatus)
);
")]
    [Alias("DealMetrics")]
    public class RydrDealMetric
    {
        [Required]
        public long Timestamp { get; set; }

        [Required]
        public int MetricType { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        public long WorkspaceId { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double Latitude { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double Longitude { get; set; }

        [Required]
        public long DealId { get; set; }

        [Required]
        public long DealPublisherAccountId { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double DealValue { get; set; }

        [Required]
        public long DealPlaceId { get; set; }

        [Required]
        public long DealReceivePlaceId { get; set; }

        [Required]
        public int DealStatus { get; set; }

        [Required]
        public bool IsPrivateDeal { get; set; }

        [Required]
        public int DealMinAge { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double DealDistanceMiles { get; set; }

        [Required]
        public int DealPublisherAgeRangeMin { get; set; }

        [Required]
        public int DealPublisherAgeRangeMax { get; set; }

        [Required]
        public int DealPublisherGender { get; set; }

        [Required]
        public int RequestAgeRangeMin { get; set; }

        [Required]
        public int RequestAgeRangeMax { get; set; }

        [Required]
        public int RequestGender { get; set; }

        [Required]
        public int RequestFollowedBy { get; set; }

        [Required]
        public int RequestRecentStories { get; set; }

        [Required]
        public int RequestRecentMedias { get; set; }

        [Required]
        public long RequestRecentStoryImpressions { get; set; }

        [Required]
        public long RequestRecentMediaImpressions { get; set; }

        [Required]
        public long RequestRecentStoryReach { get; set; }

        [Required]
        public long RequestRecentMediaReach { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double RequestStoryEngagementRating { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double RequestEngagementRating { get; set; }

        [Required]
        [DecimalLength(18, 8)]
        public double RequestTrueEngagementRating { get; set; }
    }
}
