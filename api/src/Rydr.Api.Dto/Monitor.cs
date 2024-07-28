using ServiceStack;

namespace Rydr.Api.Dto;

[Route("/monitor")]
[Route("/monitor/{echo}")]
public class Monitor : RequestBase, IReturn<MonitorResponse>
{
    public string Echo { get; set; }
    public bool EnableRespond { get; set; }
    public bool DisableRespond { get; set; }
}

[Route("/sysmonitor/resources", "POST")]
public class MonitorSystemResources : RequestBase, IReturnVoid, IPost
{
    public bool Force { get; set; }
}

[Route("/sysmonitor/requestnotifications", "POST")]
public class MonitorRequestNotifications : RequestBase, IReturnVoid, IPost
{
    public bool Force { get; set; }
}

[Route("/sysmonitor/requestallowances", "POST")]
public class MonitorRequestAllowances : RequestBase, IReturnVoid, IPost { }

public class MonitorResponse : StatusSimpleResponse { }

[FallbackRoute("/{path*}")]
public class NoRoute : RequestBase, IReturn<SimpleResponse>
{
    public string Path { get; set; }
}
