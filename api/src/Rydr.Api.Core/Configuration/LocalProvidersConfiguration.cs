using Funq;
using Rydr.Api.Core.DataAccess.Config;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Configuration
{
    public class LocalProvidersConfiguration : SharedProvidersConfiguration
    {
        protected override BaseDataAccessConfiguration RydrConfiguration => RydrEnvironment.GetAppSetting("Environment.UseRealStorageProviders", true)
                                                                                ? new MySqlConfiguration(ConnectionStringAppNames.Rydr)
                                                                                : (BaseDataAccessConfiguration)new SqlLiteConfiguration(ConnectionStringAppNames.Rydr);

        protected override BaseDataAccessConfiguration DwConfiguration => new SqlLiteConfiguration(ConnectionStringAppNames.Dw);

        public override bool DropExistingTablesOnDbConfig => ForceDbRecreate || _forceSchemaDelete;

        protected override void RegisterCaching(Container container)
        {
            container.Register<ICounterAndListService>(InMemoryCounterAndListService.Instance);

            container.Register(CacheExtensions.InMemoryCacheInstance);
            container.Register<IPersistentCounterAndListService>(InMemoryCounterAndListService.Instance);

            container.Register<IDistributedLockService>(c => new CacheDistributedLockService(CacheExtensions.InMemoryCacheInstance))
                     .ReusedWithin(ReuseScope.Hierarchy);
        }

        protected override void RegisterFileStorageProviders(Container container)
            => container.Register(c => c.ResolveNamed<IFileStorageProvider>(FileStorageProviderType.FileSystem.ToString()))
                        .ReusedWithin(ReuseScope.Hierarchy);

        protected override void RegisterStaticServices(Container container)
        {
            container.Register<IFileStorageService>(c => new LocalFileStorageService(c.Resolve<IFileStorageProvider>(),
                                                                                     c.Resolve<IPocoDynamo>(),
                                                                                     c.Resolve<IAuthorizationService>()));
        }
    }
}
