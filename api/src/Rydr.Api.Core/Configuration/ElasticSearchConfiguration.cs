using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using Elasticsearch.Net;
using Funq;
using Nest;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Configuration;

public class ElasticSearchConfiguration : IAppHostConfigurer
{
    private readonly bool _forceDbRecreate;
    private static readonly ILog _log = LogManager.GetLogger("ElasticSearchConfiguration");
    private static readonly int _esRequestTimeout = RydrEnvironment.GetAppSetting("Es.RequestTimeout", 15);
    private static readonly string _esDefaultIndexName = RydrEnvironment.GetAppSetting("Es.DefaultIndexName", "");

    public ElasticSearchConfiguration(bool forceDbRecreate)
    {
        _forceDbRecreate = forceDbRecreate;
    }

    public void Apply(ServiceStackHost appHost, Container container)
    {
        var pool = new SingleNodeConnectionPool(new Uri(RydrEnvironment.GetConnectionString(string.Concat("ConnectionString.Es.", RydrEnvironment.CurrentEnvironment), "http://localhost:9200")));

        var esConnectionSettings = new ConnectionSettings(pool).ConnectionLimit(300)
                                                               .MaximumRetries(4)
                                                               .DisableAutomaticProxyDetection()
                                                               .MaxRetryTimeout(TimeSpan.FromMinutes(3))
                                                               .RequestTimeout(TimeSpan.FromSeconds(_esRequestTimeout));

        if (_esDefaultIndexName.HasValue())
        {
            esConnectionSettings.DefaultIndex(_esDefaultIndexName);
        }

        if (RydrEnvironment.IsDebugEnabled)
        {
            esConnectionSettings.EnableDebugMode();
        }

        container.Register<IElasticClient>(c => new ElasticClient(esConnectionSettings))
                 .ReusedWithin(ReuseScope.Hierarchy);

        ConfigureIndexes(container);
    }

    private void ConfigureIndexes(Container container)
    {
        var coreAssembly = Assembly.Load("Rydr.Api.Core");

        var types = coreAssembly.GetTypes()
                                .Where(t => t.Namespace.EqualsOrdinal("Rydr.Api.Core.Models.Es", false) || t.Namespace.StartsWithOrdinalCi("Rydr.Api.Core.Models.Es."))
                                .Where(t => !t.IsInterface && !t.ContainsGenericParameters && !t.IsAbstract && !t.IsEnum && !t.IsCompilerGenerated());

        var esClient = container.Resolve<IElasticClient>();
        var utcNow = DateTimeHelper.UtcNow;

        if (_forceDbRecreate && !RydrEnvironment.IsReleaseEnvironment)
        {
            esClient.Indices.Delete(Indices.All);
        }

        foreach (var type in types)
        {
            var aliasName = type.Name.TrimPrefixes("Es");

            aliasName = aliasName.StartsWithOrdinalCi("business")
                            ? ElasticIndexes.BusinessesAlias
                            : aliasName.AppendIfNotEndsWith("s").ToLowerInvariant();

            var aliasExists = esClient.Indices.AliasExists(aliasName);

            if (aliasExists?.Exists ?? false)
            {
                if (_forceDbRecreate && !RydrEnvironment.IsReleaseEnvironment)
                {
                    esClient.Indices.DeleteAlias(Indices.All, aliasName);
                }
                else
                {
                    _log.DebugInfoFormat("ElasticSearch Alias [{0}] already exists, continuing", aliasExists);

                    continue;
                }
            }

            // Create the index first
            var indexName = string.Concat(aliasName, "_", utcNow.Year, utcNow.Month.ToString().PadLeft(2, '0'), utcNow.Day.ToString().PadLeft(2, '0'));

            var mi = InjectTypeIntoCreateMethod("CreateEsIndex", type);

            var args = new object[]
                       {
                           indexName, aliasName, esClient
                       };

            mi.Invoke(this, args);
        }
    }

    // ReSharper disable once UnusedMember.Local
    private void CreateEsIndex<T>(string indexName, string aliasName, IElasticClient esClient)
        where T : class
    {
        Guard.AgainstNullArgument(!indexName.HasValue(), nameof(indexName));
        Guard.AgainstNullArgument(!aliasName.HasValue(), nameof(aliasName));
        Guard.AgainstInvalidData(indexName.EqualsOrdinalCi(aliasName), "Index and alias names should not match");

        var refreshSeconds = indexName.StartsWithOrdinalCi("creators_")
                                 ? 31
                                 : 9;

        var replicas = RydrEnvironment.IsReleaseEnvironment
                           ? 1
                           : 0;

        _log.DebugInfoFormat("Creating elastic index [{0}]", indexName);

        Exception lastEx = null;
        var attempts = 0;

        do
        {
            try
            {
                attempts++;

                var indexCreateResponse = esClient.Indices.Create(indexName,
                                                                  cid => cid.Map<T>(d => d.AutoMap()
                                                                                          .Dynamic(false))
                                                                            .IncludeTypeName(false)
                                                                            .Settings(s => s.NumberOfShards(5)
                                                                                            .NumberOfReplicas(replicas)
                                                                                            .RefreshInterval(new Time(TimeSpan.FromSeconds(refreshSeconds)))
                                                                                            .UnassignedNodeLeftDelayedTimeout(new Time(TimeSpan.FromMinutes(13)))
                                                                                            .Analysis(ad => ad.Analyzers(az => az.Language("default", l => l.Language(Language.English)))
                                                                                                              .Normalizers(nd => nd.Custom("rydrkeyword", kw => kw.Filters("lowercase", "asciifolding")))))
                                                                            .Aliases(ad => ad.Alias(aliasName)));

                if (!indexCreateResponse.SuccessfulOnly())
                {
                    throw indexCreateResponse.ToException();
                }

                // All done...
                return;
            }
            catch(HttpRequestException hx)
            {
                lastEx = hx;
            }
            catch(SocketException sx)
            {
                lastEx = sx;
            }
            catch(WebSocketException wsx)
            {
                lastEx = wsx;
            }

            Thread.Sleep(attempts * 375);
        } while (attempts <= 5);

        throw (lastEx ?? new InvalidOperationException($"Cannot create Elasticsearch index [{indexName}], see previous log exceptions for informationl."));
    }

    private static MethodInfo InjectTypeIntoCreateMethod(string methodName, Type concreteType)
    {
        var method = typeof(ElasticSearchConfiguration).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        var genericTypes = new List<Type>
                           {
                               concreteType
                           };

        return method.MakeGenericMethod(genericTypes.ToArray());
    }
}
