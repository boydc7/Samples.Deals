using System.Collections.Generic;
using System.Runtime.Serialization;
using Rydr.Api.Dto.Auth;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Dto.Publishers
{
    [Route("/facebook/igaccounts", "GET")]
    public class GetFbIgBusinessAccounts : BaseGetManyRequest<FacebookAccount>
    {
        public long PublisherAppId { get; set; }
    }

    [Route("/facebook/businesses", "GET")]
    public class GetFbBusinesses : BaseGetManyRequest<FacebookBusiness>
    {
        public long PublisherAppId { get; set; }
    }

    [Route("/facebook/search/places", "GET")]
    public class SearchFbPlaces : BaseGetManyRequest<FacebookPlaceInfo>, IGeoQuery
    {
        public string Query { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Miles { get; set; }
        public GeoBoundingBox BoundingBox { get; set; }
        public long PublisherAppId { get; set; }
    }

    [Route("/facebook/connectuser", "POST")]
    public class PostFacebookConnectUser : RequestBase, IPost, IReturn<OnlyResultResponse<ConnectedApiInfo>>
    {
        public string AuthToken { get; set; }
        public string AccountId { get; set; }
        public string UserName { get; set; }
        public WorkspaceFeature Features { get; set; }
    }

    [Route("/facebook/webhooks", "GET")]
    [DataContract]
    public class GetFacebookWebhook
    {
        [DataMember(Name = "hub.mode")]
        public string Mode { get; set; }

        [DataMember(Name = "hub.challenge")]
        public string Challenge { get; set; }

        [DataMember(Name = "hub.verify_token")]
        public string VerifyToken { get; set; }
    }

    [Route("/facebook/webhooks", "POST")]
    public class PostFacebookWebhook
    {
        public List<FacebookWebhookEntry> Entry { get; set; }
        public string Object { get; set; } // Should be "instagram" for insta webhooks
    }

    public class FacebookWebhookEntry
    {
        public string Id { get; set; } // Instagram business or creator account id
        public long Time { get; set; }
        public string Uid { get; set; }
        public List<FacebookFieldValue> Changes { get; set; }
    }

    public class FacebookFieldValue
    {
        public string Field { get; set; }
        public FacebookFieldValueItem Value { get; set; }
    }

    [DataContract]
    public class FacebookFieldValueItem
    {
        [DataMember(Name = "media_id")]
        public string MediaId { get; set; }

        [DataMember(Name = "impressions")]
        public long Impressions { get; set; }

        [DataMember(Name = "reach")]
        public long Reach { get; set; }

        [DataMember(Name = "taps_forward")]
        public long TapsForward { get; set; }

        [DataMember(Name = "taps_back")]
        public long TapsBack { get; set; }

        [DataMember(Name = "exits")]
        public long Exits { get; set; }

        [DataMember(Name = "replies")]
        public long Replies { get; set; }
    }

    public class FacebookAccount : IHasPublisherAccountId
    {
        public long PublisherAccountId { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string About { get; set; }
        public string Link { get; set; }
        public string UserName { get; set; }
        public string Website { get; set; }
        public FacebookIgBusinessAccount InstagramBusinessAccount { get; set; }
    }

    public class FacebookIgBusinessAccount
    {
        public string Id { get; set; }
        public long FollowersCount { get; set; }
        public long FollowsCount { get; set; }
        public string Name { get; set; }
        public long InstagramId { get; set; }
        public long MediaCount { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string UserName { get; set; }
        public string Website { get; set; }
        public string Description { get; set; }

        public RydrAccountType? LinkedAsAccountType { get; set; }
    }

    public class FacebookBusiness
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProfilePictureUrl { get; set; }
        public List<string> PermittedRoles { get; set; }
    }

    public class FacebookPlaceInfo
    {
        public string Id { get; set; }
        public string CoverPhotoUrl { get; set; }
        public string Description { get; set; }
        public bool IsPermanentlyClosed { get; set; }
        public bool IsVerified { get; set; }
        public string FbUrl { get; set; }
        public Address Location { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string SingleLineAddress { get; set; }
        public string Website { get; set; }
        public long RydrPlaceId { get; set; }
    }
}
