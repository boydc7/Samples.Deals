using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Internal;

namespace Rydr.Api.Core.Services.Publishers;

public class CompositePublisherAccountConnectionDecorator : IPublisherAccountConnectionDecorator
{
    private readonly IReadOnlyList<IPublisherAccountConnectionDecorator> _decorators;

    public CompositePublisherAccountConnectionDecorator(IEnumerable<IPublisherAccountConnectionDecorator> decorators)
    {
        _decorators = decorators.AsListReadOnly();
    }

    public async Task DecorateAsync(PublisherAccountConnectInfo publisherAccountConnectInfo)
    {
        foreach (var decorator in _decorators)
        {
            await decorator.DecorateAsync(publisherAccountConnectInfo);
        }
    }
}
