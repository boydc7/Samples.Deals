using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Shared;

[Route("/internal/mq/defer", "POST")]
public class PostDeferredMessage : PostDeferredBase { }

[Route("/internal/mq/deferlow", "POST")]
public class PostDeferredLowPriMessage : PostDeferredBase { }

[Route("/internal/mq/deferdeal", "POST")]
public class PostDeferredDealMessage : PostDeferredBase { }

[Route("/internal/mq/deferprimarydeal", "POST")]
public class PostDeferredPrimaryDealMessage : PostDeferredBase { }

[Route("/internal/mq/deferfifo", "POST")]
public class PostDeferredFifoMessage : PostDeferredBase { }

[Route("/internal/mq/defer/affected", "POST")]
public class PostDeferredAffected : RequestBase, IReturnVoid, IHaveOriginalRequestId
{
    public RecordType Type { get; set; }
    public List<long> Ids { get; set; }
    public List<DynamoItemIdEdge> CompositeIds { get; set; }
    public string OriginatingRequestId { get; set; }
}

public class PostDeferredBase : RequestBase, IReturnVoid, IHaveOriginalRequestId
{
    public string Type { get; set; }
    public string Dto { get; set; }
    public string OriginatingRequestId { get; set; }
}

public class DynamoItemIdEdge : IEquatable<DynamoItemIdEdge>
{
    public DynamoItemIdEdge() { }

    public DynamoItemIdEdge(long id, string edgeId)
    {
        Id = id;
        EdgeId = edgeId;
    }

    public long Id { get; set; }
    public string EdgeId { get; set; }

    public static string GetCompositeStringId(long id, string edgeId) => string.Concat(id, "|", edgeId);

    private string _toString;

    public override string ToString() => _toString ??= (Id > 0 && EdgeId != null
                                                            ? GetCompositeStringId(Id, EdgeId)
                                                            : null);

    public bool Equals(DynamoItemIdEdge other)
        => other != null && Id == other.Id &&
           ((EdgeId == null && other.EdgeId == null) ||
            (string.Equals(EdgeId, other.EdgeId, StringComparison.OrdinalIgnoreCase)));

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

        return obj is DynamoItemIdEdge oobj && Equals(oobj);
    }

    public override int GetHashCode()
        => ToString().GetHashCode();
}
