using Funq;
using Rydr.Api.Core.DataAccess.Config;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Redis;

namespace Rydr.Api.Core.Configuration;

public class LiveProvidersConfiguration : SharedProvidersConfiguration
{
    protected override BaseDataAccessConfiguration RydrConfiguration => new MySqlConfiguration(ConnectionStringAppNames.Rydr);
    protected override BaseDataAccessConfiguration DwConfiguration => new RedshiftConfiguration(ConnectionStringAppNames.Dw);

    public override bool DropExistingTablesOnDbConfig => (ForceDbRecreate || _forceSchemaDelete) && !_rydrSchemaCreateDisabled && !_allSchemaCreateDisabled;

    protected override void RegisterCaching(Container container)
    {
        container.Register<ICounterAndListService>(InMemoryCounterAndListService.Instance);

        // Always register the Redis instance for caching, we use it for other things aside from just the ICacheClient implementation
        SharedConfiguration.RegisterNamedRedisProvider(ConnectionStringAppNames.Caching, container);

        container.Register(c => c.ResolveNamed<IRedisClientsManager>(ConnectionStringAppNames.Caching).GetCacheClient())
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IDistributedLockService>(c => new CacheDistributedLockService(c.ResolveNamed<IRedisClientsManager>(ConnectionStringAppNames.Caching).GetCacheClient()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IPersistentCounterAndListService>(c => new RedisCounterAndListService(c.ResolveNamed<IRedisClientsManager>(ConnectionStringAppNames.Caching)))
                 .ReusedWithin(ReuseScope.Hierarchy);
    }

    protected override void RegisterFileStorageProviders(Container container)
    {
        // Default
        container.Register(c => c.ResolveNamed<IFileStorageProvider>(FileStorageProviderType.S3.ToString()))
                 .ReusedWithin(ReuseScope.Hierarchy);
    }

    protected override void RegisterStaticServices(Container container)
    {
        container.Register<IFileStorageService>(c => new S3FileStorageService(c.Resolve<IFileStorageProvider>(),
                                                                              c.Resolve<IPocoDynamo>(),
                                                                              c.Resolve<IAuthorizationService>(),
                                                                              c.Resolve<IDeferRequestsService>()));
    }
}
