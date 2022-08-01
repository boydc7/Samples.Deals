using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Services;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Services
{
    public class CompositeSqlExpressionDecorator : IDecorateSqlExpressionService
    {
        private readonly List<IDecorateSqlExpressionService> _services;

        public CompositeSqlExpressionDecorator(IEnumerable<IDecorateSqlExpressionService> services)
        {
            _services = services.AsList();
        }

        public async Task DecorateAsync<TRequest, TFrom>(TRequest request, SqlExpression<TFrom> query)
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
