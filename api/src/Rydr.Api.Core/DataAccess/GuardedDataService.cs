using System.Data;
using System.Linq.Expressions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Services.Internal;
using ServiceStack.Model;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.DataAccess;

public class GuardedDataService : IDataService
{
    private readonly IDataService _realDataService;

    public GuardedDataService(IDataService realDataService)
    {
        _realDataService = realDataService;
    }

    public async Task<TReturn> SingleByIdAsync<TReturn, TIdType>(TIdType id)
    {
        var item = await _realDataService.SingleByIdAsync<TReturn, TIdType>(id);

        Guard.AgainstRecordNotFound(item == null, string.Concat(typeof(TReturn).ToString(), id.ToString()));

        return item;
    }

    public TReturn SingleById<TReturn, TIdType>(TIdType id)
    {
        var item = _realDataService.SingleById<TReturn, TIdType>(id);

        Guard.AgainstRecordNotFound(item == null, string.Concat(typeof(TReturn).ToString(), id.ToString()));

        return item;
    }

    public async Task<IEnumerable<TReturn>> SelectByIdAsync<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver, int batchSize = int.MaxValue)
        where TReturn : IHasId<TIdType>
        => (await _realDataService.SelectByIdAsync(ids, idResolver) ?? []).Where(m => m != null);

    public IEnumerable<TReturn> SelectById<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver, int batchSize = int.MaxValue)
        where TReturn : IHasId<TIdType>
        => (_realDataService.SelectById(ids, idResolver) ?? []).Where(m => m != null);

    public Task DeleteByIdAsync<T, TIdType>(TIdType id)
        => _realDataService.DeleteByIdAsync<T, TIdType>(id);

    public Task DeleteByIdsAsync<T, TIdType>(IEnumerable<TIdType> ids)
        => _realDataService.DeleteByIdsAsync<T, TIdType>(ids);

    public void DeleteByIds<T, TIdType>(IEnumerable<TIdType> ids)
        => _realDataService.DeleteByIds<T, TIdType>(ids);

    public long Save<T, TIdType>(T model, Func<T, TIdType> idResolver)
        => model == null
               ? 0
               : _realDataService.Save(model, idResolver);

    public Task<long> SaveAsync<T, TIdType>(T model, Func<T, TIdType> idResolver)
        => model == null
               ? Task.FromResult(0L)
               : _realDataService.SaveAsync(model, idResolver);

    public void SaveRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
    {
        if (models == null)
        {
            return;
        }

        _realDataService.SaveRange(models, idResolver);
    }

    public Task SaveRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
        => _realDataService.SaveRangeAsync((models ?? []).Where(m => m != null), idResolver);

    public long Insert<T, TIdType>(T model, Func<T, TIdType> idResolver)
        => model == null
               ? 0L
               : _realDataService.Insert(model, idResolver);

    public Task<long> InsertAsync<T, TIdType>(T model, Func<T, TIdType> idResolver)
        => model == null
               ? Task.FromResult(0L)
               : _realDataService.InsertAsync(model, idResolver);

    public void Update<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null)
    {
        if (model == null)
        {
            return;
        }

        _realDataService.Update(model, idResolver, where);
    }

    public void UpdateRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
    {
        if (models == null)
        {
            return;
        }

        _realDataService.UpdateRange(models, idResolver);
    }

    public Task UpdateAsync<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null)
        => model == null
               ? Task.FromResult(0L)
               : _realDataService.UpdateAsync(model, idResolver, where);
}

public class GuardedRdbmsDataService : GuardedDataService, IRydrDataService
{
    private readonly IRdbmsDataService _realRdbmsDataService;

    public GuardedRdbmsDataService(IRdbmsDataService realRdbmsDataService)
        : base(realRdbmsDataService)
    {
        _realRdbmsDataService = realRdbmsDataService;
    }

    public async Task<TReturn> SingleAsync<TReturn, TIdType>(Func<IDbConnection, Task<TReturn>> query, Func<TReturn, TIdType> idResolver)
    {
        var item = await _realRdbmsDataService.SingleAsync(query, idResolver);

        Guard.AgainstRecordNotFound(item == null, typeof(TReturn).ToString());

        return item;
    }

    public TReturn Single<TReturn, TIdType>(Func<IDbConnection, TReturn> query, Func<TReturn, TIdType> idResolver)
    {
        var item = _realRdbmsDataService.Single(query, idResolver);

        Guard.AgainstRecordNotFound(item == null, typeof(TReturn).ToString());

        return item;
    }

    public IEnumerable<TReturn> QueryLazy<TReturn, TIdType>(Func<IDbConnection, IEnumerable<TReturn>> query, Func<TReturn, TIdType> idResolver)
        => (_realRdbmsDataService.QueryLazy(query, idResolver) ?? []).Where(m => m != null);

    public TReturn QueryAdHoc<TReturn>(Func<IDbConnection, TReturn> query)
        => _realRdbmsDataService.QueryAdHoc(query);

    public Task<TReturn> QueryAdHocAsync<TReturn>(Func<IDbConnection, Task<TReturn>> query)
        => _realRdbmsDataService.QueryAdHocAsync(query);

    public async Task<IEnumerable<TReturn>> QueryAsync<TReturn, TIdType>(Func<IDbConnection, Task<List<TReturn>>> query, Func<TReturn, TIdType> idResolver)
        => (await _realRdbmsDataService.QueryAsync(query, idResolver) ?? []).Where(m => m != null);

    public void InsertRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver, int batchSize = int.MaxValue)
        => _realRdbmsDataService.InsertRange((models ?? []).Where(m => m != null), idResolver, batchSize);

    public Task InsertRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver, int batchSize = int.MaxValue)
        => _realRdbmsDataService.InsertRangeAsync((models ?? []).Where(m => m != null), idResolver, batchSize);

    public void UpdateNonDefaults<T, TIdType>(T model, Expression<Func<T, bool>> filter, Func<T, TIdType> idResolver)
    {
        if (model == null)
        {
            return;
        }

        _realRdbmsDataService.UpdateNonDefaults(model, filter, idResolver);
    }

    public Task UpdateNonDefaultsAsync<T, TIdType>(T model, Expression<Func<T, bool>> filter, Func<T, TIdType> idResolver)
        => model == null
               ? Task.CompletedTask
               : _realRdbmsDataService.UpdateNonDefaultsAsync(model, filter, idResolver);

    public void UpdateOnly<T, TIdType>(T partialModel, Func<IDbConnection, SqlExpression<T>> selectFilter,
                                       Func<IEnumerable<T>, Expression<Func<T, bool>>> updateFilter,
                                       Func<T, TIdType> idResolver)
    {
        if (partialModel == null)
        {
            return;
        }

        _realRdbmsDataService.UpdateOnly(partialModel, selectFilter, updateFilter, idResolver);
    }

    public Task UpdateOnlyAsync<T, TIdType>(T partialModel, Func<IDbConnection, SqlExpression<T>> selectFilter,
                                            Func<IEnumerable<T>, Expression<Func<T, bool>>> updateFilter,
                                            Func<T, TIdType> idResolver)
        => partialModel == null
               ? Task.CompletedTask
               : _realRdbmsDataService.UpdateOnlyAsync(partialModel, selectFilter, updateFilter, idResolver);

    public void DeleteAdHoc<T>(Func<IDbConnection, SqlExpression<T>> deleteQuery)
        => _realRdbmsDataService.DeleteAdHoc(deleteQuery);

    public Task DeleteAdHocAsync<T>(Func<IDbConnection, SqlExpression<T>> deleteQuery)
        => _realRdbmsDataService.DeleteAdHocAsync(deleteQuery);

    public void ExecAdHoc(string sql, object anonDbParams = null)
        => _realRdbmsDataService.ExecAdHoc(sql, anonDbParams);

    public Task ExecAdHocAsync(string sql, object anonDbParams = null)
        => _realRdbmsDataService.ExecAdHocAsync(sql, anonDbParams);

    public Task<TReturn> QueryMultipleAsync<TReturn>(string sql, object param, Func<SqlMapper.GridReader, TReturn> callback)
        => _realRdbmsDataService.QueryMultipleAsync(sql, param, callback);

    public IOrmLiteDialectProvider Dialect => _realRdbmsDataService.Dialect;
}
