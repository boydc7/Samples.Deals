using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Messaging;

namespace Rydr.Api.Services.Helpers;

public class MqHostHelper
{
    private static readonly ILog _log = LogManager.GetLogger("MqHostHelper");

    private MqHostHelper() { }

    public static MqHostHelper Instance { get; } = new();

    public void ShutdownHosts(IMessageService mqHost, IServerEvents eventBroker)
    {
        var mqTask = Task.Run(() => ShutdownMqHost(mqHost));

        Task.WaitAll(new[]
                     {
                         mqTask
                     }, TimeSpan.FromMinutes(9));
    }

    private void ShutdownMqHost(IMessageService mqHost)
    {
        if (mqHost == null)
        {
            return;
        }

        try
        {
            _log.Info("Shutdown of MqHost starting");
            mqHost.Stop();
            _log.Info("Shutdown of MqHost complete");
        }
        catch(Exception ex)
        {
            _log.Warn("Exception trying to Shutdown the MqHost.", ex);
        }
    }
}
