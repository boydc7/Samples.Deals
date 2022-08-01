using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;

namespace Rydr.Api.Services.Filters
{
    public class RequestExecutorFactory
    {
        private readonly IStats _stats;
        private readonly ICounterAndListService _counterService;
        private readonly IServiceCacheInvalidator _serviceCacheInvalidator;
        private readonly IDecorateResponsesService _decorateResponsesService;

        public RequestExecutorFactory(IStats stats,
                                      ICounterAndListService counterService,
                                      IServiceCacheInvalidator serviceCacheInvalidator,
                                      IDecorateResponsesService decorateResponsesService)
        {
            _stats = stats;
            _counterService = counterService;
            _serviceCacheInvalidator = serviceCacheInvalidator;
            _decorateResponsesService = decorateResponsesService;
        }

        public IRequestExecutor<TRequest> CreateRequestExecutor<TRequest>()
            => new DefaultRequestExecutor<TRequest>(_stats, _counterService, _serviceCacheInvalidator, _decorateResponsesService);
    }
}
