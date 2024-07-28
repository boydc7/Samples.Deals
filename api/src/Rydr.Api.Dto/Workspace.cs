using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using ServiceStack;

namespace Rydr.Api.Dto;

[Route("/workspaces/{id}", "GET")]
public class GetWorkspace : BaseGetRequest<Workspace> { }

[Route("/workspaces", "GET")]
public class GetWorkspaces : BaseGetManyRequest<Workspace>, IHasUserIdentifier
{
    public string UserIdentifier { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/requests", "GET")]
public class GetWorkspaceAccessRequests : BaseGetManyRequest<WorkspaceAccessRequest>, IPagedRequest, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users", "GET")]
public class GetWorkspaceUsers : BaseGetManyRequest<WorkspaceUser>, IHasWorkspaceIdentifier, IPagedRequest
{
    public string WorkspaceIdentifier { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users/{workspaceuseridentifier}/publisheraccts", "GET")]
public class GetWorkspaceUserPublisherAccounts : BaseGetManyRequest<WorkspacePublisherAccountInfo>, IHasWorkspaceIdentifier, IPagedRequest
{
    public string WorkspaceIdentifier { get; set; }
    public string WorkspaceUserIdentifier { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool Unlinked { get; set; }
    public RydrAccountType RydrAccountType { get; set; }
    public long PublisherAccountId { get; set; }
    public string UserNamePrefix { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/publisheraccts", "GET")]
public class GetWorkspacePublisherAccounts : BaseGetManyRequest<WorkspacePublisherAccountInfo>, IHasWorkspaceIdentifier, IPagedRequest
{
    public string WorkspaceIdentifier { get; set; }
    public string UserNamePrefix { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/publisheraccts/{publisheridentifier}/users", "GET")]
public class GetWorkspacePublisherAccountUsers : BaseGetManyRequest<WorkspaceUser>, IHasWorkspaceIdentifier, IHasPublisherAccountIdentifier, IPagedRequest
{
    public string WorkspaceIdentifier { get; set; }
    public string PublisherIdentifier { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

[Route("/workspaces", "POST")]
public class PostWorkspace : RequestBaseDeferAffected<OnlyResultResponse<Workspace>>, IRequestBaseWithModel<Workspace>, IPost
{
    public Workspace Model { get; set; }
    public List<PublisherAccount> LinkAccounts { get; set; }

    protected override (IEnumerable<long> Ids, RecordType Type) GetMyAffected(OnlyResultResponse<Workspace> result)
        => ((result.Result?.Id ?? 0).Gz(Model.Id).AsEnumerable(), RecordType.Workspace);
}

[Route("/workspaces/{id}", "PUT")]
public class PutWorkspace : BasePutRequest<Workspace>
{
    protected override RecordType GetRecordType() => RecordType.Workspace;
}

[Route("/workspaces/{id}", "DELETE")]
public class DeleteWorkspace : BaseDeleteRequest
{
    protected override RecordType GetRecordType() => RecordType.Workspace;
}

[Route("/workspaces/{invitetoken}/requests", "POST")]
public class PostRequestWorkspaceAccess : RequestBase, IPost, IReturnVoid, IHasUserIdentifier
{
    public string InviteToken { get; set; }
    public string UserIdentifier { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/requests/{requesteduserid}", "DELETE")]
public class DeleteWorkspaceAccessRequest : RequestBase, IDelete, IReturnVoid, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public long RequestedUserId { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/tokenacct", "PUT")]
public class PutLinkWorkspaceTokenAccount : RequestBase, IReturnVoid, IPut, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public PublisherAccount TokenAccount { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users/{linkuserid}", "PUT")]
public class PutLinkWorkspaceUser : RequestBase, IPut, IReturnVoid, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public long LinkUserId { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users/{linkeduserid}", "DELETE")]
public class DeleteLinkedWorkspaceUser : RequestBase, IDelete, IReturnVoid, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public long LinkedUserId { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users/{useridentifier}/userroles", "PUT")]
public class PutWorkspaceUserRole : RequestBase, IPut, IReturnVoid, IHasWorkspaceIdentifier, IHasUserIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public string UserIdentifier { get; set; }
    public WorkspaceRole WorkspaceRole { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users/{workspaceuserid}/publisheraccts/{publisheraccountid}", "PUT")]
public class PutLinkWorkspaceUserPublisherAccount : RequestBase, IPut, IReturnVoid, IHasPublisherAccountId, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public long WorkspaceUserId { get; set; }
    public long PublisherAccountId { get; set; }
}

[Route("/workspaces/{workspaceidentifier}/users/{workspaceuserid}/publisheraccts/{publisheraccountid}", "DELETE")]
public class DeleteLinkWorkspaceUserPublisherAccount : RequestBase, IDelete, IReturnVoid, IHasPublisherAccountId, IHasWorkspaceIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public long WorkspaceUserId { get; set; }
    public long PublisherAccountId { get; set; }
}

// DEFERRED actions in response to deal actions
[Route("/internal/workspaces/delete", "POST")]
public class DeleteWorkspaceInternal : RequestBase, IReturnVoid, IPost
{
    public long Id { get; set; }
    public bool DeleteWorkspacePublisherAccount { get; set; }
}

[Route("/internal/workspaces/deleted", "POST")]
public class WorkspaceDeleted : RequestBase, IReturnVoid, IPost
{
    public long Id { get; set; }
}

[Route("/internal/workspaces/posted", "POST")]
public class WorkspacePosted : RequestBase, IReturnVoid, IPost
{
    public long Id { get; set; }
}

[Route("/internal/workspaces/updated", "POST")]
public class WorkspaceUpdated : RequestBase, IReturnVoid, IPost
{
    public long Id { get; set; }
    public WorkspaceType FromWorkspaceType { get; set; }
    public WorkspaceType ToWorkspaceType { get; set; }
}

[Route("/internal/workspaceusers/linked", "POST")]
public class WorkspaceUserLinked : RequestBase, IReturnVoid, IPost
{
    public long RydrUserId { get; set; }
    public long WorkspaceUserId { get; set; }
    public long InWorkspaceId { get; set; }
}

[Route("/internal/workspaceusers/updated", "POST")]
public class WorkspaceUserUpdated : RequestBase, IReturnVoid, IPost
{
    public long RydrUserId { get; set; }
    public long WorkspaceUserId { get; set; }
    public long InWorkspaceId { get; set; }
    public WorkspaceRole WorkspaceRole { get; set; }
}

[Route("/internal/workspaceusers/delinked", "POST")]
public class WorkspaceUserDelinked : RequestBase, IReturnVoid, IPost
{
    public long RydrUserId { get; set; }
    public long WorkspaceUserId { get; set; }
    public long InWorkspaceId { get; set; }
}

[Route("/internal/workspaceuserpublishers/linked", "POST")]
public class WorkspaceUserPublisherAccountLinked : RequestBase, IReturnVoid, IPost
{
    public long RydrUserId { get; set; }
    public long WorkspaceUserId { get; set; }
    public long InWorkspaceId { get; set; }
    public long ToPublisherAccountId { get; set; }
    public bool IsValidateRequest { get; set; }
}

[Route("/internal/workspaceuserpublishers/delinked", "POST")]
public class WorkspaceUserPublisherAccountDelinked : RequestBase, IReturnVoid, IPost
{
    public long RydrUserId { get; set; }
    public long WorkspaceUserId { get; set; }
    public long InWorkspaceId { get; set; }
    public long FromPublisherAccountId { get; set; }
}

[Route("/internal/workspaces/validatesubscription", "POST")]
public class ValidateWorkspaceSubscription : RequestBase, IReturnVoid, IPost, IHasPublisherAccountId
{
    public string StripeSubscriptionId { get; set; }
    public long SubscriptionWorkspaceId { get; set; }
    public long PublisherAccountId { get; set; }
    public string Message { get; set; }
}

public class Workspace : BaseDateTimeDeleteTrackedDtoModel, IHasSettableId
{
    public long Id { get; set; }
    public string Name { get; set; }
    public WorkspaceType WorkspaceType { get; set; }
    public WorkspaceFeature WorkspaceFeatures { get; set; }

    // Response only properties
    public SubscriptionType SubscriptionType { get; set; }
    public WorkspaceRole WorkspaceRole { get; set; }
    public long DefaultPublisherAccountId { get; set; }
    public long OwnerId { get; set; }
    public string InviteCode { get; set; }
    public long AccessRequests { get; set; }
    public IReadOnlyList<WorkspacePublisherAccountInfo> PublisherAccountInfo { get; set; }
    public bool? RequiresReauth { get; set; }
}

public class WorkspacePublisherAccountInfo
{
    public PublisherAccountProfile PublisherAccountProfile { get; set; }
    public long UnreadNotifications { get; set; }
    public long FollowerCount { get; set; }
    public SubscriptionType SubscriptionType { get; set; }
}

public class WorkspaceAccessRequest : WorkspaceUser
{
    public DateTime RequestedOn { get; set; }
}

public class WorkspaceUser
{
    public long UserId { get; set; }
    public string Name { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public string Avatar { get; set; }
    public WorkspaceRole WorkspaceRole { get; set; }
}
