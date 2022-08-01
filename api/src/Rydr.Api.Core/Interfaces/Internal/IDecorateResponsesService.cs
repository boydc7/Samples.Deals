using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface IDecorateResponsesService
    {
        Task DecorateOneAsync<TRequest, T>(TRequest request, T result)
            where TRequest : class, IHasUserAuthorizationInfo
            where T : class;

        Task DecorateManyAsync<TRequest, T>(TRequest request, ICollection<T> results)
            where T : class
            where TRequest : class, IHasUserAuthorizationInfo;
    }
}
