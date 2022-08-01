using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto.Publishers
{
    [Route("/publisheracct/{PublisherIdentifier}/contentstats", "GET")]
    public class GetPublisherContentStats : BaseGetManyRequest<ContentTypeStat>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public PublisherType? PublisherType { get; set; }
        public PublisherContentType ContentType { get; set; }
        public int Limit { get; set; }
    }

    [Route("/publisheracct/{PublisherIdentifier}/audience/locations", "GET")]
    public class GetPublisherAudienceLocations : BaseGetManyRequest<AudienceLocationResult>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public int Limit { get; set; }
    }

    [Route("/publisheracct/{PublisherIdentifier}/audience/agegenders", "GET")]
    public class GetPublisherAudienceAgeGenders : BaseGetManyRequest<AudienceAgeGenderResult>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public int Limit { get; set; }
    }

    [Route("/publisheracct/{PublisherIdentifier}/audience/growth", "GET")]
    public class GetPublisherAudienceGrowth : BaseGetManyRequest<AudienceGrowthResult>, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Limit { get; set; }
    }

    [Route("/internal/publisheracct/{PublisherIdentifier}/creatormetrics", "POST")]
    public class PostUpdateCreatorMetrics : RequestBase, IPost, IReturnVoid, IHasPublisherAccountIdentifier
    {
        public string PublisherIdentifier { get; set; }

        public static string GetRecurringJobId(long publisherAccountId)
            => string.Concat("UpdateCreatorMetrics|", publisherAccountId);
    }

    [Route("/internal/creatorsmetrics", "POST")]
    public class PostUpdateCreatorsMetrics : RequestBase, IPost, IReturn<StatusSimpleResponse>
    {
        public List<long> PublisherAccountIds { get; set; }
    }

    public class ContentTypeStat
    {
        public long PublisherMediaId { get; set; }
        public DateTime MediaCreatedOn { get; set; }
        public string MediaUrl { get; set; }
        public string PublisherUrl { get; set; }
        public string Period { get; set; }
        public long EndTime { get; set; }
        public long? Engagements { get; set; }
        public long? Impressions { get; set; }
        public long? Saves { get; set; }
        public long? Replies { get; set; }
        public long? Views { get; set; }
        public long? Reach { get; set; }
        public long? Actions { get; set; }
        public long? Comments { get; set; }
        public double? EngagementRating { get; set; }
        public double? TrueEngagementRating { get; set; }
    }

    public class AudienceLocationResult
    {
        public AudienceLocationType LocationType { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
    }

    public class AudienceAgeGenderResult
    {
        public string Gender { get; set; }
        public string AgeRange { get; set; }
        public double Value { get; set; }
    }

    public class AudienceGrowthResult
    {
        public DateTime Date { get; set; }
        public long Followers { get; set; }
        public long? FollowerPriorDayGrowth { get; set; }
        public double? FollowerPriorDayGrowthPercent { get; set; }
        public long Following { get; set; }
        public long? FollowingPriorDayGrowth { get; set; }
        public double? FollowingPriorDayGrowthPercent { get; set; }
        public long OnlineFollowers { get; set; }
        public long? OnlineFollowersPriorDayGrowth { get; set; }
        public double? OnlineFollowersPriorDayGrowthPercent { get; set; }
        public long Impressions { get; set; }
        public long? ImpressionsPriorDayGrowth { get; set; }
        public double? ImpressionsPriorDayGrowthPercent { get; set; }
        public long Reach { get; set; }
        public long? ReachPriorDayGrowth { get; set; }
        public double? ReachPriorDayGrowthPercent { get; set; }
        public long ProfileViews { get; set; }
        public long? ProfileViewsPriorDayGrowth { get; set; }
        public double? ProfileViewsPriorDayGrowthPercent { get; set; }
        public long WebsiteClicks { get; set; }
        public long? WebsitePriorDayGrowth { get; set; }
        public double? WebsitePriorDayGrowthPercent { get; set; }
        public long EmailContacts { get; set; }
        public long? EmailPriorDayGrowth { get; set; }
        public double? EmailPriorDayGrowthPercent { get; set; }
        public long GetDirectionClicks { get; set; }
        public long? GetDirectionClicksPriorDayGrowth { get; set; }
        public double? GetDirectionClicksPriorDayGrowthPercent { get; set; }
        public long PhoneCallClicks { get; set; }
        public long? PhoneCallClicksPriorDayGrowth { get; set; }
        public double? PhoneCallClicksPriorDayGrowthPercent { get; set; }
        public long TextMessageClicks { get; set; }
        public long? TextMessageClicksPriorDayGrowth { get; set; }
        public double? TextMessageClicksPriorDayGrowthPercent { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long DayTimestamp { get; set; }
    }
}
