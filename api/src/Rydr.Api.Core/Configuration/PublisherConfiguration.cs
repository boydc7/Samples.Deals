using Funq;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Configuration;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Configuration
{
    public class PublisherConfiguration : IAppHostConfigurer
    {
        public void Apply(ServiceStackHost appHost, Container container)
        {
            FacebookSdkConfig.CacheClientFactory = container.LazyResolve<ICacheClient>();
            FacebookSdkConfig.ETagDisabled = RydrEnvironment.GetAppSetting("Facebook.ETag.Disabled", false);

#if LOCALDEBUG
            FacebookSdkConfig.StaticFbSystemToken = "EAAEc3MyzU2QBAJAtlZCoGZAmFXapnEzKoZAvcLRTJwKOtwalniQNGBgibFzPpExNUZBXHLD1AcmxhnvXhttPodspWyJPG5HHXCCW4LFguQIo7dhguVZAz84cUvzA4H9ZAM7h4bpwNNTjXjwIKp0GKvvkzkcRZCWZCNWdeLisAcHKv6tPZCthUpqpdgRIUtOlLm0nNKMibVXZAHsgZDZD";
#else
            var secretService = container.Resolve<ISecretService>();
            FacebookSdkConfig.StaticFbSystemToken = secretService.GetSecretStringAsync($"RydrApi.StaticFbSystemToken.{RydrEnvironment.CurrentEnvironment}").GetAwaiter().GetResult();
#endif

            container.Register<IPublisherAccountConnectionDecorator>(c => new CompositePublisherAccountConnectionDecorator(new IPublisherAccountConnectionDecorator[]
                                                                                                                           {
                                                                                                                               new SoftLinkedPublisherAccountConnectionDecorator(c.Resolve<IRydrDataService>()), new BasicIgPublisherAccountConnectionDecorator(c.Resolve<IRydrDataService>())
                                                                                                                           }))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IPublisherDataService>(PublisherType.Google.ToString(), NullPublisherDataService.Instance);
            container.Register<IPublisherDataService>(PublisherType.Rydr.ToString(), NullPublisherDataService.Instance);
            container.Register<IPublisherDataService>(PublisherType.Firebase.ToString(), NullPublisherDataService.Instance);

            container.Register<IPublisherDataService>(PublisherType.Instagram.ToString(),
                                                      c => new InstagramPublisherDataService(c.Resolve<IPocoDynamo>(),
                                                                                             c.Resolve<IAuthorizationService>(),
                                                                                             c.Resolve<IEncryptionService>(),
                                                                                             c.Resolve<IRequestStateManager>(),
                                                                                             c.Resolve<IPublisherAccountService>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IPublisherDataService>(PublisherType.Facebook.ToString(),
                                                      c => new FacebookPublisherDataService(c.Resolve<IPocoDynamo>(),
                                                                                            c.Resolve<IAuthorizationService>(),
                                                                                            c.Resolve<IEncryptionService>(),
                                                                                            c.Resolve<IRequestStateManager>(),
                                                                                            c.Resolve<IPublisherAccountService>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IPublisherMediaStorageService>(c => new DynamoPublisherMediaStorageService(c.Resolve<IPocoDynamo>(),
                                                                                                          new IPublisherMediaStatDecorator[]
                                                                                                          {
                                                                                                              new EngagementsCalcMediaStatDecorator(),
                                                                                                              new EngagementRatingCalcMediaStatDecorator(c.Resolve<IPublisherAccountService>()),
                                                                                                              new PublisherAccountEngagementCalcMediaStatDecorator(c.Resolve<IPocoDynamo>(),
                                                                                                                                                                   c.Resolve<IPublisherAccountService>())
                                                                                                          },
                                                                                                          c.Resolve<IServiceCacheInvalidator>(),
                                                                                                          c.Resolve<IRydrDataService>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IPublisherMediaSingleStorageService>(c => new DynamoPublisherMediaStorageService(c.Resolve<IPocoDynamo>(),
                                                                                                                new IPublisherMediaStatDecorator[]
                                                                                                                {
                                                                                                                    new EngagementsCalcMediaStatDecorator(), new EngagementRatingCalcMediaStatDecorator(c.Resolve<IPublisherAccountService>())
                                                                                                                },
                                                                                                                c.Resolve<IServiceCacheInvalidator>(),
                                                                                                                c.Resolve<IRydrDataService>()))
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<IPublisherMediaSyncService>(PublisherType.Google.ToString(), NullPublisherMediaService.Instance);
            container.Register<IPublisherMediaSyncService>(PublisherType.Rydr.ToString(), NullPublisherMediaService.Instance);
            container.Register<IPublisherMediaSyncService>(PublisherType.Firebase.ToString(), NullPublisherMediaService.Instance);

            container.RegisterAutoWiredAs<InstagramMediaSyncService, IPublisherMediaSyncService>(PublisherType.Instagram.ToString())
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<FacebookMediaSyncService, IPublisherMediaSyncService>(PublisherType.Facebook.ToString())
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<TimestampedPublisherAccountService, IPublisherAccountService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<DefaultPublisherAccountStatsService, IPublisherAccountStatsService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<TimestampedWorkspaceService, IWorkspaceService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<TimestampedWorkspaceSubscriptionService, IWorkspaceSubscriptionService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.RegisterAutoWiredAs<TimestampedWorkspacePublisherSubscriptionService, IWorkspacePublisherSubscriptionService>()
                     .ReusedWithin(ReuseScope.Hierarchy);

            container.Register<ISubscriptionPlanService>(c => new CustomSubscriptionPlanService(StaticSubscriptionPlanService.Instance,
                                                                                                c.Resolve<IPocoDynamo>()))
                     .ReusedWithin(ReuseScope.Hierarchy);
        }
    }
}
