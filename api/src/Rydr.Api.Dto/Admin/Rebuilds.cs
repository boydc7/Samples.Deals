using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Admin;

[Route("/internal/admin/rebuildesmedias", "POST")]
public class RebuildEsMedias : RequestBase, IReturnVoid, IHasPublisherAccountId, IPost
{
    public long PublisherAccountId { get; set; } // -1 indicates all
}

[Route("/internal/admin/rebuildesdeals", "POST")]
public class RebuildEsDeals : RequestBase, IReturnVoid, IHasPublisherAccountId, IPost
{
    public long PublisherAccountId { get; set; } // -1 indicates all
    public bool DeferDealAsAffected { get; set; } // Set true to push a deferAffected record for the deal
}

[Route("/internal/admin/rebuildescreators", "POST")]
public class RebuildEsCreators : RequestBase, IReturnVoid, IHasPublisherAccountId, IPost
{
    public long PublisherAccountId { get; set; } // -1 indicates all
}

[Route("/internal/admin/rebuildesbusinesses", "POST")]
public class RebuildEsBusinesses : RequestBase, IReturnVoid, IHasPublisherAccountId, IPost
{
    public long PublisherAccountId { get; set; } // -1 indicates all
}

[Route("/internal/admin/rebalancejobs", "POST")]
public class RebalanceRecurringJobs : RequestBase, IReturn<StringIdResponse>, IPost
{
    public bool DeleteOnly { get; set; }
}

[Route("/internal/admin/softlinkrydr", "POST")]
public class SoftLinkMapRydr : RequestBase, IReturnVoid, IPost
{
    public long ToWorkspaceId { get; set; }
    public long ToPublisherAccountId { get; set; }
}

[Route("/internal/admin/uncancelrequest", "PUT")]
public class PutUncancelDealRequest : RequestBase, IReturn<StringIdResponse>, IPost, IHasPublisherAccountId
{
    public long DealId { get; set; }
    public long PublisherAccountId { get; set; }
}
