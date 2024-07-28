using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.Web;

namespace Rydr.Api.Core.Services;

public class BasicAutoQueryRunner : IAutoQueryRunner
{
    private readonly Func<IAutoQueryData> _autoQueryDataFactory;
    private readonly IDecorateDynamoExpressionService _dynamoExpressionDecoratorService;

    public BasicAutoQueryRunner(Func<IAutoQueryData> autoQueryDataFactory,
                                IDecorateDynamoExpressionService dynamoExpressionDecoratorService)
    {
        _autoQueryDataFactory = autoQueryDataFactory;
        _dynamoExpressionDecoratorService = dynamoExpressionDecoratorService;
    }

    public async Task<QueryResponse<TFrom>> ExecuteQueryAsync<TRequest, TFrom>(IAutoQueryDb autoQueryDb, TRequest dto, SqlExpression<TFrom> query)
        where TRequest : IQueryDb<TFrom>, IRequestBase
    {
        var result = await autoQueryDb.ExecuteAsync(dto, query);

        return result;
    }

    public async Task<QueryResponse<TFrom>> ExecuteDataAsync<TRequest, TFrom>(TRequest dto, Dictionary<string, string> requestParams, IRequest ssRequest,
                                                                              Action<TRequest, DataQuery<TFrom>> queryDecorator = null)
        where TRequest : QueryData<TFrom>, IRequestBase, IQueryDataRequest<TFrom>
        where TFrom : ICanBeRecordLookup
    {
        var autoQueryData = _autoQueryDataFactory();

        var q = autoQueryData.CreateQuery(dto, requestParams, ssRequest);

        await _dynamoExpressionDecoratorService.DecorateAsync(dto, q);

        queryDecorator?.Invoke(dto, q);

        var result = autoQueryData.Execute(dto, q, ssRequest);

        return result;
    }
}
