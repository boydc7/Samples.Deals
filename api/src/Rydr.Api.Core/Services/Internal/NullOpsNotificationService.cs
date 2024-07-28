using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public class NullOpsNotificationService : IOpsNotificationService
{
    private static readonly ILog _log = LogManager.GetLogger("NullOpsNotificationService");

    private NullOpsNotificationService() { }

    public static NullOpsNotificationService Instance { get; } = new();

    public Task SendAppNotificationAsync(string subject, string message)
    {
        _log.DebugInfoFormat("AppNotification send - subject [{0}], body [{1}]", subject, message);

        return Task.CompletedTask;
    }

    public Task SendApiNotificationAsync(string subject, string message)
    {
        _log.DebugInfoFormat("ApiNotification send - subject [{0}], body [{1}]", subject, message);

        return Task.CompletedTask;
    }

    public Task SendTrackEventNotificationAsync(string eventName, string userEmail, string extraEventInfo = null)
    {
        _log.DebugInfoFormat("TrackEventNotification send - eventName [{0}], userEmail [{1}], extraEventInfo [{2}]", eventName, userEmail, extraEventInfo);

        return Task.CompletedTask;
    }

    public Task SendManagedAccountNotificationAsync(string subject, string message)
    {
        _log.DebugInfoFormat("ManagedAccountNotification send - subject [{0}], body [{1}]", subject, message);

        return Task.CompletedTask;
    }
}
