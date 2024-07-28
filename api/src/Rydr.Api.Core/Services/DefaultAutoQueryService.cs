using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite.Dapper;
using ServiceStack.Web;

namespace Rydr.Api.Core.Services;

public class DefaultAutoQueryService : IAutoQueryService
{
    private static readonly List<string> _queryBaseProperyNames = TypeProperties<QueryBase>.Instance
                                                                                           .PublicPropertyInfos
                                                                                           .Select(p => p.GetDataMemberName()
                                                                                                         .Coalesce(p.Name))
                                                                                           .AsList();

    private readonly int _defaultTake = RydrEnvironment.GetAppSetting("Query.DefaultLimit", 100);

    private readonly Func<IAutoQueryDb> _autoQueryDbFactory;
    private readonly IDecorateSqlExpressionService _sqlExpressionDecoratorService;
    private readonly IAutoQueryRunner _queryRunner;

    public DefaultAutoQueryService(Func<IAutoQueryDb> autoQueryDb,
                                   IDecorateSqlExpressionService sqlExpressionDecoratorService,
                                   IAutoQueryRunner queryRunner)
    {
        _autoQueryDbFactory = autoQueryDb;
        _sqlExpressionDecoratorService = sqlExpressionDecoratorService;
        _queryRunner = queryRunner;
    }

    public async Task<QueryResponse<TFrom>> QueryDbAsync<TRequest, TFrom>(TRequest dto, IRequest ssRequest)
        where TRequest : IQueryDb<TFrom>, IRequestBase
    {
        if (!dto.Take.HasValue || dto.Take.Value <= 0)
        {
            dto.Take = _defaultTake;
        }

        var autoQueryDb = _autoQueryDbFactory();

        var q = autoQueryDb.CreateQuery(dto, ssRequest.GetRequestParams(), ssRequest);

        await _sqlExpressionDecoratorService.DecorateAsync(dto, q);

        var result = await _queryRunner.ExecuteQueryAsync(autoQueryDb, dto, q);

        return result;
    }

    public async Task<QueryResponse<TFrom>> QueryDataAsync<TRequest, TFrom>(TRequest dto, IRequest ssRequest,
                                                                            Action<TRequest, DataQuery<TFrom>> queryDecorator = null)
        where TRequest : QueryData<TFrom>, IQueryDataRequest<TFrom>
        where TFrom : ICanBeRecordLookup, IDynItemGlobalSecondaryIndex
    {
        if (!dto.Take.HasValue || dto.Take.Value <= 0)
        {
            dto.Take = _defaultTake;
        }

        var dtoPropertyNames = TypeProperties<TRequest>.Instance
                                                       .PublicPropertyInfos?
                                                       .Select(p => p.GetDataMemberName()
                                                                     .Coalesce(p.Name))
                                                       .AsHashSet(StringComparer.OrdinalIgnoreCase);

        dtoPropertyNames.ExceptWith(_queryBaseProperyNames);

        var requestParams = ssRequest.GetRequestParams()
                                     .Where(kvp => dtoPropertyNames == null
                                                   ||
                                                   !dtoPropertyNames.Contains(kvp.Key))
                                     .ToDictionary(k => k);

        var result = await _queryRunner.ExecuteDataAsync(dto, requestParams, ssRequest, queryDecorator);

        return result;
    }
}
