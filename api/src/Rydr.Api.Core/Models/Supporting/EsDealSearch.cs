using System.Collections.Generic;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Models.Supporting
{
    public class EsDealSearch : IGeoQuery, IHasLatitudeLongitude, IHasUserLatitudeLongitude
    {
        public long DealId { get; set; }
        public long WorkspaceId { get; set; }
        public long? ContextWorkspaceId { get; set; }
        public long PublisherAccountId { get; set; }
        public long DealPublisherAccountId { get; set; }
        public double? UserLatitude { get; set; }
        public double? UserLongitude { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Miles { get; set; }
        public GeoBoundingBox BoundingBox { get; set; }
        public string Search { get; set; }
        public long PlaceId { get; set; }
        public bool IncludeInactive { get; set; }
        public bool IncludeExpired { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
        public bool IdsOnly { get; set; }
        public DealSort Sort { get; set; }
        public PrivateDealOption PrivateDealOption { get; set; }
        public DealStatus[] DealStatuses { get; set; }
        public IReadOnlyList<string> ExcludeGroupIds { get; set; }
        public DealSearchGroupOption Grouping { get; set; }
        public DealType[] DealTypes { get; set; }
        public HashSet<Tag> Tags { get; set; }    // Null means do nothing, empty set searches for deals without any tags
        public IntRange AgeRange { get; set; }
        public LongRange FollowerCount { get; set; }
        public DoubleRange EngagementRating { get; set; }
        public DoubleRange Value { get; set; }
        public IntRange RequestCount { get; set; }
        public IntRange RemainingQuantity { get; set; }
        public LongRange CreatedBetween { get; set; }
        public LongRange PublishedBetween { get; set; }
    }

    public enum PrivateDealOption
    {
        All,
        PublicAndInvited,
        PublicOnly,
        PrivateOnly
    }

    public enum DealSearchGroupOption
    {
        None,
        Default,       // Deal group grouping
        Tags
    }
}
