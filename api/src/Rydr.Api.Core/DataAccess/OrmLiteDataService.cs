using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using ServiceStack;
using ServiceStack.Model;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.DataAccess
{
    public class OrmLiteDataService : BaseDataService, IRydrDataService
    {
        private readonly IStorageMarshaller _storageMarshaller;
        private readonly Func<IDbConnection> _dbConnectionFactory;
        private IOrmLiteDialectProvider _dialect;

        public OrmLiteDataService(IRydrSqlConnectionFactory rydrSqlConnectionFactory,
                                  IStorageMarshaller storageMarshaller,
                                  string toRegisteredName = null)
        {
            var rydrSqlConnectionFactory1 = rydrSqlConnectionFactory;
            _storageMarshaller = storageMarshaller;

            if (toRegisteredName.HasValue())
            {
                _dbConnectionFactory = () => rydrSqlConnectionFactory1.OpenDbConnection(toRegisteredName);
            }
            else
            {
                _dbConnectionFactory = () => rydrSqlConnectionFactory1.OpenDbConnection();
            }

            Dialect = rydrSqlConnectionFactory1.DialectProvider;
        }

        private IDbConnection OpenConnection()
        {
            var dbConn = _dbConnectionFactory();

            if (_dialect == null)
            {
                _dialect = dbConn.GetDialectProvider();
            }

            return dbConn;
        }

        private T Exec<T>(Func<T> results)
        {
            try
            {
                return results();
            }
            catch(Exception x) when(OnDataAccessFail(x))
            {
                // Eat the exception
                return default;
            }
        }

        private IEnumerable<TReturn> DoSelectById<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver, int batchSize = int.MaxValue)
            where TReturn : IHasId<TIdType>
        {
            using(var dbConnection = OpenConnection())
            {
                var batchResults = _storageMarshaller.GetRange(ids, b => dbConnection.LoadSelect<TReturn>(t => Sql.In(t.Id, b)), idResolver, batchSize);

                foreach (var result in batchResults)
                {
                    yield return result;
                }
            }
        }

        public IOrmLiteDialectProvider Dialect { get; }

        public override TReturn SingleById<TReturn, TIdType>(TIdType id)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            var result = _storageMarshaller.Get(id, i => dbConnection.SingleById<TReturn>(i));

                            return result;
                        }
                    });

        public override async Task<TReturn> SingleByIdAsync<TReturn, TIdType>(TIdType id)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    var result = await _storageMarshaller.GetAsync(id, i => dbConnection.SingleByIdAsync<TReturn>(i));

                    return result;
                }

                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    return default;
                }
            }
        }

        public TReturn Single<TReturn, TIdType>(Func<IDbConnection, TReturn> query, Func<TReturn, TIdType> idResolver)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            var result = _storageMarshaller.Get(() =>
                                                                {
                                                                    var singleInstance = query(dbConnection);

                                                                    return singleInstance;
                                                                }, idResolver);

                            return result;
                        }
                    });

        public async Task<TReturn> SingleAsync<TReturn, TIdType>(Func<IDbConnection, Task<TReturn>> query, Func<TReturn, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    var result = await _storageMarshaller.GetAsync(async () =>
                                                                   {
                                                                       var singleInstance = await query(dbConnection);

                                                                       return singleInstance;
                                                                   }, idResolver);

                    return result;
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    throw; // Will never hit (the log action above will log and just bubble the exception out)
                }
            }
        }

        public override IEnumerable<TReturn> SelectById<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver,
                                                                          int batchSize = int.MaxValue)
            => Exec(() => DoSelectById(ids, idResolver, batchSize));

        public override async Task<IEnumerable<TReturn>> SelectByIdAsync<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver,
                                                                                           int batchSize = int.MaxValue)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    var result = await _storageMarshaller.GetRangeAsync(ids, b => dbConnection.SelectAsync<TReturn>(r => Sql.In(r.Id, b)), idResolver, batchSize);

                    return result;
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))

                {
                    return Enumerable.Empty<TReturn>();
                }
            }
        }

        public IEnumerable<TReturn> QueryLazy<TReturn, TIdType>(Func<IDbConnection, IEnumerable<TReturn>> query, Func<TReturn, TIdType> idResolver)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            return _storageMarshaller.Query(() => query(dbConnection), idResolver);
                        }
                    });

        public async Task<IEnumerable<TReturn>> QueryAsync<TReturn, TIdType>(Func<IDbConnection, Task<List<TReturn>>> query, Func<TReturn, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    var result = await _storageMarshaller.QueryAsync(() => query(dbConnection), idResolver);

                    return result;
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))

                {
                    throw; // Will never hit (the log action above will log and just bubble the exception out)
                }
            }
        }

        public async Task<TReturn> QueryMultipleAsync<TReturn>(string sql, object param, Func<SqlMapper.GridReader, TReturn> callback)
        {
            TReturn returnVal;

            using(var dbConnection = OpenConnection())
            using(var gridReader = await dbConnection.QueryMultipleAsync(sql, param))
            {
                try
                {
                    returnVal = callback(gridReader);
                }
                catch(InvalidOperationException ex)
                {
                    // No results...
                    if (ex.Message == "No columns were selected")
                    {
                        returnVal = default;
                    }
                    else
                    {
                        throw;
                    }
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    return default;
                }
            }

            return returnVal;
        }

        public TReturn QueryAdHoc<TReturn>(Func<IDbConnection, TReturn> query)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            return _storageMarshaller.QueryAdHoc(() => query(dbConnection));
                        }
                    });

        public async Task<TReturn> QueryAdHocAsync<TReturn>(Func<IDbConnection, Task<TReturn>> query)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    return await _storageMarshaller.QueryAdHocAsync(() => query(dbConnection));
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    return default;
                }
            }
        }

        public void InsertRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver, int batchSize = int.MaxValue)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.StoreRange(models,
                                                          m =>
                                                          { // Just a simple optimization to avoid batching if not needed
                                                              if (batchSize == int.MaxValue || batchSize <= 0)
                                                              {
                                                                  dbConnection.InsertAll(m, c => c.OnConflictIgnore());
                                                              }
                                                              else
                                                              {
                                                                  foreach (var modelBatch in m.ToLazyBatchesOf(batchSize))
                                                                  {
                                                                      dbConnection.InsertAll(modelBatch, c => c.OnConflictIgnore());
                                                                  }
                                                              }
                                                          },
                                                          idResolver);

                            return true;
                        }
                    });

        public async Task InsertRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver,
                                                       int batchSize = int.MaxValue)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.StoreRangeAsync(models,
                                                             async m =>
                                                             { // Just a simple optimization to avoid batching if not needed
                                                                 if (batchSize == int.MaxValue || batchSize <= 0)
                                                                 {
                                                                     await dbConnection.InsertAllAsync(m, c => c.OnConflictIgnore());
                                                                 }
                                                                 else
                                                                 {
                                                                     foreach (var modelBatch in m.ToLazyBatchesOf(batchSize))
                                                                     {
                                                                         await dbConnection.InsertAllAsync(modelBatch, c => c.OnConflictIgnore());
                                                                     }
                                                                 }
                                                             },
                                                             idResolver);
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql())) { }
            }
        }

        public override long Insert<T, TIdType>(T model, Func<T, TIdType> idResolver)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.Store(model, m => dbConnection.Insert(m, c => c.OnConflictIgnore()), idResolver);

                            return 0;
                        }
                    });

        public override async Task<long> InsertAsync<T, TIdType>(T model, Func<T, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.StoreAsync(model, async m => await dbConnection.InsertAsync(m, c => c.OnConflictIgnore()), idResolver);

                    return 0;
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    return -1;
                }
            }
        }

        /// <summary>
        ///     Used to update only some fields in the model POCO.
        ///     Populate the partialModel with values on the properties which should be updated (and to what they should be updated
        ///     to)
        ///     This model can include OTHER values (i.e. it could be a fully populated model as well), as you specify the fields
        ///     to update in the next paramter
        ///     Use theseFields to specify which fields to actually update in the data source. If a single field is to be updated
        ///     you can
        ///     use a simple lambda syntax, i.e.:
        ///     t => t.FieldToUpdate
        ///     If more than one field is to be updated, specify an anonymous type with the fields to include, i.e.:
        ///     t => new { t.Field1, t.Field2, t.Field3 }
        ///     The selectFilter is a full-featured SqlExpression that specifies the filter to use to get the records from the data
        ///     source
        ///     to be updated - this can be any SqlExpression that returns the records to be updated (which are then updated back
        ///     to the
        ///     data source in batches)
        ///     The updateFilter is where you specify filters for the update operation itself given the batch of records input to
        ///     the func.
        ///     Here you likely want to specify something like a Sql.In(t.Id, ...) filter along with anything else applicable in
        ///     the batch.
        /// </summary>
        public async Task UpdateOnlyAsync<T, TIdType>(T partialModel, Expression<Func<T, object>> theseFields,
                                                      Func<IDbConnection, SqlExpression<T>> selectFilter,
                                                      Func<IEnumerable<T>, Expression<Func<T, bool>>> updateFilter,
                                                      Func<T, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    foreach (var updateBatch in _storageMarshaller.QueryAdHoc(() => dbConnection.SelectLazy(selectFilter(dbConnection)))
                                                                  .Select(t => t.PopulateWithNonDefaultValues(partialModel))
                                                                  .ToLazyBatchesOf(250))
                    {
                        await _storageMarshaller.StoreRangeAsync(updateBatch,
                                                                 m => dbConnection.UpdateOnlyAsync(partialModel, theseFields,
                                                                                                   updateFilter(m)),
                                                                 idResolver);
                    }
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql())) { }
            }
        }

        /// <summary>
        ///     Used to update only some fields in the model POCO.
        ///     Populate the partialModel with values on the properties which should be updated (and to what they should be updated
        ///     to)
        ///     This model can include OTHER values (i.e. it could be a fully populated model as well), as you specify the fields
        ///     to update in the next paramter
        ///     Use theseFields to specify which fields to actually update in the data source. If a single field is to be updated
        ///     you can
        ///     use a simple lambda syntax, i.e.:
        ///     t => t.FieldToUpdate
        ///     If more than one field is to be updated, specify an anonymous type with the fields to include, i.e.:
        ///     t => new { t.Field1, t.Field2, t.Field3 }
        ///     The selectFilter is a full-featured SqlExpression that specifies the filter to use to get the records from the data
        ///     source
        ///     to be updated - this can be any SqlExpression that returns the records to be updated (which are then updated back
        ///     to the
        ///     data source in batches)
        ///     The updateFilter is where you specify filters for the update operation itself given the batch of records input to
        ///     the func.
        ///     Here you likely want to specify something like a Sql.In(t.Id, ...) filter along with anything else applicable in
        ///     the batch.
        /// </summary>
        public void UpdateOnly<T, TIdType>(T partialModel, Expression<Func<T, object>> theseFields,
                                           Func<IDbConnection, SqlExpression<T>> selectFilter,
                                           Func<IEnumerable<T>, Expression<Func<T, bool>>> updateFilter,
                                           Func<T, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    foreach (var updateBatch in _storageMarshaller.QueryAdHoc(() => dbConnection.SelectLazy(selectFilter(dbConnection)))
                                                                  .Select(t => t.PopulateWithNonDefaultValues(partialModel))
                                                                  .ToLazyBatchesOf(250))
                    {
                        _storageMarshaller.StoreRange(updateBatch,
                                                      m => dbConnection.UpdateOnlyAsync(partialModel, theseFields,
                                                                                        updateFilter(m)),
                                                      idResolver);
                    }
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql())) { }
            }
        }

        /// <summary>
        ///     Used to update the ENTIRE model (including properties with default values (i.e. null/0/empty values)), will
        ///     basically
        ///     bring the database record to match the values on the model passed exactly, ALL fields, regardless of value, with
        ///     the ID in the
        ///     model passed (ID being the Id field/property).
        ///     If you want to update only non-default values on the model, use the UpdateNonDefaults method.
        ///     If you want to specify only certain fields on the model to be updated, use the UpdateOnly method.
        /// </summary>
        public override async Task UpdateAsync<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.StoreAsync(model,
                                                        m => where == null
                                                                 ? dbConnection.UpdateAsync(m)
                                                                 : dbConnection.UpdateAsync(m, where),
                                                        idResolver);
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql())) { }
            }
        }

        public override void Update<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.Store(model, m =>
                                                            {
                                                                if (where == null)
                                                                {
                                                                    dbConnection.Update(m);
                                                                }
                                                                else
                                                                {
                                                                    dbConnection.Update(m, where);
                                                                }
                                                            }, idResolver);

                            return true;
                        }
                    });

        public override void UpdateRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.StoreRange(models, m => dbConnection.UpdateAll(m), idResolver);

                            return true;
                        }
                    });

        public void UpdateNonDefaults<T, TIdType>(T model, Expression<Func<T, bool>> filter, Func<T, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    _storageMarshaller.Store(model,
                                             m => dbConnection.UpdateNonDefaults(m, filter),
                                             idResolver, true);
                }
                catch(ArgumentException wsx) when(wsx.Message.StartsWithOrdinalCi("No non-null or non-default values were provided for type"))
                {
                    // Just eat it...
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql())) { }
            }
        }

        public async Task UpdateNonDefaultsAsync<T, TIdType>(T model, Expression<Func<T, bool>> filter, Func<T, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.StoreAsync(model,
                                                        m => dbConnection.UpdateNonDefaultsAsync(m, filter),
                                                        idResolver, true);
                }
                catch(ArgumentException wsx) when(wsx.Message.StartsWithOrdinalCi("No non-null or non-default values were provided for type"))
                {
                    // Just eat it...
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    // Eat the exception
                }
            }
        }

        public override long Save<T, TIdType>(T model, Func<T, TIdType> idResolver)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.Store(model, m => dbConnection.Save(m), idResolver);

                            return 0;
                        }
                    });

        public override void SaveRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.StoreRange(models, m => dbConnection.SaveAll(m), idResolver);
                        }

                        return true;
                    });

        public override async Task<long> SaveAsync<T, TIdType>(T model, Func<T, TIdType> idResolver)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.StoreAsync(model, async m => await dbConnection.SaveAsync(m), idResolver);

                    return 0;
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                { // Eat the exception, return failure code
                    return -1;
                }
            }
        }

        public override async Task SaveRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver)
        {
            try
            {
                using(var dbConnection = OpenConnection())
                {
                    await _storageMarshaller.StoreRangeAsync(models, m => dbConnection.SaveAllAsync(m), idResolver);
                }
            }
            catch(Exception x) when(OnDataAccessFail(x))
            {
                // Eat the exception
            }
        }

        public override async Task DeleteByIdAsync<T, TIdType>(TIdType id)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.DeleteAsync<T, TIdType>(id, i => dbConnection.DeleteByIdAsync<T>(i));
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    throw; // Will never hit (the log action above will log and just bubble the exception out)
                }
            }
        }

        public override async Task DeleteByIdsAsync<T, TIdType>(IEnumerable<TIdType> ids)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.DeleteRangeAsync<T, TIdType>(ids, i => dbConnection.DeleteByIdsAsync<T>(i));
                }

                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    throw; // Will never hit (the log action above will log and just bubble the exception out)
                }
            }
        }

        public override void DeleteByIds<T, TIdType>(IEnumerable<TIdType> ids)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.DeleteRange<T, TIdType>(ids, i => dbConnection.DeleteByIds<T>(i));

                            return true;
                        }
                    });

        public void DeleteAdHoc<T>(Func<IDbConnection, SqlExpression<T>> deleteQuery)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            _storageMarshaller.DeleteAdHoc<T>(() => dbConnection.Delete(deleteQuery));

                            return true;
                        }
                    });

        public void ExecAdHoc(string sql, object anonDbParams = null)
            => Exec(() =>
                    {
                        using(var dbConnection = OpenConnection())
                        {
                            dbConnection.ExecuteSql(sql, anonDbParams);

                            return true;
                        }
                    });

        public async Task ExecAdHocAsync(string sql, object anonDbParams = null)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await dbConnection.ExecuteSqlAsync(sql, anonDbParams);
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    throw; // Will never hit (the log action above will log and just bubble the exception out)
                }
            }
        }

        public async Task DeleteAdHocAsync<T>(Func<IDbConnection, SqlExpression<T>> deleteQuery)
        {
            using(var dbConnection = OpenConnection())
            {
                try
                {
                    await _storageMarshaller.DeleteAdHocAsync<T>(() => dbConnection.DeleteAsync(deleteQuery));
                }
                catch(Exception x) when(OnDataAccessFail(x, dbConnection.GetLastSql()))
                {
                    throw; // Will never hit (the log action above will log and just bubble the exception out)
                }
            }
        }
    }
}
