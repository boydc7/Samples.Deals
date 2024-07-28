using Funq;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Filters;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Messages;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Configuration;

public class LiveServicesConfiguration : IAppHostConfigurer
{
    public void Apply(ServiceStackHost appHost, Container container)
    {
        DateTimeHelper.SetDateTimeProvider(UtcOnlyDateTimeProvider.Instance);

        container.Register(UtcOnlyDateTimeProvider.Instance);

        // Stats
        var haveStatsDServer = RydrEnvironment.GetAppSetting("Stats.StatsD.Server", string.Empty).HasValue();

        if (haveStatsDServer)
        {
            container.Register<IUdpClient>(WrappedUdpClient.DefaultClient);

            //container.Register<IStats>(StatsD.Default);
            container.Register<IStats>(DogStatsD.Default);
        }
        else
        {
            container.Register<IUdpClient>(NullUdpClient.Default);
            container.Register<IStats>(NullStatsD.Default);
        }

        container.Register<ITaskExecuter>(LocalAsyncTaskExecuter.DefaultTaskExecuter);

        if (RydrEnvironment.IsDebugEnabled && RydrEnvironment.GetAppSetting("Stats.Profiler.Enabled", false))
        {
            StatsProfilerFactory.SetFactory(() => new LoggedStatsProfiler());
        }

        container.RegisterAutoWiredAs<MailKitEmailService, ISendEmailService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IAdminServiceGateway>(c => new InProcessAdminServiceGateway())
                 .ReusedWithin(ReuseScope.None);

        container.Register<ICountryLookupService>(InMemoryCountryLookupService.Instance);

        /*********************************************************************************************
        // Dialog/messaging services
        *********************************************************************************************/
        container.RegisterAutoWired<DynamoDialogService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.RegisterAutoWiredAs<CounterListDialogCountService, IDialogCountService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register(c => new CountedDialogMessageService(new DynamoPersistDialogMessageService(c.Resolve<DynamoDialogService>(),
                                                                                                      c.Resolve<IPocoDynamo>()),
                                                                c.Resolve<DynamoDialogService>(),
                                                                c.Resolve<IPersistentCounterAndListService>(),
                                                                c.Resolve<ICacheClient>(),
                                                                c.Resolve<IServerNotificationService>(),
                                                                c.Resolve<ITaskExecuter>(),
                                                                c.Resolve<IRequestStateManager>(),
                                                                c.Resolve<IDialogCountService>(),
                                                                c.Resolve<IPublisherAccountService>(),
                                                                c.Resolve<IServiceCacheInvalidator>(),
                                                                c.Resolve<IPocoDynamo>(),
                                                                c.Resolve<IRydrDataService>(),
                                                                c.Resolve<IRecordTypeRecordService>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IDialogMessageService>(c => c.Resolve<CountedDialogMessageService>())
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IDialogService>(c => c.Resolve<CountedDialogMessageService>())
                 .ReusedWithin(ReuseScope.Hierarchy);
        /********************************************************************************************/

        /*********************************************************************************************
        // Push notifications, chat, communications
        *********************************************************************************************/
        container.Register<IPushNotificationMessageFormattingService>(c => new CompositePushNotificationMessageFormattingService(new IPushNotificationMessageFormattingService[]
                                                                                                                                 {
                                                                                                                                     new ServerPushNotificationMessageFormattingService(c.Resolve<IUserNotificationService>())
                                                                                                                                 }));

        container.Register<IServerNotificationService>(c => new CompositeServerNotificationService(new List<IServerNotificationService>
                                                                                                   {
                                                                                                       new ManagedWorkspaceServerNotificationService(c.Resolve<IOpsNotificationService>(),
                                                                                                                                                     c.Resolve<IPocoDynamo>(),
                                                                                                                                                     c.Resolve<IPublisherAccountService>(),
                                                                                                                                                     c.Resolve<IPushNotificationMessageFormattingService>()),
                                                                                                       new LogServerNotificationService(c.Resolve<IPocoDynamo>(),
                                                                                                                                        c.Resolve<IRecordTypeRecordService>(),
                                                                                                                                        c.Resolve<IPersistentCounterAndListService>(),
                                                                                                                                        c.Resolve<IServiceCacheInvalidator>()),
                                                                                                       new FirebasePushServerNotificationService(c.Resolve<IPocoDynamo>(),
                                                                                                                                                 c.Resolve<ICacheClient>(),
                                                                                                                                                 c.Resolve<IPushNotificationMessageFormattingService>(),
                                                                                                                                                 c.Resolve<IPublisherAccountService>(),
                                                                                                                                                 c.Resolve<IWorkspaceService>())
                                                                                                   },
                                                                                                   c.Resolve<IPublisherAccountService>(),
                                                                                                   c.Resolve<IRecordTypeRecordService>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IUserNotificationService>(c => new LogServerNotificationService(c.Resolve<IPocoDynamo>(),
                                                                                           c.Resolve<IRecordTypeRecordService>(),
                                                                                           c.Resolve<IPersistentCounterAndListService>(),
                                                                                           c.Resolve<IServiceCacheInvalidator>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IServerNotificationSubsriptionService>(c => new FirebasePushServerNotificationService(c.Resolve<IPocoDynamo>(),
                                                                                                                 c.Resolve<ICacheClient>(),
                                                                                                                 c.Resolve<IPushNotificationMessageFormattingService>(),
                                                                                                                 c.Resolve<IPublisherAccountService>(),
                                                                                                                 c.Resolve<IWorkspaceService>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        // Release vs non-release configs
        // if (RydrEnvironment.IsReleaseEnvironment)
        // {
        //     container.Register<IOpsNotificationService>(c => new AwsOpsNotificationService(c.Resolve<IAwsSnsService>()));
        // }
        // else
        // {
        container.Register<IOpsNotificationService>(NullOpsNotificationService.Instance);

        // }

        /********************************************************************************************/

        /*********************************************************************************************
        // Model services
        *********************************************************************************************/

        container.RegisterAutoWiredAs<DynamoDealService, IDealService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.RegisterAutoWiredAs<AsyncDealMetricService, IDealMetricService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.RegisterAutoWiredAs<DynamoDealRequestService, IDealRequestService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.RegisterAutoWiredAs<CompositeDealRestrictionTypeFilter, IDealRestrictionFilterService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        /********************************************************************************************/
    }
}
