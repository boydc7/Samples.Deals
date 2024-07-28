using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Publishers;

[Route("/instagram/authurl", "GET")]
public class GetInstagramAuthUrl : RequestBase, IGet, IReturn<OnlyResultResponse<StringIdResponse>>
{
    public long PublisherAppId { get; set; }
}

[Route("/instagram/authcomplete", "GET")]
[DataContract]
public class GetInstagramAuthComplete : IGet, IReturnVoid
{
    [DataMember(Name = "state")]
    public string State { get; set; }

    [DataMember(Name = "code")]
    public string Code { get; set; }

    [DataMember(Name = "error")]
    public string Error { get; set; }

    [DataMember(Name = "error_reason")]
    public string ErrorReason { get; set; }

    [DataMember(Name = "error_description")]
    public string ErrorDescription { get; set; }

    public bool HasError() => !string.IsNullOrEmpty(Error) || !string.IsNullOrEmpty(ErrorReason) || !string.IsNullOrEmpty(ErrorDescription);
}

[Route("/instagram/softconnectraw", "POST")]
public class PostInstagramSoftUserRawFeed : RequestBase, IPost, IReturn<LongIdResponse>, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public string UserName { get; set; }
    public RydrAccountType RydrAccountType { get; set; }
    public string RawFeed { get; set; }
    public string FeedUrl { get; set; }
}

[Route("/instagram/softconnect", "POST")]
public class PostInstagramSoftUser : RequestBase, IPost, IReturn<LongIdResponse>
{
    public string UserName { get; set; }
    public RydrAccountType RydrAccountType { get; set; }
    public bool Secure { get; set; }
}

[Route("/instagram/softconnectmedia", "POST")]
public class PostInstagramSoftUserMedia : RequestBase, IPost, IReturnVoid, IHasPublisherAccountId
{
    public long PublisherAccountId { get; set; }
    public string AccountId { get; set; }
    public bool Secure { get; set; }
}

[Route("/instagram/{publisheridentifier}/softconnectmediaraw", "POST")]
public class PostInstagramSoftUserMediaRawFeed : RequestBase, IPost, IHasPublisherAccountIdentifier, IReturn<OnlyResultResponse<PublisherMedia>>
{
    public string PublisherIdentifier { get; set; }
    public string RawFeed { get; set; }
    public string FeedUrl { get; set; }
}

[Route("/instagram/connectuser", "POST")]
public class PostInstagramUser : RequestBase, IPost, IHasUserIdentifier, IReturn<LongIdResponse>
{
    public string UserIdentifier { get; set; }
    public string AccountId { get; set; }
    public string UserName { get; set; }
    public string AccessToken { get; set; }
    public int ExpiresInSeconds { get; set; }
    public long MediaCount { get; set; }
    public long Follows { get; set; }
    public long FollowedBy { get; set; }
    public string FullName { get; set; }
    public string Description { get; set; }
    public string Website { get; set; }
    public string ProfilePicture { get; set; }
    public RydrAccountType RydrAccountType { get; set; }
    public WorkspaceFeature Features { get; set; }
}

[Route("/instagram/postbackuser", "POST")]
public class PostBackInstagramUser : RequestBase, IPost, IReturn<LongIdResponse>
{
    public string PostBackId { get; set; }
    public RydrAccountType RydrAccountType { get; set; }
    public string RawFeed { get; set; }
}
