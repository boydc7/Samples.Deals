using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Services.Internal
{
    public class CompositeDeferredAffectedProcessingService : IDeferredAffectedProcessingService
    {
        private readonly IEnumerable<IDeferredAffectedProcessingService> _services;

        public CompositeDeferredAffectedProcessingService(IEnumerable<IDeferredAffectedProcessingService> services)
        {
            _services = services;
        }

        public async Task ProcessAsync(PostDeferredAffected request)
        {
            foreach (var service in _services)
            {
                await service.ProcessAsync(request);
            }
        }
    }
}
