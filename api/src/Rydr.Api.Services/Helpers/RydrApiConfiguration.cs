using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Funq;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.QueryDto;
using Rydr.Api.Services.Services;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite;

namespace Rydr.Api.Services.Helpers
{
    public class RydrApiConfiguration
    {
        private readonly ServiceStackHost _appHost;
        private readonly Container _container;

        public RydrApiConfiguration(ServiceStackHost appHost, Container container)
        {
            _appHost = appHost;
            _container = container;
        }

        public void ApplyConfigs()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            _container.RegisterAutoWiredAs<AwsSecretService, ISecretService>()
                      .ReusedWithin(ReuseScope.Hierarchy);

            ApplyConfig(new SharedConfiguration());
            ApplyConfig(new AwsConfiguration());
            ApplyConfig(new LiveServicesConfiguration());
            ApplyConfig(new RequestFilterConfiguration());

            ApplyConfig(new PublisherConfiguration());

            // After all the non-data dependent services are complete....
            SetupProviders();
            SetupAutoQuery();

            ApplyConfig(new ElasticSearchConfiguration(false));
        }

        private void SetupProviders()
        {
            var providersConfiguration = RydrEnvironment.CurrentEnvironment.EqualsOrdinalCi("local")
                                             ? (SharedProvidersConfiguration)new LocalProvidersConfiguration()
                                             : new LiveProvidersConfiguration();

            providersConfiguration.Setup(_container);

            ApplyConfig(providersConfiguration);
        }

        private void SetupAutoQuery()
        { // OrmLite AutoQuery
            var aqFeature = new AutoQueryFeature
                            {
                                MaxLimit = 250,
                                StripUpperInLike = true,
                                AutoQueryServiceBaseType = typeof(BaseApiService),
                                EnableAutoQueryViewer = false,
                                IncludeTotal = true
                            };

            aqFeature.LoadFromAssemblies.Add(typeof(QueryDealRequests).Assembly);

            _appHost.Plugins.Add(aqFeature);

            OrmLiteUtils.IllegalSqlFragmentTokens = OrmLiteUtils.IllegalSqlFragmentTokens
                                                                .Where(f => !(new[]
                                                                              {
                                                                                  "select"
                                                                              }).Contains(f))
                                                                .ToArray();

            OrmLiteUtils.VerifyFragmentRegEx = new Regex("([^\\w]|^)+(--|;--|;|%|/\\*|\\*/|@@|@|char|nchar|varchar|nvarchar|alter|begin|cast|create|cursor|declare|delete|drop|end|exec|execute|fetch|insert|kill|open|sys|sysobjects|syscolumns|table|update)([^\\w]|$)+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

            // Dynamo AutoQuery
            var aqdynFeature = new AutoQueryDataFeature
                               {
                                   MaxLimit = 250,
                                   IncludeTotal = true,
                                   AutoQueryServiceBaseType = typeof(BaseApiService),
                                   EnableAutoQueryViewer = false
                               };

            aqdynFeature.LoadFromAssemblies.Add(typeof(QueryPublishedDeals).Assembly);

            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynAssociation>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynAuthorization>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynDeal>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynDealRequest>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynItemTypeOwnerSpaceReferenceGlobalIndex>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynItemEdgeIdGlobalIndex>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynItemIdTypeReferenceGlobalIndex>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynFile>(_container.Resolve<IPocoDynamo>(), false));
            aqdynFeature.AddDataSource(x => x.DynamoDbSource<DynPublisherAccount>(_container.Resolve<IPocoDynamo>(), false));

            _appHost.Plugins.Add(aqdynFeature);

            _container.Register<IAutoQueryService>(c => new DefaultAutoQueryService(c.LazyResolve<IAutoQueryDb>(),
                                                                                    c.Resolve<IDecorateSqlExpressionService>(),
                                                                                    c.Resolve<IAutoQueryRunner>()))
                      .ReusedWithin(ReuseScope.Hierarchy);
        }

        private void ApplyConfig(IAppHostConfigurer config)
            => config.Apply(_appHost, _container);
    }
}
