using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;

namespace Rydr.Api.Services.Services
{
    public class WorkspaceSubscriptionService : BaseAuthenticatedApiService
    {
        private readonly IWorkspaceSubscriptionService _workspaceSubscriptionService;

        public WorkspaceSubscriptionService(IWorkspaceSubscriptionService workspaceSubscriptionService)
        {
            _workspaceSubscriptionService = workspaceSubscriptionService;
        }

        [RydrForcedSimpleCacheResponse(30)]
        public async Task<OnlyResultResponse<WorkspaceSubscription>> Get(GetActiveWorkspaceSubscription request)
        {
            var activeWorkspace = await _workspaceSubscriptionService.TryGetActiveWorkspaceSubscriptionAsync(request.Id);

            return activeWorkspace.ToWorkspaceSubscription()
                                  .AsOnlyResultResponse();
        }
    }
}
