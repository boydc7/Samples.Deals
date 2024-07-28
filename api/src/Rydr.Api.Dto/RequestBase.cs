using System.Runtime.Serialization;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.DataAnnotations;

namespace Rydr.Api.Dto;

public abstract class RequestBase : IRequestBase
{
    [Ignore]
    [IgnoreDataMember]
    public DateTime ReceivedAt { get; } = DateTime.UtcNow;

    public long UserId { get; set; }
    public long WorkspaceId { get; set; }
    public long RequestPublisherAccountId { get; set; }
    public long RoleId { get; set; }
    public List<string> Unset { get; set; }
    public bool ForceRefresh { get; set; }

    [Ignore]
    [IgnoreDataMember]
    public bool IsSystemRequest { get; set; }
}

public abstract class DeferrableRequestBase : RequestBase
{
    public bool IsDeferred { get; set; }
}

public abstract class RequestBaseDeferAffected<T> : RequestBase, IDeferAffected<T>
{
    protected abstract (IEnumerable<long> Ids, RecordType Type) GetMyAffected(T result);

    public (IEnumerable<long> Ids, RecordType Type) GetAffected(object result)
    {
        if (result == null || !(result is T tResult))
        {
            return (Enumerable.Empty<long>(), RecordType.Unknown);
        }

        var (ids, type) = GetMyAffected(tResult);

        return ((ids ?? Enumerable.Empty<long>()).Where(i => i > 0), type);
    }
}

public abstract class RequestBaseDeferAffectedVoid : RequestBase, IDeferAffected, IReturnVoid
{
    protected abstract (IEnumerable<long> Ids, RecordType Type) GetMyAffected();

    public (IEnumerable<long> Ids, RecordType Type) GetAffected(object result)
    {
        var (ids, type) = GetMyAffected();

        return ((ids ?? Enumerable.Empty<long>()).Where(i => i > 0), type);
    }
}
