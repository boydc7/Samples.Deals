using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.DataAnnotations;
using ServiceStack.Model;
using ServiceStack.Web;

namespace Rydr.Api.Dto
{
    [Route("/deals/{id}", "GET")]
    public class GetDeal : GetDealBase, IHasLongId
    {
        public long Id { get; set; }
    }

    [Route("/deals/{id}/invites", "GET")]
    public class GetDealInvites : BaseGetManyRequest<PublisherAccountProfile>, IHasLongId, IPagedRequest
    {
        public long Id { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    [Route("/deallinks/{deallink}", "GET")]
    public class GetDealByLink : GetDealBase
    {
        public string DealLink { get; set; }
    }

    public abstract class GetDealBase : RequestBase, IGet, IReturn<OnlyResultResponse<DealResponse>>, IHasUserLatitudeLongitude
    {
        public long RequestedPublisherAccountId { get; set; }
        public double? UserLatitude { get; set; }
        public double? UserLongitude { get; set; }
    }

    [Route("/deals/x/{deallink}", "GET")]
    public class GetDealExternalHtml : RequestBase, IGet, IReturn<IHttpResult>
    {
        public string DealLink { get; set; }
    }

    [Route("/deals/{id}/xlink", "GET")]
    public class GetDealExternalLink : BaseGetRequest<StringIdResponse> { }

    [Route("/deals", "POST")]
    public class PostDeal : BasePostRequest<Deal>
    {
        protected override RecordType GetRecordType() => RecordType.Deal;
    }

    [Route("/deals/{id}", "PUT")]
    public class PutDeal : BasePutRequest<Deal>
    {
        public string Reason { get; set; }
        protected override RecordType GetRecordType() => RecordType.Deal;
    }

    [Route("/deals/{dealid}/invites", "PUT")]
    public class PutDealInvites : RequestBaseDeferAffectedVoid, IPut
    {
        public long DealId { get; set; }
        public List<PublisherAccount> PublisherAccounts { get; set; }

        protected override (IEnumerable<long> Ids, RecordType Type) GetMyAffected()
            => (DealId.AsEnumerable(), RecordType.Deal);
    }

    [Route("/deals/{id}", "DELETE")]
    public class DeleteDeal : BaseDeleteRequest
    {
        public string Reason { get; set; }
        protected override RecordType GetRecordType() => RecordType.Deal;
    }

    // DEFERRED actions in response to deal actions
    [Route("/internal/deals/posted", "POST")]
    public class DealPosted : DealPostedLow { }

    [Route("/internal/deals/postedlow", "POST")]
    public class DealPostedLow : RequestBase, IReturnVoid, IPost
    {
        public long DealId { get; set; }
    }

    [Route("/internal/deals/updateexternal", "POST")]
    public class UpdateExternalDeal : RequestBase, IReturnVoid, IPost
    {
        public long DealId { get; set; }
    }

    [Route("/internal/deals/updated", "POST")]
    public class DealUpdated : DealUpdatedLow { }

    [Route("/internal/deals/updatedlow", "POST")]
    public class DealUpdatedLow : DealStatusUpdated
    {
        public HashSet<long> NewlyInvitedPublisherAccountIds { get; set; }
    }

    [Route("/internal/deals/delete", "POST")]
    public class DeleteDealInternal : RequestBase, IReturnVoid, IPost
    {
        public long DealId { get; set; }
        public string Reason { get; set; }
    }

    [Route("/internal/dealstats/increment", "POST")]
    public class DealStatIncrement : RequestBase, IReturnVoid, IPost
    {
        public long DealId { get; set; }
        public DealStatType StatType { get; set; }
        public DealStatType FromStatType { get; set; }
        public long FromPublisherAccountId { get; set; }
    }

    [Route("/internal/dealstats/incremented", "POST")]
    public class DealStatIncremented : RequestBase, IReturnVoid, IPost
    {
        public long DealId { get; set; }
        public DealStatType StatType { get; set; }
        public DealStatType FromStatType { get; set; }
        public long FromPublisherAccountId { get; set; }
    }

    [Route("/internal/deals/statusupdated", "POST")]
    public class DealStatusUpdated : RequestBase, IReturnVoid, IPost
    {
        public long DealId { get; set; }
        public DealStatus FromStatus { get; set; }
        public DealStatus ToStatus { get; set; }
        public long OccurredOn { get; set; }
        public string Reason { get; set; }
        public bool EsUpdated { get; set; }

        public DealStatusUpdated WithEsAlreadyUpdated()
        {
            EsUpdated = true;

            return this;
        }
    }

    public class Deal : BaseDateTimeDeleteTrackedDtoModel, IHasLongId, IHasPublisherAccountId, IHasDealId
    {
        public long Id { get; set; }
        public long PublisherAccountId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Value { get; set; }
        public Place Place { get; set; } // Where the deal is hosted/redeemed - the business location likely
        public List<DealRestriction> Restrictions { get; set; }
        public DealStatus Status { get; set; }
        public DealType DealType { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public int? MaxApprovals { get; set; }
        public bool? AutoApproveRequests { get; set; }
        public int HoursAllowedInProgress { get; set; }
        public int HoursAllowedRedeemed { get; set; }

        public string ApprovalNotes { get; set; } // Any notes about how/where/what to do to redeem the deal when approved

        // Receive**** attribtes are basically things the biz receives/gets from the deal
        public Place ReceivePlace { get; set; } // Where the post/image/story must be tagged
        public List<PublisherAccount> ReceivePublisherAccounts { get; set; } // Which accounts must be mentioned
        public List<Hashtag> ReceiveHashtags { get; set; } // Which hashtags must be included in the post/story/etc.
        public string ReceiveNotes { get; set; } // Any notes about the requirements
        public List<MediaLineItem> ReceiveType { get; set; } // Type of content(s) the biz expects the influencer to post/etc
        public List<PublisherAccount> InvitedPublisherAccounts { get; set; } // Accounts specifically asked to request/take the deal. NOT included in the response
        public List<PublisherMedia> PublisherMedias { get; set; } // Media
        public HashSet<Tag> Tags { get; set; }
        public long DealWorkspaceId { get; set; }
        public HashSet<DealMetaData> MetaData { get; set; }
        public List<long> PublisherApprovedMediaIds { get; set; }

        // Incoming this is set/created in a filter based on various properties
        [Ignore]
        [IgnoreDataMember]
        public string DealGroupId { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long DealId => Id;

        public bool? IsPrivateDeal { get; set; }
    }

    public class DealResponse : IDecorateWithPublisherAccountInfo
    {
        public Deal Deal { get; set; }
        public long? UnreadMessages { get; set; }
        public long? ApprovalsRemaining { get; set; }
        public List<DealStat> Stats { get; set; }
        public DealRequest DealRequest { get; set; }
        public double? DistanceInMiles { get; set; } // The distance in miles from a specified lat/lon in a request
        public List<PublisherAccountProfile> PendingRecentRequesters { get; set; }
        public PublisherAccountInfo PublisherAccount { get; set; }
        public bool? CanBeRequested { get; set; }
        public bool IsInvited { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? PublishedOn { get; set; }
        public List<PublisherAccountProfile> RecentCompleters { get; set; }

        [Ignore]
        [IgnoreDataMember]
        public long PublisherAccountId => Deal?.PublisherAccountId ?? 0;
    }

    public class MediaLineItem
    {
        public PublisherContentType Type { get; set; }
        public int Quantity { get; set; }
    }

    public class DealMetaData : IEquatable<DealMetaData>
    {
        public DealMetaType Type { get; set; }
        public string Value { get; set; }

        public bool Equals(DealMetaData other)
            => other != null && Type == other.Type;

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is DealMetaData oobj && Equals(oobj);
        }

        public override int GetHashCode()
            => Type.GetHashCode();
    }

    public class DealRestriction : IEquatable<DealRestriction>
    {
        public DealRestrictionType Type { get; set; }
        public string Value { get; set; }

        public bool Equals(DealRestriction other)
            => other != null && Type == other.Type;

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is DealRestriction oobj && Equals(oobj);
        }

        public override int GetHashCode()
            => Type.GetHashCode();
    }
}
