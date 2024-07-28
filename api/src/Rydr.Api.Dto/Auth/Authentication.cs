using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using ServiceStack;

namespace Rydr.Api.Dto.Auth;

[Route("/authentication/users/{foruserid}/{tousertype}", "PUT")]
public class PutUpdateUserType : RequestBase, IReturnVoid, IPut
{
    public long ForUserId { get; set; }
    public UserType ToUserType { get; set; }
}

[Route("/authentication/register/apikey", "POST")]
public class PostApiKey : RequestBase, IReturn<SetupNewAdminResponse>, IPost
{
    public long ToUserId { get; set; }
    public string ApiKey { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

[Route("/authentication/register", "POST")]
public class PostUser : RequestBase, IReturn<OnlyResultResponse<User>>, IPost
{
    public string FirebaseToken { get; set; }
    public string FirebaseId { get; set; }
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string Phone { get; set; }
    public bool IsEmailVerified { get; set; }
    public string AuthProvider { get; set; } //
}

[Route("/authentication/connect", "POST")]
public class PostAuthenticationConnect : RequestBase, IReturn<OnlyResultResponse<ConnectedApiInfo>>, IPost
{
    public string FirebaseToken { get; set; }
    public string FirebaseId { get; set; }
    public string AuthProvider { get; set; }
    public string AuthProviderToken { get; set; }
    public string AuthProviderId { get; set; }
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string Phone { get; set; }
    public bool IsEmailVerified { get; set; }
}

[Route("/authentication/token/{foruserid}", "GET")]
public class GetAuthenticationToken : RequestBase, IReturn<OnlyResultResponse<ConnectedApiInfo>>, IGet
{
    public long ForUserId { get; set; }
}

[Route("/authentication/publisherinfo", "GET")]
public class GetAuthenticationPublisherInfo : RequestBase, IReturn<OnlyResultResponse<GetAuthenticationPublisherInfoResponse>>, IGet
{
    public PublisherType PublisherType { get; set; }
    public string PublisherId { get; set; }
    public long InWorkspaceId { get; set; }
    public long WorkspacePublisherAccountId { get; set; }
    public long PublisherAccountId { get; set; }
    public bool IncludeTempToken { get; set; }
}

[Route("/authentication/connectinfo/{useridentifier}", "GET")]
public class GetAuthenticationConnectInfo : RequestBase, IReturn<OnlyResultsResponse<ConnectedApiInfo>>, IGet, IHasUserIdentifier
{
    public string UserIdentifier { get; set; }
}

[Route("/authentication/{useridentifier}", "GET")]
public class GetAuthenticationUser : RequestBase, IReturn<OnlyResultResponse<User>>, IGet, IHasUserIdentifier
{
    public string UserIdentifier { get; set; }
}

public class SetupNewAdminResponse : ResponseBase
{
    public long UserId { get; set; }
    public string ApiKey { get; set; }
}

public class User
{
    public long Id { get; set; }
    public UserType UserType { get; set; }
    public string FullName { get; set; }
    public string Avatar { get; set; }
    public string Email { get; set; }
    public bool IsEmailVerified { get; set; }
    public string AuthProviderUserName { get; set; }
    public string AuthProviderUid { get; set; }
    public long DefaultWorkspaceId { get; set; }
    public PublisherType AuthPublisherType { get; set; }
}

public class GetAuthenticationPublisherInfoResponse
{
    public PublisherAccount PublisherAccount { get; set; }
    public ConnectedApiInfo ConnectInfo { get; set; }
    public PublisherAccount WorkspacePublisherAccount { get; set; }
}

public class ConnectedApiInfo
{
    public long OwnerUserId { get; set; }
    public string OwnerName { get; set; }
    public string OwnerEmail { get; set; }
    public string OwnerUserName { get; set; }
    public string OwnerAuthProviderId { get; set; }
    public long WorkspaceId { get; set; }
    public string WorkspaceName { get; set; }
    public string ApiKey { get; set; }
    public long ApiKeyUserId { get; set; }
    public PublisherAccount DefaultPublisherAccount { get; set; }
}
