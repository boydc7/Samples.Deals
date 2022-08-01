using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Interfaces;

// ReSharper disable UnusedParameter.Local

namespace Rydr.Api.Core.Services.Internal
{
    public class PublisherAccountResponseDecoratorService : IDecorateResponsesService
    {
        private readonly IPublisherAccountService _publisherAccountService;

        public PublisherAccountResponseDecoratorService(IPublisherAccountService publisherAccountService)
        {
            _publisherAccountService = publisherAccountService;
        }

        public async Task DecorateManyAsync<TRequest, T>(TRequest request, ICollection<T> results)
            where T : class
            where TRequest : class, IHasUserAuthorizationInfo
        {
            if (results == null || results.Count <= 0)
            {
                return;
            }

            await DecorateManyInternalAsync(request, results);
        }

        public async Task DecorateOneAsync<TRequest, T>(TRequest request, T response)
            where TRequest : class, IHasUserAuthorizationInfo
            where T : class
        {
            switch (response)
            {
                case null:

                    return;

                case IDecorateWithPublisherAccountInfo responsePubInfo:
                    if (responsePubInfo.PublisherAccountId <= 0)
                    {
                        return;
                    }

                    var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(responsePubInfo.PublisherAccountId);

                    responsePubInfo.PublisherAccount = publisherAccount.ToPublisherAccountInfo();

                    //responsePubInfo.PublisherAccount.Metrics = null;

                    return;
            }
        }

        private async Task DecorateManyInternalAsync<TRequest>(TRequest request, IEnumerable<object> results)
            where TRequest : class, IHasUserAuthorizationInfo
        {
            var firstResult = results.FirstOrDefault();

            switch (firstResult)
            {
                case null:

                    return;

                // ReSharper disable once UnusedVariable
                case IDecorateWithPublisherAccountInfo responsePubInfo:
                    foreach (var result in results.SafeCast<IDecorateWithPublisherAccountInfo>()
                                                  .Where(r => r != null && r.PublisherAccountId > 0))
                    {
                        var publisherAccount = await _publisherAccountService.TryGetPublisherAccountAsync(result.PublisherAccountId);

                        result.PublisherAccount = publisherAccount.ToPublisherAccountInfo();

                        //pa.PublisherAccount.Metrics = null;
                    }

                    return;
            }
        }
    }
}
