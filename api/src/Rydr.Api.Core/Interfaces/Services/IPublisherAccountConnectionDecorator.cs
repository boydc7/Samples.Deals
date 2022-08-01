using System.Threading.Tasks;
using Rydr.Api.Core.Models.Internal;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IPublisherAccountConnectionDecorator
    {
        Task DecorateAsync(PublisherAccountConnectInfo publisherAccountConnectInfo);
    }
}
