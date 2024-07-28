using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Amazon.Rekognition.Model;
using Funq;
using Rydr.Api.Core.DataAccess.Config;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Services.Publishers;
using Rydr.Api.Dto.Publishers;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Redis;
using ServiceStack.Text;
using ServiceStack.Validation;

// ReSharper disable UnusedMember.Local

namespace Rydr.Api.Core.Configuration;

public class SharedConfiguration : IAppHostConfigurer
{
    private static readonly ILog _log = LogManager.GetLogger("SharedConfiguration");

    public void Apply(ServiceStackHost appHost, Container container)
    {
        // NOTE: Only put things in here that should be shared across ALL API components (i.e. production apps, test runs, apis, etc.)
        _log.InfoFormat("Running in Environment.Configuration of [{0}].", RydrBuildEnvironment.Configuration);

        var hostConfig = new HostConfig
                         {
                             DebugMode = false,
                             DefaultContentType = MimeTypes.Json,
                             UseSecureCookies = true,
                             AllowNonHttpOnlyCookies = false,
                             AllowSessionIdsInHttpParams = false,
                             AllowSessionCookies = false,
                             EnableAccessRestrictions = true,
                             EnableFeatures = Feature.All.Remove(Feature.Razor | Feature.Soap | Feature.Soap11 | Feature.Soap12 | Feature.Csv | Feature.Html),
                             EnableOptimizations = true,
                             UseCamelCase = true,
                             AddRedirectParamsToQueryString = true,
                             ReturnsInnerException = false,
                             WebHostUrl = RydrUrls.WebHostUri.AbsoluteUri,
                         };

        // Debug stuff
        if (RydrEnvironment.IsDebugEnabled)
        {
            _log.Info("DEBUG IS ENABLED.");
            hostConfig.DebugMode = true;

            hostConfig.UseSecureCookies = false;
            hostConfig.AllowNonHttpOnlyCookies = true;
        }

        appHost.SetConfig(hostConfig);

        appHost.Plugins.Add(new ValidationFeature());

        appHost.Plugins.Add(new PostmanFeature
                            {
                                DefaultLabelFmt = new List<string>
                                                  {
                                                      "type:english",
                                                      " ",
                                                      "route"
                                                  }
                            });

        // Set default cache control for older ToOptimzied* features
        var cacheFeature = appHost.GetPlugin<HttpCacheFeature>();
        cacheFeature.CacheControlForOptimizedResults = "max-age=60, private";
        cacheFeature.DefaultMaxAge = TimeSpan.FromSeconds(60);

        // Common services where concrete implementation matches interface definition (minus the I)
        RegisterAssemblyByConvention(container, "Rydr.Api.Core", "Service");

        ConfigureJsSerializer();

        // Authentication/User info
        new AuthenticationConfiguration().Apply(appHost, container);
        new AuthorizationConfiguration().Apply(appHost, container);

        // Data
        new DataAccessConfiguration().Apply(appHost, container);

        // Others
        new DeferredProcessingConfiguration().Apply(appHost, container);
        new HangfireConfiguration().Apply(appHost, container);

        // Shared services
        container.RegisterAutoWiredAs<DynAssociationService, IAssociationService>();
        container.RegisterAutoWiredAs<TimestampedMapItemService, IMapItemService>();
        container.RegisterAutoWiredAs<AwsVideoConversionService, IVideoConversionService>();

        container.Register<IAutoQueryRunner>(c => new BasicAutoQueryRunner(c.LazyResolve<IAutoQueryData>(),
                                                                           c.Resolve<IDecorateDynamoExpressionService>()))
                 .ReusedWithin(ReuseScope.Hierarchy);
    }

    private void ConfigureJsSerializer()
    {
        JsConfig.TextCase = TextCase.CamelCase;
        JsConfig.DateHandler = DateHandler.ISO8601;
        JsConfig.AssumeUtc = true;

        JsConfig<PublisherAccount>.ExcludeTypeInfo = true;
        JsConfig<PublisherAccountInfo>.ExcludeTypeInfo = true;
        JsConfig<ModerationLabel>.ExcludeTypeInfo = true;

        JsConfig<bool>.DeSerializeFn = x => x.ToBoolean();

        JsConfig<CompressedString>.SerializeFn = cs => cs?.ToString();

        JsConfig<CompressedString>.DeSerializeFn = s => s.IsNullOrEmpty()
                                                            ? null
                                                            : new CompressedString(s);

        JsConfig<DateTime>.SerializeFn = dt =>
                                         {
                                             if (dt.Kind != DateTimeKind.Utc && dt.Kind != DateTimeKind.Unspecified)
                                             { // We assume UTC in unspecified cases...
                                                 _log.ErrorFormat("Non-UTC DateTime attempted to be serialized, kind is [{0}]", dt.Kind.ToString());

                                                 //throw new SerializationException($"Non-UTC DateTime attempted to be serialized, kind is [{dt.Kind.ToString()}].");
                                             }

                                             return dt.ToIso8601Utc();
                                         };

        JsConfig<DateTime>.DeSerializeFn = dts =>
                                           {
                                               if (!dts.HasValue())
                                               {
                                                   return DateTimeHelper.MinApplicationDate;
                                               }

                                               if (dts.Length == 10)
                                               {
                                                   if (Regex.IsMatch(dts, "^(-?(?:[1-9][0-9]*)?[0-9]{4})-(1[0-2]|0[1-9])-(3[01]|0[1-9]|[12][0-9])$", RegexOptions.Compiled))
                                                   {
                                                       dts = string.Concat(dts, "T00:00:00Z");
                                                   }
                                                   else
                                                   {
                                                       throw new SerializationException($"Non-ISO8601 formatted string passed for DateTime deserialization, passed [{dts}].");
                                                   }
                                               }
                                               else if (!Regex.IsMatch(dts, @"^(-?(?:[1-9][0-9]*)?[0-9]{4})-(1[0-2]|0[1-9])-(3[01]|0[1-9]|[12][0-9])T(2[0-3]|[01][0-9]):([0-5][0-9]):([0-5][0-9])(\.[0-9]+)?(Z)?$", RegexOptions.Compiled))
                                               {
                                                   throw new SerializationException($"Non-ISO8601 formatted string passed for DateTime deserialization, passed [{dts}].");
                                               }

                                               return DateTime.Parse(dts, CultureInfo.InvariantCulture).ToStableUniversalTime();
                                           };

        JsConfig<DateTime?>.SerializeFn = dtn =>
                                          {
                                              if (!dtn.HasValue)
                                              {
                                                  return null;
                                              }

                                              if (dtn.Value.Kind != DateTimeKind.Utc && dtn.Value.Kind != DateTimeKind.Unspecified)
                                              { // We assume UTC in unspecified cases...
                                                  _log.ErrorFormat("Non-UTC DateTime? attempted to be serialized, kind is [{0}]", dtn.Value.Kind.ToString());

                                                  //throw new SerializationException($"Non-UTC DateTime? attempted to be serialized, kind is [{dtn.Value.Kind.ToString()}].");
                                              }

                                              return dtn.Value.ToIso8601Utc();
                                          };

        JsConfig<DateTime?>.DeSerializeFn = dts =>
                                            {
                                                if (!dts.HasValue())
                                                {
                                                    return null;
                                                }

                                                if (dts.Length == 10)
                                                {
                                                    if (Regex.IsMatch(dts, "^(-?(?:[1-9][0-9]*)?[0-9]{4})-(1[0-2]|0[1-9])-(3[01]|0[1-9]|[12][0-9])$", RegexOptions.Compiled))
                                                    {
                                                        dts = string.Concat(dts, "T00:00:00Z");
                                                    }
                                                    else
                                                    {
                                                        throw new SerializationException($"Non-ISO8601 formatted string passed for DateTime deserialization, passed [{dts}].");
                                                    }
                                                }
                                                else if (!Regex.IsMatch(dts, @"^(-?(?:[1-9][0-9]*)?[0-9]{4})-(1[0-2]|0[1-9])-(3[01]|0[1-9]|[12][0-9])T(2[0-3]|[01][0-9]):([0-5][0-9]):([0-5][0-9])(\.[0-9]+)?(Z)?$", RegexOptions.Compiled))
                                                {
                                                    throw new SerializationException($"Non-ISO8601 formatted string passed for DateTime? deserialization, passed [{dts}].");
                                                }

                                                return DateTime.Parse(dts, CultureInfo.InvariantCulture).ToStableUniversalTime();
                                            };
    }

    private void RegisterAssemblyByConvention(Container container, string assemblyName, string typeSuffix, ReuseScope reuseScope = ReuseScope.Hierarchy)
    {
        var assembly = Assembly.Load(assemblyName);

        var implementationTypes = assembly.GetTypes()
                                          .Where(t => !t.IsInterface &&
                                                      t.Name.EndsWithOrdinalCi(typeSuffix));

        foreach (var implementedType in implementationTypes)
        {
            var interfaceName = $"I{implementedType.Name}";

            var interfaceType = implementedType.GetInterface(interfaceName);

            if (interfaceType == null)
            {
                continue;
            }

            var mi = InjectTypeAndInterfaceIntoMethod("RegisterAutoWiredTypeAsInterface", implementedType, interfaceType);

            var args = new object[]
                       {
                           container, reuseScope
                       };

            mi.Invoke(this, args);
        }
    }

    private void RegisterTypesAsMatchingInterfaces(string assemblyName, string assemblyNamespace,
                                                   Container container, string suffixToMatch = null,
                                                   ReuseScope reuseScope = ReuseScope.None)
    {
        if (string.IsNullOrEmpty(suffixToMatch))
        {
            suffixToMatch = string.Empty;
        }

        var coreAssembly = Assembly.Load(assemblyName);

        var types = coreAssembly.GetTypesInNamespace(assemblyNamespace)
                                .Where(t => t.Name.EndsWith(suffixToMatch) && !t.IsInterface);

        foreach (var type in types)
        {
            var interfaceName = $"I{type.Name}";
            var interfaceType = type.GetInterface(interfaceName);

            if (interfaceType == null)
            {
                continue;
            }

            var mi = InjectTypeAndInterfaceIntoMethod("RegisterAutoWiredTypeAsInterface", type, interfaceType);

            var args = new object[]
                       {
                           container, reuseScope
                       };

            mi.Invoke(this, args);
        }
    }

    // ReSharper disable once UnusedMember.Local
    private void RegisterAutoWiredTypeAsInterface<T, TInterface>(Container container, ReuseScope reuseScope = ReuseScope.None)
        where T : TInterface
        => container.RegisterAutoWiredAs<T, TInterface>().ReusedWithin(reuseScope);

    private static MethodInfo InjectTypeAndInterfaceIntoMethod(string methodName, Type concreteType, Type interfaceType = null)
    {
        var method = typeof(SharedConfiguration).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        var genericTypes = new List<Type>
                           {
                               concreteType
                           };

        if (interfaceType != null)
        {
            genericTypes.Add(interfaceType);
        }

        return method.MakeGenericMethod(genericTypes.ToArray());
    }

    public static void RegisterNamedRedisProvider(string appName, Container container)
    {
        var redisConfig = new RedisConfiguration(appName);

        var poolSize = RydrEnvironment.GetAppSetting("Environment.Redis.PoolSize", "300").ToInteger(300);

        container.Register<IRedisClientsManager>(appName, c => new RedisManagerPool(redisConfig.ConnectionString(),
                                                                                    new RedisPoolConfig
                                                                                    {
                                                                                        MaxPoolSize = poolSize
                                                                                    }));
    }
}
