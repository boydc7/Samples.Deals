using ServiceStack;

namespace Rydr.Api.Dto.Shared;

[Route("/internal/mq/retry")]
public class MqRetry : RequestBase, IReturn<MqRetryResponse>
{
    public string TypeName { get; set; }
    public int Limit { get; set; }
    public bool ProcessInQ { get; set; }
}

public class MqRetryResponse : ResponseBase
{
    public int IgnoredCount { get; set; }
    public int ArchivedCount { get; set; }
    public int AttemptCount { get; set; }
    public int FailCount { get; set; }
}
