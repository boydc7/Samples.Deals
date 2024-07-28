using Funq;
using Rydr.Api.Core.Enums;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.Messaging.Redis;
using ServiceStack.Redis;

namespace Rydr.Api.Core.Configuration;

public class LiveMessagingConfiguration : MessagingConfiguration
{
    private readonly bool _useRedisMq = RydrEnvironment.GetAppSetting("Messaging.UseRedisMq", false);
    private static bool _redisMqManagerRegistered;

    protected override IMessageService GetMessageService(Container container)
    {
        if (RydrEnvironment.IsLocalEnvironment && !_useRedisMq)
        { // Locally just use in-memory transient
            _log.InfoFormat("Using [InMemoryTransientMessageService] MqHost, prefix of [{0}].", QueueNames.QueuePrefix);

            return new InMemoryTransientMessageService();
        }

        _log.InfoFormat("Using [RedisMqServer] MqHost, prefix of [{0}].", QueueNames.QueuePrefix);

        return GetRedisMessageService(container);
    }

    private IMessageService GetRedisMessageService(Container container)
    {
        if (!_redisMqManagerRegistered)
        {
            SharedConfiguration.RegisterNamedRedisProvider(ConnectionStringAppNames.Mq, container);
            _redisMqManagerRegistered = true;
        }

        var redisMqServer = new RedisMqServer(container.ResolveNamed<IRedisClientsManager>(ConnectionStringAppNames.Mq))
                            {
                                // NOTE: DO NOT set this property to false, unless you actually want to disable priority
                                //       queues, even if you plan to set this to false.  See the bug here:
                                //       https://github.com/ServiceStack/ServiceStack.Aws/blob/master/src/ServiceStack.Aws/Sqs/BaseMqServer.cs
                                //DisablePriorityQueues = false,
                                PriorityQueuesWhitelist = TypeConstants.EmptyStringArray,
                                PublishResponsesWhitelist = TypeConstants.EmptyStringArray,
                                PublishToOutqWhitelist = TypeConstants.EmptyStringArray,
                                WaitBeforeNextRestart = TimeSpan.FromSeconds(6),
                                RetryCount = DefaultRetryCount
                            };

        return redisMqServer;
    }

    protected override void Register<T>(ServiceStackHost appHost, IMessageService mqHost, int threads = 0, int? retryCount = null,
                                        int? visibilityTimeoutSeconds = null, bool isFifoQueue = false)
    {
        switch (mqHost)
        {
            case InMemoryTransientMessageService immqHost:
                immqHost.RegisterHandler<T>(appHost.ServiceController.ExecuteMessage, 1);

                return;

            case RedisMqServer rmqHost:
                rmqHost.RegisterHandler<T>(appHost.ServiceController.ExecuteMessage, threads);

                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(mqHost), "Messaging host passed to LiveMessagingConfiguration is not a supported/handled MqHost type, did something change?");
        }
    }
}

public class TransientMessagingConfiguration : MessagingConfiguration
{
    protected override IMessageService GetMessageService(Container container) => new InMemoryTransientMessageService();

    protected override void Register<T>(ServiceStackHost appHost, IMessageService mqHost, int threads = 0, int? retryCount = null, int? visibilityTimeoutSeconds = null, bool isFifoQueue = false)
    {
        // Test systems ignore the threads and retry param alltogether
        mqHost.RegisterHandler<T>(appHost.ServiceController.ExecuteMessage);
    }
}
