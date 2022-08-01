using Funq;
using ServiceStack;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IAppHostConfigurer
    {
        void Apply(ServiceStackHost appHost, Container container);
    }
}
