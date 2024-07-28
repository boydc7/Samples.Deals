using Rydr.Api.Dto.Enums;
using ServiceStack.Model;

namespace Rydr.Api.Dto.Interfaces;

public interface IHasWorkspaceId
{
    long WorkspaceId { get; set; }
}

public interface IHasUserId
{
    long UserId { get; set; }
}

public interface IHasUserAndWorkspaceId : IHasUserId, IHasWorkspaceId { }

public interface IHasUserAuthorizationInfo : IHasUserAndWorkspaceId
{
    long RoleId { get; set; }
    long RequestPublisherAccountId { get; set; }
    bool IsSystemRequest { get; set; }
}

public interface ICanBeAuthorized : IHasWorkspaceId, IHasCreatedBy, IHasModifiedBy
{
    long OwnerId { get; }
    AccessIntent DefaultAccessIntent();
    bool IsPubliclyReadable();
}

public interface ICanBeAuthorizedById : ICanBeAuthorized
{
    long AuthorizeId();
}

public interface ICanBeRecordLookup : ICanBeAuthorized, IHasLongId { }

public interface IHasPublisherAccountId
{
    long PublisherAccountId { get; }
}

public interface IHasDealId
{
    long DealId { get; }
}

public interface IHasPublisherAccountIdentifier
{
    string PublisherIdentifier { get; set; }
}

public interface IHasWorkspaceIdentifier
{
    string WorkspaceIdentifier { get; set; }
}

public interface IHasUserIdentifier
{
    string UserIdentifier { get; set; }
}
