using Funq;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Redis.StackExchange;
using Rydr.Api.Core.DataAccess.Config;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Internal;
using ServiceStack;
using StackExchange.Redis;

namespace Rydr.Api.Core.Configuration;

public class HangfireConfiguration : IAppHostConfigurer
{
    public void Apply(ServiceStackHost appHost, Container container)
    {
        if (RydrEnvironment.IsLocalEnvironment)
        {
            GlobalConfiguration.Configuration.UseMemoryStorage(new MemoryStorageOptions
                                                               {
                                                                   JobExpirationCheckInterval = TimeSpan.FromMinutes(30),
                                                                   CountersAggregateInterval = TimeSpan.FromMinutes(30)
                                                               });
        }
        else
        {
            var redisConnectionConfig = new RedisConfiguration(ConnectionStringAppNames.Mq);

            GlobalConfiguration.Configuration.UseStorage(new RedisStorage(ConnectionMultiplexer.Connect(redisConnectionConfig.ConnectionString()),
                                                                          new RedisStorageOptions
                                                                          {
                                                                              Prefix = "hangfire:",
                                                                              DeletedListSize = 50,
                                                                              SucceededListSize = 50,
                                                                              UseTransactions = false,
                                                                          }));
        }

        GlobalConfiguration.Configuration.UseActivator(new FunqJobActivator(container));
        GlobalConfiguration.Configuration.UseNLogLogProvider();

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
                                     {
                                         Attempts = 3,
                                         OnAttemptsExceeded = AttemptsExceededAction.Delete
                                     });

        container.AddSingleton(c => new BackgroundJobServer(new BackgroundJobServerOptions
                                                            {
                                                                WorkerCount = RydrEnvironment.GetAppSetting("Hangfire.WorkerCount", 1),
                                                                ShutdownTimeout = TimeSpan.FromSeconds(RydrEnvironment.GetAppSetting("Environment.ShutdownWaitSeconds", 600)),
                                                                StopTimeout = TimeSpan.FromSeconds(RydrEnvironment.GetAppSetting("Environment.ShutdownWaitSeconds", 600)),
                                                                SchedulePollingInterval = TimeSpan.FromSeconds(11)
                                                            }));
    }
}

public class FunqJobActivator : JobActivator
{
    private readonly Container _container;

    public FunqJobActivator(Container container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public override object ActivateJob(Type type) => _container.TryResolve(type);
}
