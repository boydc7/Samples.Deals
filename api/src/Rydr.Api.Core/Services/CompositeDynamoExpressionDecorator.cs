using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services
{
    public class CompositeDynamoExpressionDecorator : IDecorateDynamoExpressionService
    {
        private readonly List<IDecorateDynamoExpressionService> _services;

        public CompositeDynamoExpressionDecorator(IEnumerable<IDecorateDynamoExpressionService> services)
        {
            _services = services.AsList();
        }

        public async Task DecorateAsync<TRequest, TFrom>(TRequest request, DataQuery<TFrom> query)
            where TRequest : IQueryDataRequest<TFrom>
            where TFrom : ICanBeRecordLookup
        {
            if (request == null || query == null)
            {
                return;
            }

            foreach (var service in _services)
            {
                await service.DecorateAsync(request, query);
            }
        }
    }
}
