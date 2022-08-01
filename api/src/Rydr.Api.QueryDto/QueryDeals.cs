using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using Rydr.Api.QueryDto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.DataAnnotations;

// ReSharper disable ValueParameterNotUsed
// ReSharper disable UnusedMember.Global

namespace Rydr.Api.QueryDto
{
    [Route("/query/publisherdeals")] // Deals the user has created (i.e. typically a business making this call)
    public class QueryPublisherDeals : BaseQueryDataRequest<DynItemIdTypeReferenceGlobalIndex>, IReturn<RydrQueryResponse<DealResponse>>, IGet, IGeoQuery, IHasUserLatitudeLongitude
    {
        private static readonly string _typeRefStartsWith = string.Concat((int)DynItemType.Deal, "|");

        private string[] _statusIds;
        private int[] _typeIds;

        [DynamoDBIgnore]
        public long? PublisherAccountId
        {
            get => null;
            set => Id = value ?? 0;
        }

        [Ignore]
        [IgnoreDataMember]
        public long Id { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string TypeReferenceStartsWith => IsPublishedPausedStatusFilterOnly()
                                                     ? null
                                                     : _typeRefStartsWith;

        [Ignore]
        [IgnoreDataMember]
        public string[] TypeReferenceBetween => IsPublishedPausedStatusFilterOnly()
                                                    ? DefaultPublisherAccountStatsService.DealTypeRefPublishedPausedBetwenMinMax
                                                    : null;

        [DynamoDBIgnore]
        public DealStatus[] Status { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string[] StatusId
        {
            get => Status.IsNullOrEmpty()
                       ? null
                       : (_statusIds ??= Status.Select(s => s.ToString()).ToArray());
            set
            { /* nothing to do */
            }
        }

        [DynamoDBIgnore]
        public DealType[] DealTypes { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public int[] DealType
        {
            get => DealTypes.IsNullOrEmpty()
                       ? null
                       : (_typeIds ??= DealTypes.Select(t => (int)t).ToArray());
            set
            { /* nothing to do */
            }
        }

        [DynamoDBIgnore]
        public DealSort Sort { get; set; }

        [DynamoDBIgnore]
        public long? PlaceId { get; set; }

        [DynamoDBIgnore]
        public bool? IsPrivateDeal { get; set; } = false;

        [DynamoDBIgnore]
        public bool? IncludeExpired { get; set; } = true;

        [DynamoDBIgnore]
        public string Search { get; set; }

        [DynamoDBIgnore]
        public HashSet<Tag> Tags { get; set; }    // null == no filter, empty == show deals with no tags

        [DynamoDBIgnore]
        public IntRange RequestCount { get; set; }

        [DynamoDBIgnore]
        public IntRange RemainingQuantity { get; set; }

        [DynamoDBIgnore]
        public LongRange CreatedBetween { get; set; }

        [DynamoDBIgnore]
        public LongRange PublishedBetween { get; set; }

        [DynamoDBIgnore]
        public double? UserLatitude { get; set; }    // User physical location - for sorting by closest->futhest, noting physical location

        [DynamoDBIgnore]
        public double? UserLongitude { get; set; } // User physical location - for sorting by closest->futhest, noting physical location

        [DynamoDBIgnore]
        public double? Latitude { get; set; }    // Center point to search/find from (i.e. miles around this)

        [DynamoDBIgnore]
        public double? Longitude { get; set; } // Center point to search/find from (i.e. miles around this)

        [DynamoDBIgnore]
        public double? Miles { get; set; }   // Miles around lat/lon to search in a radius

        [DynamoDBIgnore]
        public GeoBoundingBox BoundingBox { get; set; } // Bounding box to filter within

        [DynamoDBIgnore]
        public LongRange FollowerCount { get; set; }

        [DynamoDBIgnore]
        public DoubleRange EngagementRating { get; set; }

        // Interface/base abstract implementations
        [Ignore]
        [IgnoreDataMember]
        [DynamoDBIgnore]
        public override DynItemType QueryDynItemType => DynItemType.Deal;

        [Ignore]
        [IgnoreDataMember]
        public override string OrderByDesc
        {
            get => "TypeReference";
            set
            { /* nothing to do */
            }
        }

        private bool IsPublishedPausedStatusFilterOnly() => Status != null && Status.Length <= 2 &&
                                                            Status.All(s => s == DealStatus.Published || s == DealStatus.Paused);

        // Still using this because this is basically the default query run every time a user goes to the various tabs to view deals, and runs quite
        // frequently...which allows us to offload those queries from ES...
        public bool CanQueryDynamo() => (Sort == DealSort.Default || Sort == DealSort.Newest) && IsPublishedPausedStatusFilterOnly() && Tags == null &&
                                        PlaceId.GetValueOrDefault() <= 0 && !IsPrivateDeal.GetValueOrDefault() && IncludeExpired.GetValueOrDefault() &&
                                        string.IsNullOrEmpty(Search) && !RequestCount.IsValidRange() && !RemainingQuantity.IsValidRange() &&
                                        !CreatedBetween.IsValidRange() && !PublishedBetween.IsValidRange() && !FollowerCount.IsValidRange() &&
                                        !EngagementRating.IsValidRange() && !this.IsValidGeoQuery();
    }

    [Route("/query/publisheddeals")] // Deals available for request/deals that can be requestsed (i.e. typically an influencer making this call)
    public class QueryPublishedDeals : RequestBase, IReturn<RydrQueryResponse<DealResponse>>, IGet, IGeoQuery, IHasUserLatitudeLongitude, IPagedRequest
    {
        private static readonly string[] _publishedStatus =
        {
            DealStatus.Published.ToString()
        };

        private static readonly string _typeOwnerSpaceHashValue = DynItem.BuildTypeOwnerSpaceHash(DynItemType.Deal, UserAuthInfo.PublicOwnerId);

        [DynamoDBIgnore]
        public long? PublisherAccountId
        {
            get => null;
            set => Id = value ?? 0;
        }

        [Ignore]
        [IgnoreDataMember]
        public long Id { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string EdgeId => DealId.HasValue && DealId.Value > 0
                                    ? DealId.Value.ToEdgeId()
                                    : null;

        [Ignore]
        [IgnoreDataMember]
        public string TypeOwnerSpace => _typeOwnerSpaceHashValue;

        [Ignore]
        [IgnoreDataMember]
        public string[] StatusId => _publishedStatus;

        [DynamoDBIgnore]
        public long? DealId { get; set; }

        public DealType[] DealTypes { get; set; }

        [DynamoDBIgnore]
        public double? UserLatitude { get; set; } // User physical location - for sorting by closest->futhest, noting physical location

        [DynamoDBIgnore]
        public double? UserLongitude { get; set; } // User physical location - for sorting by closest->futhest, noting physical location

        [DynamoDBIgnore]
        public double? Latitude { get; set; } // Center point to search/find from (i.e. miles around this)

        [DynamoDBIgnore]
        public double? Longitude { get; set; } // Center point to search/find from (i.e. miles around this)

        [DynamoDBIgnore]
        public double? Miles { get; set; } // Miles around lat/lon to search in a radius

        [DynamoDBIgnore]
        public GeoBoundingBox BoundingBox { get; set; } // Bounding box to filter within

        [DynamoDBIgnore]
        public DateTime? PublishedAfter { get; set; }

        [DynamoDBIgnore]
        public bool? IsPrivateDeal { get; set; } = false;

        [Ignore]
        [IgnoreDataMember]
        public long? IdNotEqualTo { get; set; }

        [DynamoDBIgnore]
        public long? PlaceId { get; set; }

        [DynamoDBIgnore]
        public LongRange FollowerCount { get; set; }

        [DynamoDBIgnore]
        public DoubleRange EngagementRating { get; set; }

        [DynamoDBIgnore]
        public int? MinAge { get; set; }

        [DynamoDBIgnore]
        public DoubleRange Value { get; set; }

        [DynamoDBIgnore]
        public HashSet<Tag> Tags { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public string[] ReferenceIdBetween { get; set; }

        [DynamoDBIgnore]
        public DealSort Sort { get; set; }

        [DynamoDBIgnore]
        public DealSearchGroupOption Grouping { get; set; }

        public int Skip { get; set; }
        public int Take { get; set; }
        public bool IncludeDeleted { get; set; }
    }
}
