using System.Net;
using Funq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Services.Filters;
using Rydr.Api.Services.Helpers;
using ServiceStack;
using ServiceStack.Host;
using ServiceStack.Web;

namespace Rydr.Api.Host;

public static class Program
{
    public static void Main()
    {
        var hostBuilder = new HostBuilder().UseContentRoot(Directory.GetCurrentDirectory())
                                           .ConfigureHostConfiguration(BuildConfiguration)
                                           .ConfigureAppConfiguration((wc, conf) => BuildConfiguration(conf))
                                           .UseEnvironment(RydrBuildEnvironment.EnvName)
                                           .ConfigureLogging(b => b.ClearProviders()
                                                                   .AddNLog()
                                                                   .SetMinimumLevel(LogLevel.Debug))
                                           .ConfigureWebHost(whb => whb.UseShutdownTimeout(TimeSpan.FromSeconds(RydrBuildEnvironment.ShutdownTimeSeconds))
                                                                       .UseUrls(RydrBuildEnvironment.ListenOn)
                                                                       .UseKestrel(o => { o.AllowSynchronousIO = true; })
                                                                       .UseStartup<RydrStartup>());

        var host = hostBuilder.Build();

        host.Run();
    }

    // The configuration produced by this method is used for both the host and app configurations.
    private static void BuildConfiguration(IConfigurationBuilder conf)
        => conf.AddJsonFile("appsettings.json", false, true)
               .AddJsonFile($"appsettings.{RydrBuildEnvironment.Configuration}.json", true, true);
}

public class RydrStartup
{
    private ApiAppHost _appHost;

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services) { }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app)
    {
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var appLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        RydrEnvironment.SetAppSettings(new NetCoreAppSettings(configuration));

        RydrAppHostHelper.Instance.Create();

        _appHost = new ApiAppHost
                   {
                       AppSettings = RydrEnvironment.AppSettings
                   };

        app.UseServiceStack(_appHost);

        appLifetime.ApplicationStopping.Register(OnShutdown);

        RydrAppHostHelper.Instance.OnBeforeRun(_appHost);

        app.Run(context =>
                {
                    context.Response.Redirect("/metadata");

                    return Task.FromResult(0);
                });

        RydrAppHostHelper.Instance.Log.InfoFormat("Listening for requests, HostUrl is [{0}]", RydrUrls.WebHostUri.AbsoluteUri);
    }

    public void OnShutdown() => _appHost.Shutdown();
}

public class ApiAppHost : AppHostBase
{
    private RequestExecutorFactory _requestExecutorFactory;
    private IStats _stats;

    public ApiAppHost() : base("Rydr Self Hosted API", typeof(ApiAppHost).Assembly) { }

    public override void Configure(Container container)
    {
        RydrAppHostHelper.Instance.Init();
        RydrEnvironment.SetContainer(container);

        container.Register(c => new RequestExecutorFactory(c.Resolve<IStats>(),
                                                           c.Resolve<ICounterAndListService>(),
                                                           c.Resolve<IServiceCacheInvalidator>(),
                                                           c.Resolve<IDecorateResponsesService>()))
                 .ReusedWithin(ReuseScope.Hierarchy);

        RydrAppHostHelper.Instance.Configure(this, container);
    }

    public override IServiceRunner<TRequest> CreateServiceRunner<TRequest>(ActionContext actionContext)
        => new RydrServiceRunner<TRequest>(this, actionContext,
                                           (_requestExecutorFactory ??= Container.Resolve<RequestExecutorFactory>()).CreateRequestExecutor<TRequest>(),
                                           (_stats ??= Container.Resolve<IStats>()));

    public void Shutdown() => RydrAppHostHelper.Instance.Shutdown(Container);

    // No cookie for you
    public override bool SetCookieFilter(IRequest req, Cookie cookie) => false;
}

public class RydrServiceRunner<TRequest> : ServiceRunner<TRequest>
{
    private readonly IRequestExecutor<TRequest> _requestExecutor;
    private readonly IStats _stats;

    public RydrServiceRunner(IAppHost appHost, ActionContext actionContext,
                             IRequestExecutor<TRequest> requestExecutor, IStats stats)
        : base(appHost, actionContext)
    {
        _requestExecutor = requestExecutor;
        _stats = stats;
    }

    public override async Task<object> ExecuteAsync(IRequest req, object instance, TRequest requestDto)
    {
        var dtoName = requestDto.GetType().Name;

        var result = await _stats.MeasureAsync(new[]
                                               {
                                                   Stats.StatsKey(dtoName.StartsWith("Query", StringComparison.OrdinalIgnoreCase)
                                                                      ? "QUERY"
                                                                      : req.Verb,
                                                                  StatsKeySuffix.ResponseTime),
                                                   Stats.AllApiResponseTime
                                               },
                                               () => _requestExecutor.ExecuteAsync(req, instance, requestDto, base.ExecuteAsync),
                                               string.Concat("dto:", dtoName));

        return result;
    }
}
