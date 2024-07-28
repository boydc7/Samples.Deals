using Amazon.Runtime;
using FirebaseAdmin;
using Funq;
using Google.Apis.Auth.OAuth2;
using Hangfire;
using NLog.AWS.Logger;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.DataAccess.Repositories;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Dto.Users;
using Rydr.Api.Services.Filters;
using Rydr.Api.Services.Services;
using Rydr.Api.Services.Validators;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Logging;
using ServiceStack.Logging.NLogger;
using ServiceStack.Messaging;
using ServiceStack.OrmLite.Dapper;
using ServiceStack.Validation;
using LogManager = NLog.LogManager;

namespace Rydr.Api.Services.Helpers;

public class RydrAppHostHelper
{
    private IMessageService _mqHost;
    private int _inShutdown;
    private int _inStartup = 1;

    private RydrAppHostHelper()
    {
        Licensing.RegisterLicense(@"7376-e1JlZjo3Mzc2LE5hbWU6IlJ5ZHIgVGVjaG5vbG9naWVzLCBJbmMiLFR5cGU6SW5kaWUsTWV0YTowLEhhc2g6RnRLMzhRWTJTMXU1OWdHVUJ5TGM1ZlN2dHREY3hCWVBvQ3Y2NDZmWWtFVlJZKzhycUYwUno1MzNmUE1LRmlYdUg3MmJWZmFuenQ3Sml0eG5XQVJFeEtMa3VCNUpkdXhxcGd5Ykw5cGhVeVluRmdJNFhQYmFOejY4dVNGZGdFa25YcHF1OG50Z3dianErcmVSUVQyb2FTSGJWNGhpYlVjZW9sWittdnZ4bHNNPSxFeHBpcnk6MjAyMC0wNi0xMn0=");
    }

    public static RydrAppHostHelper Instance { get; } = new();

    public ILog Log { get; private set; }

    public bool InStartup => _inStartup > 0;

    public string ClientUrlRoot => RydrUrls.ClientRootUri?.AbsoluteUri;

    public string WebHostCustomPath => RydrUrls.WebHostCustomPath;

    public void Create() { }

    public void OnBeforeRun(ServiceStackHost host)
    {
        // Resolve the Hangfire server to start it...
        host.Container.Resolve<BackgroundJobServer>();

        _mqHost.Start();
        _mqHost = null;

        // Ensure system-wide ongoing jobs are configured
        if (RydrEnvironment.IsReleaseEnvironment)
        {
            var deferRequestsService = host.Container.Resolve<IDeferRequestsService>();

            if (HumanLoopService.HumanBusinessCategoryFlowArn.HasValue())
            {
                deferRequestsService.PublishMessageRecurring(new PostDeferredLowPriMessage
                                                             {
                                                                 Dto = new PostProcessHumanLoop
                                                                     {
                                                                         LoopIdentifier = HumanLoopService.HumanBusinessCategoryFlowArn,
                                                                         HoursBack = 50
                                                                     }.WithAdminRequestInfo()
                                                                      .ToJsv(),
                                                                 Type = typeof(PostProcessHumanLoop).FullName
                                                             }.WithAdminRequestInfo(),
                                                             CronBuilder.Minutely(18),
                                                             PostProcessHumanLoop.GetRecurringJobId(HumanLoopService.PublisherAccountBusinessCategoryPrefix));
            }

            if (HumanLoopService.HumanCreatorCategoryFlowArn.HasValue())
            {
                deferRequestsService.PublishMessageRecurring(new PostDeferredLowPriMessage
                                                             {
                                                                 Dto = new PostProcessHumanLoop
                                                                     {
                                                                         LoopIdentifier = HumanLoopService.HumanCreatorCategoryFlowArn,
                                                                         HoursBack = 50
                                                                     }.WithAdminRequestInfo()
                                                                      .ToJsv(),
                                                                 Type = typeof(PostProcessHumanLoop).FullName
                                                             }.WithAdminRequestInfo(),
                                                             CronBuilder.Minutely(17),
                                                             PostProcessHumanLoop.GetRecurringJobId(HumanLoopService.PublisherAccountCreatorCategoryPrefix));
            }
        }

        Interlocked.Exchange(ref _inStartup, 0);
    }

    public void Configure(ServiceStackHost host, Container container)
    { // Prod vs. dev firebase creds
        var firebaseCredentials = RydrEnvironment.IsReleaseEnvironment || RydrEnvironment.CurrentEnvironment.EqualsOrdinalCi("Production")
                                      ? GoogleCredential.FromJson(@"
{
    ""type"": ""service_account"",
    ""project_id"": ""rydr-flutter"",
    ""private_key_id"": ""5a64c2fd37971b6d331857ea62545027da8a3a00"",
    ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDIeJ087h2nNTif\nfysbpKCtCxjeXMNgwDRMek1kicPO2SfYjp80i7/Oq8nx96imv+UbVnI2DPZrnDj8\npSw+94NtoKKh//AQC5cPE/VR+96H/y1D2TWG8qSIjncFJQ06WzwZtZwmBvVbwxDb\n9prgagfaWxt/GHMktTjaQLeVvy/NwGYjM0K+kik1KP+9sigJ61QN97RdRtihBhl1\n2qBma2Bx5SUMfc7zogJYT581Eku0Gq4WK3bG0Isg+aRctgPz8ePOspAKU+/brUrt\n4WvQoj4L25IGuwru9kuVk+O9kubWVHPRMaHh/gt43Tkw+S83+0uZCVJTWrMRrZFH\nqy3Cu1WfAgMBAAECggEACWs6qVy0saD7WT8T1Zd9YclQ2023c58Zi0hUb4yrfFdR\nRchPAZyD9WAL5nL7jmNwuRaYL3hExwHkNDacfBQ2rXAWhA8EMUoiHaK97CKt2MJB\nVwaTEGqWbBYUJdPmVF9p/6PmmMDLRrWaYANXpoNE0tpkpyriag9GWGF3CVxIu4gh\nKygcQoCASTlDqfK0f3vVBsvtW29TzathRTg+hgQrrT29LU2yRVkB6HvG0ZmxKryO\nHzfVP4n2cu7pZFQ+dQt6ima9YCCebIrkJN/htfeYLEIt3we9TAlsd7NfRUGhmPYJ\nmcQAnkEnC8Ds6U/EL3cEpLYKSRfZD3PFkvmF9HiJ8QKBgQDsWkz3Zdi5ZJ3Nf5yy\nSrYlx2vkMFVpEySPKENyOfFcRmKs6J0TRa9DZBneZXGiGHTa5idRjRtPIwnKry2G\n88/eOjfM3KIHThFy/pP1Ug2bB44QtXR44A6iqJU5aLpKSKrHWBdmgdsnq8/qts0U\n1Qw+1IvuS+ZdBDjCIaSE6r40EQKBgQDZIrx34ta4LsAVDon7+T1CHw8MvX5IIe/f\neJmgac2xoj1cDKMXy1Qmb32E1hHq7wu5WZ4KCR+Hb4trKmWvO/iKwGU4XFKgXGRK\ndwL6DCCzfZn/Fwb/RDv9lDqnHrBiXKsqQ/Q4HJrHkbFeTzdMpxdV8+RGp+4YSGNq\nwEh005/erwKBgQC1SkpxFWjYQ4obH0A1LcNrVPy36i8JSsqnGC4rxrAQpFh54m7h\nYnkdywFgqhUwTWwMn68XCZIh8HFJS3czZX5TKfq1I6MQ0VvnBci9yjNvb6sTu+tb\n8BipwX+8qk0CP5znDPXeBcrxMgNoONEzonsjEmtG3GcVf/B9T8revSQp0QKBgQC3\nfYky7nhxEOC1aqHkUw0XUVPQelm67yLb//gi/QYb9HRR00QHmYW1LUYu+RAPLo8D\nxN2usWL5eqOgniVr3gv8hPWEmVAhv7Ho04WqdJE13RBD5tu835aqhZbDH0YC+TiT\n8PTybgnGWDJA9kRO/GzV79KaetLTpmiND4yrXSKedQKBgGSF8GvWgfifV3OuGfKN\nBpzJ3kjIgqfRHF6opJMladcrnz2/mPp4DdfqVHEPjM8LaqX8UJVLt61Ju6fZsv8N\nHBL72Mp+8E85vVrq6dl/X9Mshp8C4NpKUNppHGnhKeoW8lj3nwlaibgEDMYT/6mS\noZz+2OuaiHKrHVIqGorUVnvp\n-----END PRIVATE KEY-----\n"",
    ""client_email"": ""firebase-adminsdk-wpurb@rydr-flutter.iam.gserviceaccount.com"",
    ""client_id"": ""116796166679425869711"",
    ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
    ""token_uri"": ""https://oauth2.googleapis.com/token"",
    ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
    ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/firebase-adminsdk-wpurb%40rydr-flutter.iam.gserviceaccount.com""
}
")
                                      : GoogleCredential.FromJson(@"
{
  ""type"": ""service_account"",
  ""project_id"": ""rydr-dev"",
  ""private_key_id"": ""280b3c985e515f50dccce0682f53f5eb2f075316"",
  ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQCz27FyOoWLMpKz\nK3zRx0aNQKAZvQoFxq10hwvvPZGtEynl5aEJOJo9PuaXkvRBikgjfMC6/u5La0hU\nyYcxjLmGATVdl+3Kw6+SRnFxwVdOJmk8MjoCUbqTfykbuCmgLiry5J847LtSIy9y\n+/x9+ZTAGqFv/Ir1VY7X5V7CIG+mUnTOj9E7c72WgqideQBwQjc377TvuJGEXkMK\nGL1lG3VgQxKjSbYXYSaAcdmCPBqQgF7xjL13h5PToO5P+TUu599daqeDve/cufiM\nQH8JYD6ZV8QNZvHKN34Sjmr3UgIZREl/f4IWU2NcUHyB8NwM9ym1dIQNACeFpteP\nXwKHHU9RAgMBAAECggEAML7gLtdRjlJclBa7M5fQtUPIoHEtoDcil6xqPaLwMno5\nJse/h2JB20uK75WygXja6FNNYODq8KHY7rHX5EQBnCIDtqQQnJ3AneJdqLj/0nxy\nlQ//zNUdvg/+sjaNgY5BsaboyGLQuggzOfS1j+buu8n76wAFIUzY9AaEUhS8bdUB\nmviiA0zng7vKfNkrVMI92Su79N3quTyuBQNNJ6RMXPH+HAyziUaM8cDjd7ZZuTa5\nJd4jCqpGoXKNdw5QsSjGYrxwQjLYzx3yNlZF/sYJu0XHgIzM3DpWIXiN6oWnbHJf\nPbkcPHjeL287MFl8pDJwU7WHOSA7paqO9aDXWKz6yQKBgQD5H589s/K6OzLMjPdL\nOWBngSzR21lc0S6ZQpBqsYLrMUsnuL6Uxx7pnCxLRsUuixbmlq4JllyFQanBf8mt\nEgTJ6XjVntlv1nUhvhqzYGayZO7hLVvnCStaf4etcp9MP9z4kV/6CRO29Gas2eIt\nua4ijdJRVui3FuwmvOD1v1WTnwKBgQC40p9U5Gymh3Q0m4IyAFmozs7N+Hh2hVyz\nENEiUZFkFkKAx5+/E4Wg/6EsrYetgTYgZkuQU3ixwXtbJD6GTe1SzQD3wwGvllQK\nMnuEnmU0NczmW7q2rxJjkeRq+aZQ7e4qrWA3diENHIOWLy6Tm8t8J93EYuik5YQ6\n7EPsao23DwKBgQCXmSelO8EElQunsEy4aRUCR3hHyEyMD/tkZj5NvvHlP5z1chX/\noWBtVo0ZzdomJZvs/FqyGN76dGfiCWpnuGRTnpDapgy5Yu7qdq325D36ZzN6sciQ\nQmMwchTVdr/7fY1xcb3PAQEPP5DPtNNPcgPGoTkQKGv7JqbUN/JJeYKRDwKBgQC3\nitsWGB5aJmxdjg12kGh5vp8bZuRid0A+x7WYij6DkaOLdjMLM1ziLNqnntD9mjLh\nbBUgh/R1OnrBYTYCdEL5loKeifcTo4tj8Qw/AHnqpn8MSQ4cO7JcVVbscW4cMpzx\nnunNSi+6cJWwwLxVdENY0dJnI/57Oz7csSMnFg4UOwKBgDGd9uN28hBYmq3AxVHA\nlyaTKiyZ4plE9vgFCswaOr1WgHkHmAOrFO8uZuosOkha0hpmMhhHN/xn2jfHp12S\nneNWMt6zmNQU5u9ylkaYPXRQt5LybS3rRnYnp646TPOQ9j4ZPAxRD2F84LhXgVzW\nNreC7WVjeN3MH4IWhENdAK/8\n-----END PRIVATE KEY-----\n"",
  ""client_email"": ""firebase-adminsdk-nq86q@rydr-dev.iam.gserviceaccount.com"",
  ""client_id"": ""113250311600040429944"",
  ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
  ""token_uri"": ""https://oauth2.googleapis.com/token"",
  ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
  ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/firebase-adminsdk-nq86q%40rydr-dev.iam.gserviceaccount.com""
}
");

        // rydr-flutter firebase project (LIVE app)
        FirebaseApp.Create(new AppOptions
                           {
                               Credential = firebaseCredentials
                           });

        var apiConfig = new RydrApiConfiguration(host, container);

        apiConfig.ApplyConfigs();

        if (RydrEnvironment.GetAppSetting("Messaging.DisableAll", false))
        {
            var tmc = new TransientMessagingConfiguration();

            tmc.Apply(host, host.Container);

            _mqHost = tmc.MqHost;

            Log.Info("Message handling DISABLED on this service due to Messaging.DisableAll configuration setting being on.");
        }
        else
        {
            var lmc = new LiveMessagingConfiguration();

            lmc.Apply(host, host.Container);

            _mqHost = lmc.MqHost;
        }

        // Auth repos and schema
        var userRepository = new RydrDynamoFirebaseAuthUserRepository(container.Resolve<IPocoDynamo>(),
                                                                      container.Resolve<IUserService>(),
                                                                      container.Resolve<IClientTokenAuthorizationService>());

        container.Register<IAuthRepository>(userRepository);
        container.Register<IUserAuthRepository>(userRepository);
        container.Register<IRydrUserAuthRepository>(userRepository);

        // Web Services and validators...
        host.RegisterServicesInAssembly(typeof(MonitorService).Assembly);
        host.Container.RegisterValidators(typeof(DateTimeAttributeValidator).Assembly);

        if (RydrEnvironment.IsDebugEnabled)
        {
            var hideExcludeTypes = RequestHelpers.AllowedAnonRequestTypes
                                                 .Concat(RequestHelpers.RequestsThatSkipLogging)
                                                 .Distinct()
                                                 .ToArray();

            host.Plugins.Add(new RequestLogsFeature
                             {
                                 EnableErrorTracking = true,
                                 EnableSessionTracking = false,
                                 EnableResponseTracking = RydrEnvironment.IsLocalEnvironment,
                                 EnableRequestBodyTracking = true,
                                 Capacity = 500,
                                 RequestLogFilter = (r, e) =>
                                                    {
                                                        e.Items = null;
                                                        e.SessionId = null;

                                                        if (e.Headers.IsNullOrEmptyRydr())
                                                        {
                                                            return;
                                                        }

                                                        var keysToRemove = e.Headers
                                                                            .Where(h => h.Key.EqualsOrdinalCi("Authorization") ||
                                                                                        h.Key.StartsWithOrdinalCi("X-rydr") ||
                                                                                        h.Key.StartsWithOrdinalCi("X-ss"))
                                                                            .Select(h => h.Key)
                                                                            .AsList();

                                                        keysToRemove.Each(k => e.Headers.Remove(k));
                                                    },
                                 ExcludeRequestDtoTypes = hideExcludeTypes,
                                 HideRequestBodyForRequestDtoTypes = hideExcludeTypes,
                                 RequiredRoles = new[]
                                                 {
                                                     "Admin"
                                                 }
                             });
        }

        // Ensure our initial admin user is setup
#if LOCALDEBUG

        // ReSharper disable once ConvertToConstant.Local
        var apiKey = "coNvop4sZWzUISupyNDRNiq274Xc_nXxLCipYEA3ThK00e5E9HbTK4kTjgc0K5ABxuQ";
#else
            var secretService = container.Resolve<ISecretService>();

            // ReSharper disable once ConvertToConstant.Local
            var apiKey = secretService.GetSecretStringAsync($"RydrApi.AdminPassword.{RydrEnvironment.CurrentEnvironment}").GetAwaiter().GetResult();
#endif

        RydrEnvironment.SetAdminKey(apiKey);
        host.Config.AdminAuthSecret = apiKey;

        var existingAdminUser = userRepository.GetDynUserByUserNameAsync("rydr.admin.dummyacct@getrydr.com").GetAwaiter().GetResult();

        if (existingAdminUser != null)
        {
            return;
        }

        var dynUser = userRepository.CreateUserAuthAsync(new DynUser
                                                         {
                                                             FirstName = "Rydr",
                                                             LastName = "Admin",
                                                             FullName = "Rydr Admin",
                                                             Email = "rydr.admin.dummyacct@getrydr.com",
                                                             UserName = "rydr.admin.dummyacct@getrydr.com",
                                                             UserType = UserType.Admin
                                                         }).GetAwaiter().GetResult();

        userRepository.StoreAll(new ApiKey
                                {
                                    Id = apiKey,
                                    UserAuthId = dynUser.UserId.ToStringInvariant()
                                }.AsEnumerable());
    }

    public int ShutdownWaitTimeout => RydrEnvironment.GetAppSetting("Environment.ShutdownWaitSeconds", 600);

    public void Shutdown(Container container, Action stopCallback = null)
    {
        if (Interlocked.Exchange(ref _inShutdown, 1) > 0)
        {
            return;
        }

        BaseApiService.InShutdown = true;
        LocalAsyncTaskExecuter.DefaultTaskExecuter.InShutdown = true;

        // Stop the MQ host early so it does not keep trying to process requests while waiting for shutdown
        ShutdownMqHost(container);

        // Stop the Hangfire JobServer...
        container.Resolve<BackgroundJobServer>().Dispose();

        var counterService = container.TryResolve<ICounterAndListService>();

        if (counterService != null)
        {
            var waitForSeconds = ShutdownWaitTimeout;
            var startedWaitAt = DateTimeHelper.UtcNowTs;

            Log.InfoFormat("Shutdown requested, waiting for outstanding requests to complete for up to [{0}] seconds", waitForSeconds);

            do
            {
                // Wait for existing things to finish up if feasible
                var currentApiThreadCount = counterService.GetCounter(Stats.AllApiOpenRequests);

                if (currentApiThreadCount <= 0 && LocalAsyncTaskExecuter.DefaultTaskExecuter.Count <= 0)
                {
                    break;
                }

                Thread.Sleep(750);
            } while (DateTimeHelper.UtcNowTs - startedWaitAt <= waitForSeconds);
        }

        ShutdownAppHost(stopCallback);
    }

    private void ShutdownAppHost(Action stopCallback)
    {
        try
        {
            Log.Info("Shutdown of AppHost starting");

            stopCallback?.Invoke();

            Log.Info("Shutdown of AppHost complete");
        }
        catch(Exception ex)
        {
            Log.Exception(ex, "Exception trying to Shutdown the AppHost.");
        }
        finally
        {
            Try.Exec(() =>
                     {
                         LogManager.Flush();
                         LogManager.Shutdown();
                     });
        }
    }

    private void ShutdownMqHost(Container container) => MqHostHelper.Instance.ShutdownHosts(container.TryResolve<IMessageService>(),
                                                                                            container.TryResolve<IServerEvents>());

    public void Init(Action initCallback = null)
    {
        var useAwsLogs = !RydrEnvironment.IsLocalEnvironment && RydrEnvironment.GetAppSetting("Logging.UseAwsTarget", false);

        // Configure NLog to either throw exceptions (if local), or add the AWS log target in non-debug...
        if (RydrEnvironment.IsLocalEnvironment)
        {
            LogManager.ThrowExceptions = true;
        }

        if (RydrEnvironment.GetAppSetting("Environment.Containerized", false))
        {
            // Disable all the loggers except console
            var loggersToDisable = LogManager.Configuration.LoggingRules
                                             .Where(r => r.Targets != null &&
                                                         r.Targets.Count > 0 &&
                                                         !r.Targets.Any(t => t.Name.Contains("console", StringComparison.OrdinalIgnoreCase)))
                                             .AsList();

            Guard.Against(!loggersToDisable.All(l => LogManager.Configuration.LoggingRules.Remove(l)), "Could not remove logger rules for containerized operation");
        }

        if (useAwsLogs)
        { // Add the AWS log target
            var awsTarget = new AWSTarget
                            {
                                Credentials = new BasicAWSCredentials(RydrEnvironment.GetAppSetting("AWSAccessKey"),
                                                                      RydrEnvironment.GetAppSetting("AWSSecretKey")),
                                LogGroup = "Rydr.Api",
                                Region = "us-west-2",
                                Name = "aws",
                                LibraryLogFileName = "./logs/awsLoggerErrorLog.txt",
                                LogStreamNameSuffix = string.Concat(RydrEnvironment.CurrentEnvironment, " - ", Environment.MachineName)
                            };

            LogManager.Configuration.AddTarget(awsTarget);

            var ruleToAddAwsTo = LogManager.Configuration.LoggingRules.Single(r => r.LoggerNamePattern.EqualsOrdinalCi("*") &&
                                                                                   r.Targets.Count(t => t.Name.EqualsOrdinalCi("console")) == 1);

            ruleToAddAwsTo.Targets.Add(awsTarget);
        }

        LogManager.Configuration.Reload();

        ServiceStack.Logging.LogManager.LogFactory = new ImplicitContextLogFactory(new NLogFactory());

        Log = ServiceStack.Logging.LogManager.GetLogger(GetType());

        try
        {
            Log.Info("AppHost Starting");

            initCallback?.Invoke();
        }
        catch(Exception ex)
        {
            if (_inShutdown > 0)
            {
                return;
            }

            Log.Exception(ex, "Exception in AppHost Init");

            throw;
        }
    }
}
