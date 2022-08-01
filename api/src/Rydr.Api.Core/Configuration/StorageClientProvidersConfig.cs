using System;
using System.Collections.Generic;
using System.Linq;
using Amazon;
using Amazon.DAX;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Funq;
using Rydr.Api.Core.DataAccess;
using Rydr.Api.Core.DataAccess.Config;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Data;
using ServiceStack.Logging;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Configuration
{
    public class StorageClientProvidersConfig
    {
        private readonly Container _container;

        private static readonly bool _dwDisabled = RydrEnvironment.GetAppSetting("Dw.DisableAll", false);
        private static readonly ILog _log = LogManager.GetLogger("StorageClientProvidersConfig");

        private static readonly bool _dwSchemaCreateDisabled = RydrEnvironment.GetAppSetting("Dw.DisableSchemaCreate", false) ||
                                                               RydrEnvironment.GetAppSetting("Dw.DisableAll", false);

        private static readonly bool _rydrSchemaCreateDisabled = RydrEnvironment.GetAppSetting("Rydr.DisableSchemaCreate", false);
        private static readonly bool _allSchemaCreateDisabled = RydrEnvironment.GetAppSetting("Environment.DisableAllSchemaCreate", false);

        public StorageClientProvidersConfig(Container container, BaseDataAccessConfiguration rydrConfiguration, BaseDataAccessConfiguration dwConfiguration)
        {
            _container = container;

            // ReSharper disable once ConvertToLocalFunction
#pragma warning disable IDE0039 // Use local function
            Func<string, List<Type>, bool, IDbCreateConfiguration> nullDbCreateConfig = (k, t, d) => NullDbCreateConfiguration.Instance;
#pragma warning restore IDE0039 // Use local function

            StorageConfigs = new List<AppStorageConfiguration>
                             {
                                 new AppStorageConfiguration
                                 {
                                     ConnectionStringAppName = ConnectionStringAppNames.Rydr,
                                     Configuration = rydrConfiguration,
                                     DbCreateConfigurationFactory = _allSchemaCreateDisabled || _rydrSchemaCreateDisabled
                                                                        ? nullDbCreateConfig
                                                                        : (k, t, d) => new OrmLiteDbCreateConfiguration(container.Resolve<IRydrSqlConnectionFactory>(), k, t.ToArray(), d)
                                 },
                                 new AppStorageConfiguration
                                 {
                                     ConnectionStringAppName = ConnectionStringAppNames.Dw,
                                     Configuration = dwConfiguration,
                                     DbCreateConfigurationFactory = _allSchemaCreateDisabled || _dwSchemaCreateDisabled
                                                                        ? nullDbCreateConfig
                                                                        : (k, t, d) => new OrmLiteDbCreateConfiguration(container.Resolve<IDwSqlConnectionFactory>(), k, t.ToArray(), d)
                                 },
                                 new AppStorageConfiguration
                                 {
                                     ConnectionStringAppName = ConnectionStringAppNames.Doc,
                                     Configuration = new DynamoDataAccessConfiguration(ConnectionStringAppNames.Doc),
                                     DbCreateConfigurationFactory = (k, t, d) => new DynamoDbCreateConfiguration(container.Resolve<IPocoDynamo>(), t, _allSchemaCreateDisabled, d)
                                 },
                                 new AppStorageConfiguration
                                 {
                                     ConnectionStringAppName = ConnectionStringAppNames.Caching,
                                     Configuration = new RedisConfiguration(ConnectionStringAppNames.Caching),
                                     DbCreateConfigurationFactory = nullDbCreateConfig
                                 }
                             };
        }

        public List<AppStorageConfiguration> StorageConfigs { get; }

        public void Configure()
        {
            // Rydr...
            var rydrDbConfig = StorageConfigs.Single(c => c.ConnectionStringAppName == ConnectionStringAppNames.Rydr && c.IsOrmLiteIntegrated).Configuration;

            var provider = rydrDbConfig.GetDialectProvider();

            provider.GetDateTimeConverter().DateStyle = DateTimeKind.Utc;

            var rydrOrmLiteConnFactory = new OrmLiteConnectionFactory(rydrDbConfig.ConnectionString(), provider, true);

            rydrOrmLiteConnFactory.RegisterConnection(ConnectionStringAppNames.Rydr, rydrDbConfig.ConnectionString(), rydrDbConfig.GetDialectProvider());

            _container.Register<IRydrSqlConnectionFactory>(c => new RydrSqlConnectionFactory(rydrOrmLiteConnFactory, c.Resolve<IRequestStateManager>()))
                      .ReusedWithin(ReuseScope.Hierarchy);

            // Dw...
            if (!_dwDisabled)
            {
                var dwDbConfig = StorageConfigs.Single(c => c.ConnectionStringAppName == ConnectionStringAppNames.Dw && c.IsOrmLiteIntegrated).Configuration;

                var dwProvider = dwDbConfig.GetDialectProvider();

                dwProvider.GetDateTimeConverter().DateStyle = DateTimeKind.Utc;

                var dwOrmLiteConnFactory = new OrmLiteConnectionFactory(dwDbConfig.ConnectionString(), dwProvider, false);

                dwOrmLiteConnFactory.RegisterConnection(ConnectionStringAppNames.Dw, dwDbConfig.ConnectionString(), dwDbConfig.GetDialectProvider());

                _container.Register<IDwSqlConnectionFactory>(c => new DwSqlConnectionFactory(rydrOrmLiteConnFactory, c.Resolve<IRequestStateManager>()))
                          .ReusedWithin(ReuseScope.Hierarchy);
            }

            _container.Register<IDbConnectionFactory>(c => c.Resolve<IRydrSqlConnectionFactory>())
                      .ReusedWithin(ReuseScope.Hierarchy);

            // Dynamo
            var dynConfig = StorageConfigs.Single(c => c.ConnectionStringAppName == ConnectionStringAppNames.Doc && !c.IsOrmLiteIntegrated).Configuration;

            var dynamoEndpoint = _container.ResolveNamed<RegionEndpoint>("Dynamo");
            var useDax = RydrEnvironment.GetAppSetting("AWS.Dynamo.UseDax", false) && !RydrEnvironment.IsLocalEnvironment;

            var config = new AmazonDynamoDBConfig
                         {
                             MaxConnectionsPerServer = 500,
                             MaxErrorRetry = 9
                         };

            if ((useDax && dynConfig.HasConnectionString) ||
                (!RydrEnvironment.IsReleaseEnvironment && dynConfig.HasConnectionString))
            { // Not a release environment and have a connection string, likely local dyncm
                if (useDax)
                {
                    _log.InfoFormat("Using DAX client and cluster connection with DynamoDb");

                    var dcc = new DaxClientConfig(dynConfig.ConnectionString(), 8111)
                              {
                                  AwsCredentials = new BasicAWSCredentials(RydrEnvironment.GetAppSetting("AWSAccessKey"),
                                                                           RydrEnvironment.GetAppSetting("AWSSecretKey")),
                                  RegionEndpoint = dynamoEndpoint,
                                  MaxPendingConnections = 250,
                                  ConnectTimeout = TimeSpan.FromSeconds(9.0),
                                  RequestTimeout = TimeSpan.FromMinutes(3),
                                  ReadRetryCount = 4,
                                  WriteRetryCount = 4
                              };

                    _container.Register<IAmazonDynamoDB>(c => new ClusterDaxClient(dcc))
                              .ReusedWithin(ReuseScope.Hierarchy);
                }
                else
                { // Not a release environment and have a connection string, likely local dyncm
                    // Not a DAX config, probably local dynamo or similar
                    _log.InfoFormat("Using standard client and specific connection string (localDynamo likely) with DynamoDb");

                    config.ServiceURL = dynConfig.ConnectionString();

                    _container.Register<IAmazonDynamoDB>(c => new AmazonDynamoDBClient(RydrEnvironment.GetAppSetting("AWSAccessKey"), RydrEnvironment.GetAppSetting("AWSSecretKey"), config))
                              .ReusedWithin(ReuseScope.Hierarchy);
                }
            }
            else
            { // Standard old Dynamo...
                _log.InfoFormat("Using standard client and standard AWS PaaS connection with DynamoDb");

                config.RegionEndpoint = dynamoEndpoint;

                _container.Register<IAmazonDynamoDB>(c => new AmazonDynamoDBClient(RydrEnvironment.GetAppSetting("AWSAccessKey"), RydrEnvironment.GetAppSetting("AWSSecretKey"), config))
                          .ReusedWithin(ReuseScope.Hierarchy);
            }

            // PocoDynamo registration
            _container.Register<IPocoDynamo>(c => new CachedPocoDynamo(new PocoDynamo(c.Resolve<IAmazonDynamoDB>())
                                                                       {
                                                                           ReadCapacityUnits = 1,
                                                                           WriteCapacityUnits = 1,
                                                                           ConsistentRead = false,
                                                                           ScanIndexForward = false,
                                                                           PagingLimit = 5000
                                                                       },
                                                                       c.LazyResolve<ILocalRequestCacheClient>()))
                      .ReusedWithin(ReuseScope.Hierarchy);

            _container.Register(c => c.Resolve<IPocoDynamo>().Sequences)
                      .ReusedWithin(ReuseScope.Hierarchy);
        }
    }
}
