using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Models;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.OrmLite;
using ServiceStack.Web;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface IAutoQueryService
    {
        Task<QueryResponse<TFrom>> QueryDbAsync<TRequest, TFrom>(TRequest dto, IRequest ssRequest)
            where TRequest : IQueryDb<TFrom>, IRequestBase;

        Task<QueryResponse<TFrom>> QueryDataAsync<TRequest, TFrom>(TRequest dto, IRequest ssRequest,
                                                                   Action<TRequest, DataQuery<TFrom>> queryDecorator = null)
            where TRequest : QueryData<TFrom>, IQueryDataRequest<TFrom>
            where TFrom : ICanBeRecordLookup, IDynItemGlobalSecondaryIndex;
    }

    public interface IAutoQueryRunner
    {
        Task<QueryResponse<TFrom>> ExecuteQueryAsync<TRequest, TFrom>(IAutoQueryDb autoQueryDb, TRequest dto, SqlExpression<TFrom> query)
            where TRequest : IQueryDb<TFrom>, IRequestBase;

        Task<QueryResponse<TFrom>> ExecuteDataAsync<TRequest, TFrom>(TRequest dto, Dictionary<string, string> requestParams, IRequest ssRequest,
                                                                     Action<TRequest, DataQuery<TFrom>> queryDecorator = null)
            where TRequest : QueryData<TFrom>, IRequestBase, IQueryDataRequest<TFrom>
            where TFrom : ICanBeRecordLookup;
    }
}
