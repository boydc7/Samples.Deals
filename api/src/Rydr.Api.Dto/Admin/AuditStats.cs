using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Admin;

[Route("/internal/admin/auditcurrentpubacctstats", "POST")]
public class AuditCurrentPublisherAccountStats : RequestBase, IReturnVoid, IHasPublisherAccountId, IPost
{
    public long PublisherAccountId { get; set; } // -1 indicates all
    public long InWorkspaceId { get; set; } // -1 indicates all
}
