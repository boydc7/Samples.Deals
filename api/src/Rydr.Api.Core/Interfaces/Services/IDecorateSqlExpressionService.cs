using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IDecorateSqlExpressionService
{
    Task DecorateAsync<TRequest, TFrom>(TRequest request, SqlExpression<TFrom> query);
}

public interface IDecorateDynamoExpressionService
{
    Task DecorateAsync<TRequest, TFrom>(TRequest request, DataQuery<TFrom> query)
        where TRequest : IQueryDataRequest<TFrom>
        where TFrom : ICanBeRecordLookup;
}
