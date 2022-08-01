using System;
using System.Collections.Generic;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;

// ReSharper disable InconsistentNaming

namespace Rydr.Api.Dto.Search
{
    [Route("/search/creators", "GET")]
    public class GetCreatorsSearch : BaseCreatorSearch, IReturn<OnlyResultsResponse<CreatorAccountInfo>>
    {
        public long ExcludeInvitesDealId { get; set; }
    }

    [Route("/search/businesses", "GET")]
    public class GetBusinessesSearch : BaseSearch, IReturn<OnlyResultsResponse<PublisherAccountInfo>>, IHasSkipTake
    {
        public int Skip { get; set; }
        public long PublisherAccountId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Miles { get; set; }
        public HashSet<Tag> Tags { get; set; }

        // Just a shim for backward compatability
        public string Search
        {
            get => Query;
            set => Query = value;
        }
    }

    public class CreatorAccountInfo : PublisherAccountInfo
    {
        public double? LastLatitude { get; set; }
        public double? LastLongitude { get; set; }
        public DateTime? LocationUpdatedOn { get; set; }
        public Address LastLocationAddress { get; set; }
        public PublisherAccountStats CreatorStats { get; set; }
    }

    public abstract class BaseCreatorSearch : BaseSearch, IHasSkipTake
    {
        public int Skip { get; set; }
        public long PublisherAccountId { get; set; }
        public double DealValue { get; set; }
        public long MaxApprovals { get; set; }
        public long TargetReach { get; set; }
        public long TargetImpressions { get; set; }
        public long TargetEngagements { get; set; }
        public List<long> ExcludePublisherAccountIds { get; set; }
        public HashSet<Tag> Tags { get; set; }

        // Just a shim for backward compatability
        public string Search
        {
            get => Query;
            set => Query = value;
        }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Miles { get; set; }
        public IntRange MinAgeRange { get; set; }
        public IntRange MaxAgeRange { get; set; }
        public GenderType Gender { get; set; }
        public LongRange FollowerRange { get; set; }
        public LongRange FollowingRange { get; set; }
        public DoubleRange StoryEngagementRatingRange { get; set; }
        public LongRange StoryImpressionsRange { get; set; }
        public LongRange StoryReachRange { get; set; }
        public IntRange StoryActionsRange { get; set; }
        public IntRange StoriesRange { get; set; }
        public DoubleRange MediaEngagementRatingRange { get; set; }
        public DoubleRange MediaTrueEngagementRatingRange { get; set; }
        public LongRange MediaImpressionsRange { get; set; }
        public LongRange MediaReachRange { get; set; }
        public IntRange MediaActionsRange { get; set; }
        public IntRange MediasRange { get; set; }
        public LongRange AvgStoryImpressionsRange { get; set; }
        public LongRange AvgMediaImpressionsRange { get; set; }
        public LongRange AvgStoryReachRange { get; set; }
        public LongRange AvgMediaReachRange { get; set; }
        public IntRange AvgStoryActionsRange { get; set; }
        public IntRange AvgMediaActionsRange { get; set; }
        public LongRange Follower7DayJitterRange { get; set; }
        public LongRange Follower14DayJitterRange { get; set; }
        public LongRange Follower30DayJitterRange { get; set; }
        public LongRange AudienceUsaRange { get; set; }
        public LongRange AudienceEnglishRange { get; set; }
        public LongRange AudienceSpanishRange { get; set; }
        public LongRange AudienceMaleRange { get; set; }
        public LongRange AudienceFemaleRange { get; set; }
        public LongRange AudienceAge1317Range { get; set; }
        public LongRange AudienceAge18UpRange { get; set; }
        public LongRange AudienceAge1824Range { get; set; }
        public LongRange AudienceAge25UpRange { get; set; }
        public LongRange AudienceAge2534Range { get; set; }
        public LongRange AudienceAge3544Range { get; set; }
        public LongRange AudienceAge4554Range { get; set; }
        public LongRange AudienceAge5564Range { get; set; }
        public LongRange AudienceAge65UpRange { get; set; }
        public IntRange ImagesAvgAgeRange { get; set; }
        public DoubleRange SuggestiveRatingRange { get; set; }
        public DoubleRange ViolenceRatingRange { get; set; }
        public DoubleRange Rydr7DayActivityRatingRange { get; set; }
        public DoubleRange Rydr14DayActivityRatingRange { get; set; }
        public DoubleRange Rydr30DayActivityRatingRange { get; set; }
        public IntRange RequestsRange { get; set; }
        public IntRange CompletedRequestsRange { get; set; }
        public DoubleRange AvgCPMrRange { get; set; }
        public DoubleRange AvgCPMiRange { get; set; }
        public DoubleRange AvgCPERange { get; set; }
        public IntRange RequestsRange1 { get; set; }
        public IntRange CompletedRequestsRange1 { get; set; }
        public DoubleRange AvgCPMrRange1 { get; set; }
        public DoubleRange AvgCPMiRange1 { get; set; }
        public DoubleRange AvgCPERange1 { get; set; }
        public IntRange RequestsRange2 { get; set; }
        public IntRange CompletedRequestsRange2 { get; set; }
        public DoubleRange AvgCPMrRange2 { get; set; }
        public DoubleRange AvgCPMiRange2 { get; set; }
        public DoubleRange AvgCPERange2 { get; set; }
        public IntRange RequestsRange3 { get; set; }
        public IntRange CompletedRequestsRange3 { get; set; }
        public DoubleRange AvgCPMrRange3 { get; set; }
        public DoubleRange AvgCPMiRange3 { get; set; }
        public DoubleRange AvgCPERange3 { get; set; }
        public IntRange RequestsRange4 { get; set; }
        public IntRange CompletedRequestsRange4 { get; set; }
        public DoubleRange AvgCPMrRange4 { get; set; }
        public DoubleRange AvgCPMiRange4 { get; set; }
        public DoubleRange AvgCPERange4 { get; set; }
        public IntRange RequestsRange5 { get; set; }
        public IntRange CompletedRequestsRange5 { get; set; }
        public DoubleRange AvgCPMrRange5 { get; set; }
        public DoubleRange AvgCPMiRange5 { get; set; }
        public DoubleRange AvgCPERange5 { get; set; }
    }
}
