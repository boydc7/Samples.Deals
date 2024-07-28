using Rydr.Api.Dto.Interfaces;
using ServiceStack;

namespace Rydr.Api.Dto.Admin;

[Route("/internal/admin/subscribeunlimitted", "POST")]
public class SubscribeWorksapceUnlimitted : RequestBase, IReturnVoid, IPost
{
    public long SubscribeWorkspaceId { get; set; }
}

[Route("/internal/admin/chargeusage", "POST")]
public class ChargeCompletedUsage : RequestBase, IReturn<StringIdResponse>, IPost, IHasWorkspaceIdentifier, IHasPublisherAccountIdentifier
{
    public string WorkspaceIdentifier { get; set; }
    public string PublisherIdentifier { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Limit { get; set; }
    public bool ForceRecharge { get; set; }
    public bool ForceNowUsageTimestamp { get; set; }
}

[Route("/internal/admin/payinvoice", "POST")]
public class PostPayInvoice : RequestBase, IReturn<StringIdResponse>, IPost
{
    public string InvoiceId { get; set; }
}
