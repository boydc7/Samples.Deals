using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using Monitor = Rydr.Api.Dto.Monitor;

namespace Rydr.Api.Services.Services;

[Restrict(VisibleLocalhostOnly = true)]
public class MonitorService : BaseApiService
{
    private readonly IDeferRequestsService _deferRequestsService;
    private static bool _doNotRespond;

    public MonitorService(IDeferRequestsService deferRequestsService)
    {
        _deferRequestsService = deferRequestsService;
    }

    public MonitorResponse Get(Monitor request)
    {
        if (InShutdown)
        {
            throw new ApplicationInShutdownException();
        }

        if (_doNotRespond && !request.EnableRespond)
        {
            throw new MonitorResponseDisabledException();
        }

        _doNotRespond = request.DisableRespond && !request.EnableRespond;

        var response = new MonitorResponse
                       {
                           Status = request.Echo.Coalesce("OK")
                       };

        if (LocalResourceManager.Instance.ShouldCheckResources())
        {
            LocalAsyncTaskExecuter.DefaultTaskExecuter.Exec(LocalResourceManager.Instance, cm => cm.CheckAllResources(LocalCache), maxAttempts: 1);

            _deferRequestsService.DeferLowPriRequest(new MonitorSystemResources().WithAdminRequestInfo());
        }

        return response;
    }

    public MonitorResponse Any(Monitor request) => Get(request);

    public SimpleResponse Any(NoRoute request) => throw new NoRouteException(request.Path.Left(50));
}
