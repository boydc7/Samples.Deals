using System.Runtime.Serialization;
using Amazon.DynamoDBv2.DataModel;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.QueryDto.Shared;

public abstract class BaseQueryDataRequest<T> : QueryData<T>, IQueryDataRequest<T>, IReturn<RydrQueryResponse<T>>
    where T : class, ICanBeRecordLookup
{
    [Ignore]
    [IgnoreDataMember]
    public DateTime ReceivedAt { get; } = DateTime.UtcNow;

    public long UserId { get; set; }
    public long RequestPublisherAccountId { get; set; }
    public long WorkspaceId { get; set; }
    public long RoleId { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool ForceRefresh { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public int TypeId => (int)QueryDynItemType;

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public abstract DynItemType QueryDynItemType { get; }

    [Ignore]
    [IgnoreDataMember]
    public bool IsSystemRequest { get; set; }

    [Ignore]
    [IgnoreDataMember]
    [DynamoDBIgnore]
    public bool IncludeWorkspace { get; set; }
}

public abstract class BaseQueryDbRequest<T> : QueryDb<T>, IRequestBase
    where T : BaseDataModel
{
    [Ignore]
    [IgnoreDataMember]
    public DateTime ReceivedAt { get; } = DateTime.UtcNow;

    public long[] Id { get; set; }
    public long UserId { get; set; }
    public long RequestPublisherAccountId { get; set; }
    public long WorkspaceId { get; set; }
    public long RoleId { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool ForceRefresh { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public bool IsSystemRequest { get; set; }
}

public abstract class BaseDeletableQueryDbRequest<T> : BaseQueryDbRequest<T>
    where T : BaseDeleteableDataModel { }
