using System.Reflection;
using Amazon.S3;
using Funq;
using Rydr.Api.Core.DataAccess.Config;
using Rydr.Api.Core.DataAccess.Providers;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Filters;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.Logging;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Configuration;

public abstract class SharedProvidersConfiguration : IAppHostConfigurer
{
    private static readonly bool _dwDisabled = RydrEnvironment.GetAppSetting("Dw.DisableAll", false);
    private static readonly ILog _log = LogManager.GetLogger("SharedProvidersConfiguration");

    private bool _forceDbRecreate;
    private StorageClientProvidersConfig _storageClientProvidersConfig;

    protected readonly bool _forceSchemaDelete = RydrEnvironment.GetAppSetting("Rydr.ForceSchemaDelete", false);
    protected readonly bool _rydrSchemaCreateDisabled = RydrEnvironment.GetAppSetting("Rydr.DisableSchemaCreate", false);
    protected static readonly bool _allSchemaCreateDisabled = RydrEnvironment.GetAppSetting("Environment.DisableAllSchemaCreate", false);

    protected abstract BaseDataAccessConfiguration RydrConfiguration { get; }
    protected abstract BaseDataAccessConfiguration DwConfiguration { get; }

    public abstract bool DropExistingTablesOnDbConfig { get; }

    protected abstract void RegisterFileStorageProviders(Container container);
    protected abstract void RegisterCaching(Container container);
    protected abstract void RegisterStaticServices(Container container);

    public bool ForceDbRecreate
    {
        get => _forceDbRecreate && !RydrEnvironment.IsReleaseEnvironment;
        set => _forceDbRecreate = value;
    }

    public void Setup(Container container)
    {
        OrmLiteConfig.DialectProvider = MySqlDialect.Provider;
        OrmLiteConfig.DialectProvider.GetDateTimeConverter().DateStyle = DateTimeKind.Utc;

        _storageClientProvidersConfig = new StorageClientProvidersConfig(container, RydrConfiguration, DwConfiguration);

        _storageClientProvidersConfig.Configure();
    }

    public void Apply(ServiceStackHost appHost, Container container)
    {
        if (_allSchemaCreateDisabled)
        {
            _log.DebugInfo("ALL Schema creation and configuration is disabled, no schema items will be configured");
        }

        RegisterDataModelsForNamespace("Rydr.Api.Core", "Rydr.Api.Core.Models.Rydr", ConnectionStringAppNames.Rydr, _storageClientProvidersConfig.StorageConfigs);
        RegisterDataModelsForNamespace("Rydr.Api.Core", "Rydr.Api.Core.Models.Doc", ConnectionStringAppNames.Doc, _storageClientProvidersConfig.StorageConfigs);

        if (!_dwDisabled)
        {
            RegisterDataModelsForNamespace("Rydr.Api.Core", "Rydr.Api.Core.Models.Dw", ConnectionStringAppNames.Dw, _storageClientProvidersConfig.StorageConfigs);
        }

        RegisterSharedCaching(container);

        RegisterSharedFileStorageProviders(container);

        RegisterStaticServices(container);

        // Set global defaults in OrmLite
        OrmLiteConfig.CommandTimeout = 10000;
        OrmLiteConfig.StripUpperInLike = true;
        OrmLiteConfig.DialectProvider = MySqlDialect.Provider;
        OrmLiteConfig.SkipForeignKeys = true;
        OrmLiteConfig.DialectProvider.GetDateTimeConverter().DateStyle = DateTimeKind.Utc;

        OrmLiteConfig.SqlExpressionSelectFilter = q =>
                                                  {
                                                      if (q.ModelDef.ModelType.ImplementsInterface<IDateTimeDeleteTracked>() &&
                                                          (q.WhereExpression == null || !q.WhereExpression.Contains("DeletedOn")))
                                                      {
                                                          q.Where<IDateTimeDeleteTracked>(d => d.DeletedOn == null);
                                                      }
                                                  };
    }

    private void RegisterSharedFileStorageProviders(Container container)
    {
        // Named representations
        container.Register<IFileStorageProvider>(FileStorageProviderType.Null.ToString(), c => new NullFileStorageProvider())
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IFileStorageProvider>(FileStorageProviderType.FileSystem.ToString(), c => new FileSystemFileStorageProvider())
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IFileStorageProvider>(FileStorageProviderType.S3.ToString(), c => new S3FileStorageProvider(c.Resolve<IAmazonS3>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.Register<IFileStorageProvider>(FileStorageProviderType.InMemory.ToString(), new InMemoryFileStorageProvider());

        RegisterFileStorageProviders(container);

        container.RegisterAutoWiredAs<AwsImageAnalysisService, IImageAnalysisService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.RegisterAutoWiredAs<AwsTextAnalysisService, ITextAnalysisService>()
                 .ReusedWithin(ReuseScope.Hierarchy);

        container.RegisterAutoWiredAs<TimestampedLabelTaxonomyProcessingFilter, ILabelTaxonomyProcessingFilter>()
                 .ReusedWithin(ReuseScope.Hierarchy);
    }

    private void RegisterSharedCaching(Container container)
    {
        RegisterCaching(container);

        container.Register<ILocalRequestCacheClient>(c => new InMemoryCacheClient())
                 .ReusedWithin(ReuseScope.Request);

        container.Register<ILocalDistributedCacheClient>(c => new LocalDistributedCacheClient(c.Resolve<ICacheClient>(),
                                                                                              c.LazyResolve<ILocalRequestCacheClient>(),
                                                                                              c.Resolve<IDateTimeProvider>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        // If caching is disabled entirely, or at the services level, simple  null invalidator
        if (RydrEnvironment.GetAppSetting("Caching.DisableAll", false) ||
            RydrEnvironment.GetAppSetting("Caching.DisableServices", false))
        {
            container.Register<IServiceCacheInvalidator>(NullServiceCacheInvalidator.Instance);
        }
        else
        {
            container.Register<IServiceCacheInvalidator>(c => new AsyncServiceCacheInvalidator(new DefaultServiceCacheInvalidator(c.Resolve<ILocalDistributedCacheClient>(),
                                                                                                                                  c.Resolve<IDateTimeProvider>(),
                                                                                                                                  c.Resolve<IRequestStateManager>(),
                                                                                                                                  c.Resolve<IPublisherAccountService>(),
                                                                                                                                  c.Resolve<IAssociationService>())))
                     .ReusedWithin(ReuseScope.Hierarchy);
        }
    }

    private void RegisterDataModelsForNamespace(string assemblyName, string assemblyNamespace, string appName,
                                                List<AppStorageConfiguration> storageConfigs, bool forceNoDropExistingOnDbConfig = false)
    {
        var coreAssembly = Assembly.Load(assemblyName);

        var assemblyNamespaceWildcard = string.Concat(assemblyNamespace, ".");

        var types = coreAssembly.GetTypes()
                                .Where(t => t.Namespace.EqualsOrdinal(assemblyNamespace, false) || t.Namespace.StartsWithOrdinalCi(assemblyNamespaceWildcard))
                                .Where(t => !t.IsInterface && !t.ContainsGenericParameters && !t.IsAbstract && !t.IsEnum && !t.IsCompilerGenerated())
                                .Where(t => !t.HasAttribute<DbViewAttribute>())
                                .ToList();

        var dropExisting = !forceNoDropExistingOnDbConfig && DropExistingTablesOnDbConfig;

        foreach (var storageConfig in storageConfigs.Where(s => s.ConnectionStringAppName.EqualsOrdinalCi(appName)))
        {
            _log.DebugInfoFormat("Configuring storage models: [{0}]{1}", storageConfig.ConnectionStringAppName,
                                 dropExisting
                                     ? " (with drop existing)"
                                     : string.Empty);

            storageConfig.DbCreateConfigurationFactory(storageConfig.Configuration.ConnectionFactoryKeyName, types, dropExisting)
                         .Configure();
        }
    }
}
