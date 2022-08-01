using System;
using System.Collections.Generic;
using Funq;
using MySql.Data.MySqlClient;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Messaging;

namespace Rydr.Api.Core.Configuration
{
    public abstract class MessagingConfiguration : IAppHostConfigurer
    {
        private readonly List<string> _registeredMessageQueueTypeNames = new List<string>();

        public const int DefaultRetryCount = 3;

        protected readonly ILog _log;

        protected MessagingConfiguration()
        {
            _log = LogManager.GetLogger(GetType());
        }

        public IMessageService MqHost { get; private set; }

        public virtual void Apply(ServiceStackHost appHost, Container container)
        {
            QueueNames.SetQueuePrefix(RydrEnvironment.GetAppSetting("AWS.SQS.QueueNamePrefix", ""));

            RegisterMq(appHost, container);
        }

        private void RegisterMq(ServiceStackHost appHost, Container container)
        {
            MqHost = GetMessageService(container);

            container.Register(MqHost);
            container.Register(c => MqHost.MessageFactory);

            if (RydrEnvironment.GetAppSetting("Messaging.DisableReadHandling", false))
            {
                _log.Info("Message READ handling disabled on this service due to Messaging.DisableReadHandling configuration setting being on.");
            }
            else
            { // Those which depend on the config
                RegisterWithConfigThreads(appHost, MqHost, 2,
                                          dlqProcessPredicates: new Func<IMessage<PostDeferredAffected>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => ra > (DefaultRetryCount * 2) ||
                                                                                 (DateTimeHelper.UtcNow - msg.CreatedDate).TotalDays > 35
                                                                                     ? MqProcessType.Archive
                                                                                     : MqProcessType.Unspecified,
                                                                    (msg, ra) => msg.Error == null ||
                                                                                 !msg.Error.ErrorCode.ContainsSafe(nameof(MySqlException)) ||
                                                                                 !msg.Error.Message.ContainsSafe("Duplicate entry ")
                                                                                     ? MqProcessType.Unspecified
                                                                                     : MqProcessType.Ignore,
                                                                    (msg, ra) => msg.Error == null ||
                                                                                 !msg.Error.ErrorCode.ContainsSafe(nameof(RecordNotFoundException)) ||
                                                                                 !msg.Error.Message.ContainsSafe("Record was not found ")
                                                                                     ? MqProcessType.Unspecified
                                                                                     : ra > (DefaultRetryCount + 2)
                                                                                         ? MqProcessType.Archive
                                                                                         : MqProcessType.Reprocess,
                                                                });

                RegisterWithConfigThreads(appHost, MqHost, 2, isFifoQueue: true,
                                          dlqProcessPredicates: new Func<IMessage<PostDeferredFifoMessage>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => ra > (DefaultRetryCount + 1)
                                                                                     ? MqProcessType.Alert
                                                                                     : MqProcessType.Reprocess
                                                                });

                RegisterWithConfigThreads(appHost, MqHost, 2,
                                          dlqProcessPredicates: new Func<IMessage<PostDeferredDealMessage>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => ra > (DefaultRetryCount + 1)
                                                                                     ? MqProcessType.Alert
                                                                                     : MqProcessType.Reprocess
                                                                });

                RegisterWithConfigThreads(appHost, MqHost, 2,
                                          dlqProcessPredicates: new Func<IMessage<PostDeferredPrimaryDealMessage>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => msg.RetryAttempts > (DefaultRetryCount + 1)
                                                                                     ? MqProcessType.Alert
                                                                                     : MqProcessType.Reprocess
                                                                });

                RegisterWithConfigThreads(appHost, MqHost, 2,
                                          dlqProcessPredicates: new Func<IMessage<PostDeferredMessage>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => ra > (DefaultRetryCount * 2) ||
                                                                                 (DateTimeHelper.UtcNow - msg.CreatedDate).TotalDays > 35
                                                                                     ? MqProcessType.Archive
                                                                                     : MqProcessType.Unspecified
                                                                });

                RegisterWithConfigThreads(appHost, MqHost, 1,
                                          dlqProcessPredicates: new Func<IMessage<PostDeferredLowPriMessage>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => ra > (DefaultRetryCount * 2) ||
                                                                                 (DateTimeHelper.UtcNow - msg.CreatedDate).TotalDays > 35
                                                                                     ? MqProcessType.Archive
                                                                                     : MqProcessType.Unspecified
                                                                });

                RegisterWithConfigThreads(appHost, MqHost, 2, 0,
                                          dlqProcessPredicates: new Func<IMessage<PostSyncRecentPublisherAccountMedia>, long, MqProcessType>[]
                                                                {
                                                                    (msg, ra) => MqProcessType.Ignore
                                                                });
            }

            if (!_registeredMessageQueueTypeNames.IsNullOrEmpty())
            {
                container.Register("MessageQueueProcessorRegisteredTypeNames", _registeredMessageQueueTypeNames);
            }
        }

        protected abstract IMessageService GetMessageService(Container container);
        protected abstract void Register<T>(ServiceStackHost appHost, IMessageService mqHost, int threads = 0, int? retryCount = null, int? visibilityTimeoutSeconds = null, bool isFifoQueue = false);

        protected void RegisterHandler<T>(ServiceStackHost appHost, IMessageService mqHost, int threads = 0, int? retryCount = null,
                                          int? visibilityTimeoutSeconds = null, bool isFifoQueue = false,
                                          IEnumerable<Func<IMessage<T>, long, MqProcessType>> dlqProcessPredicates = null)
        {
            appHost.Container
                   .Register<IMessageQueueProcessor>(typeof(T).Name, c => new ManualMessageQueueProcessor<T>(c.Resolve<IMessageFactory>(),
                                                                                                             c.Resolve<IFileStorageProvider>(),
                                                                                                             c.Resolve<IOpsNotificationService>(),
                                                                                                             c.Resolve<IPersistentCounterAndListService>(),
                                                                                                             dlqProcessPredicates))
                   .ReusedWithin(ReuseScope.Hierarchy);

            _registeredMessageQueueTypeNames.Add(typeof(T).Name);

            if (RydrEnvironment.GetAppSetting("Messaging.DisableAll", false))
            {
                threads = 1;
            }

            Register<T>(appHost, mqHost, threads, retryCount, visibilityTimeoutSeconds, isFifoQueue);
        }

        protected void RegisterWithConfigThreads<T>(ServiceStackHost appHost, IMessageService mqHost, int defaultThreadsIfNoConfig = 0,
                                                    int defaultRetriesIfNoConfig = 3, int? visibilityTimeoutSeconds = null, bool isFifoQueue = false,
                                                    IEnumerable<Func<IMessage<T>, long, MqProcessType>> dlqProcessPredicates = null)
        {
            var threadsAppSettingName = string.Concat("Messaging.Threads.", typeof(T).Name);

            var threads = RydrEnvironment.GetAppSetting(threadsAppSettingName, defaultThreadsIfNoConfig.ToString()).ToInt(defaultThreadsIfNoConfig);

            var retriesAppSettingName = string.Concat("Messaging.RetryCount.", typeof(T).Name);

            var retries = RydrEnvironment.GetAppSetting(retriesAppSettingName, defaultRetriesIfNoConfig.ToString()).ToInt(defaultRetriesIfNoConfig);

            if (threads > 0)
            {
                RegisterHandler(appHost, mqHost, threads, retries, visibilityTimeoutSeconds, isFifoQueue, dlqProcessPredicates);
            }
        }
    }
}
