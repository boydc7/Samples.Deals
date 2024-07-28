using System.Data;
using System.Linq.Expressions;
using ServiceStack.Model;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Interfaces.DataAccess;

public interface IDataService
{
    TReturn SingleById<TReturn, TIdType>(TIdType id);

    Task<TReturn> SingleByIdAsync<TReturn, TIdType>(TIdType id);

    Task<IEnumerable<TReturn>> SelectByIdAsync<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver,
                                                                 int batchSize = int.MaxValue)
        where TReturn : IHasId<TIdType>;

    IEnumerable<TReturn> SelectById<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver,
                                                      int batchSize = int.MaxValue)
        where TReturn : IHasId<TIdType>;

    Task DeleteByIdAsync<T, TIdType>(TIdType id);

    Task DeleteByIdsAsync<T, TIdType>(IEnumerable<TIdType> ids);
    void DeleteByIds<T, TIdType>(IEnumerable<TIdType> ids);

    long Save<T, TIdType>(T model, Func<T, TIdType> idResolver);
    Task<long> SaveAsync<T, TIdType>(T model, Func<T, TIdType> idResolver);

    void SaveRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver);
    Task SaveRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver);

    long Insert<T, TIdType>(T model, Func<T, TIdType> idResolver);
    Task<long> InsertAsync<T, TIdType>(T model, Func<T, TIdType> idResolver);

    void Update<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null);
    void UpdateRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver);
    Task UpdateAsync<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null);
}

public interface IRdbmsDataService : IDataService
{
    IOrmLiteDialectProvider Dialect { get; }

    TReturn Single<TReturn, TIdType>(Func<IDbConnection, TReturn> query, Func<TReturn, TIdType> idResolver);

    Task<TReturn> SingleAsync<TReturn, TIdType>(Func<IDbConnection, Task<TReturn>> query, Func<TReturn, TIdType> idResolver);

    IEnumerable<TReturn> QueryLazy<TReturn, TIdType>(Func<IDbConnection, IEnumerable<TReturn>> query, Func<TReturn, TIdType> idResolver);

    Task<IEnumerable<TReturn>> QueryAsync<TReturn, TIdType>(Func<IDbConnection, Task<List<TReturn>>> query, Func<TReturn, TIdType> idResolver);
    TReturn QueryAdHoc<TReturn>(Func<IDbConnection, TReturn> query);
    Task<TReturn> QueryAdHocAsync<TReturn>(Func<IDbConnection, Task<TReturn>> query);

    Task<TReturn> QueryMultipleAsync<TReturn>(string sql, object param, Func<SqlMapper.GridReader, TReturn> callback);

    void InsertRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver, int batchSize = int.MaxValue);
    Task InsertRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver, int batchSize = int.MaxValue);

    void UpdateNonDefaults<T, TIdType>(T model, Expression<Func<T, bool>> filter, Func<T, TIdType> idResolver);
    Task UpdateNonDefaultsAsync<T, TIdType>(T model, Expression<Func<T, bool>> filter, Func<T, TIdType> idResolver);

    Task UpdateOnlyAsync<T, TIdType>(T partialModel, Func<IDbConnection, SqlExpression<T>> selectFilter,
                                     Func<IEnumerable<T>, Expression<Func<T, bool>>> updateFilter,
                                     Func<T, TIdType> idResolver);

    void UpdateOnly<T, TIdType>(T partialModel, Func<IDbConnection, SqlExpression<T>> selectFilter,
                                Func<IEnumerable<T>, Expression<Func<T, bool>>> updateFilter,
                                Func<T, TIdType> idResolver);

    void DeleteAdHoc<T>(Func<IDbConnection, SqlExpression<T>> deleteQuery);
    Task DeleteAdHocAsync<T>(Func<IDbConnection, SqlExpression<T>> deleteQuery);

    void ExecAdHoc(string sql, object anonDbParams = null);
    Task ExecAdHocAsync(string sql, object anonDbParams = null);
}

public interface IRydrDataService : IRdbmsDataService { }
