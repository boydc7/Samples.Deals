using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using MySql.Data.MySqlClient;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Interfaces;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Model;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Extensions;

public static class DataServiceExtensions
{
    private static readonly IAuthorizationService _authorizationService = RydrEnvironment.Container.Resolve<IAuthorizationService>();
    private static readonly IRequestStateManager _requestStateManager = RydrEnvironment.Container.Resolve<IRequestStateManager>();

    public static IEnumerable<T> ReadOrDefaults<T>(this SqlMapper.GridReader gridReader)
        => gridReader == null || gridReader.IsConsumed
               ? Enumerable.Empty<T>()
               : gridReader.Read<T>();

    public static T ReadOrDefault<T>(this SqlMapper.GridReader gridReader)
        => gridReader == null || gridReader.IsConsumed
               ? default
               : gridReader.ReadFirstOrDefault<T>();

    public static async Task<bool> SoftDeleteByIdAsync<T>(this IDataService dataService, long id, IHasUserAuthorizationInfo state = null)
        where T : ICanBeAuthorized, IHasId<long>, IDateTimeDeleteTracked
    {
        var existing = await dataService.SingleByIdAsync<T>(id);

        if (state == null)
        {
            state = _requestStateManager.GetState();
        }

        if (existing == null || existing.DeletedOn.HasValue)
        {
            return false;
        }

        await _authorizationService.VerifyAccessToAsync(existing, state);

        existing.DeletedBy = state.UserId;
        existing.DeletedOn = DateTimeHelper.UtcNow;

        existing.UpdateDateTimeDeleteTrackedValuesOnly(state);

        await dataService.UpdateAsync(existing);

        return true;
    }

    public static Task<TReturn> SingleByIdAsync<TReturn>(this IDataService dataService, long id)
        => dataService.SingleByIdAsync<TReturn, long>(id);

    public static async Task<TReturn> TrySingleByIdAsync<TReturn>(this IDataService dataService, long id)
        where TReturn : class
    {
        try
        {
            return await dataService.SingleByIdAsync<TReturn, long>(id);
        }
        catch(RecordNotFoundException)
        {
            return null;
        }
    }

    public static TReturn SingleById<TReturn>(this IDataService dataService, long id)
        => dataService.SingleById<TReturn, long>(id);

    public static Task<TReturn> SingleAsync<TReturn, TIdType>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<TIdType>
        => dataService.SingleAsync(d => d.SingleAsync(expression), t => t.Id);

    public static Task<IEnumerable<TReturn>> SelectByIdAsync<TReturn>(this IDataService dataService, IEnumerable<long> ids, int batchSize = int.MaxValue)
        where TReturn : IHasId<long>
        => dataService.SelectByIdAsync<TReturn, long>(ids, t => t.Id, batchSize);

    public static IEnumerable<TReturn> SelectById<TReturn>(this IDataService dataService, IEnumerable<long> ids, int batchSize = int.MaxValue)
        where TReturn : IHasId<long>
        => dataService.SelectById<TReturn, long>(ids, t => t.Id, batchSize);

    public static Task DeleteByIdAsync<TReturn>(this IDataService dataService, long id)
        => dataService.DeleteByIdAsync<TReturn, long>(id);

    public static long Save<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.Save(model, t => t.Id);

    public static Task<long> SaveAsync<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.SaveAsync(model, t => t.Id);

    public static Task<long> SaveAsync<T, TIdType>(this IDataService dataService, T model)
        where T : IHasId<TIdType>
        => dataService.SaveAsync(model, t => t.Id);

    public static void SaveRange<T>(this IDataService dataService, IEnumerable<T> models)
        where T : IHasId<long>
        => dataService.SaveRange(models, t => t.Id);

    public static Task SaveRangeAsync<T>(this IDataService dataService, IEnumerable<T> models)
        where T : IHasId<long>
        => dataService.SaveRangeAsync(models, t => t.Id);

    public static Task SaveRangeAsync<T, TIdType>(this IDataService dataService, IEnumerable<T> models)
        where T : IHasId<TIdType>
        => dataService.SaveRangeAsync(models, t => t.Id);

    public static long Insert<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.Insert(model, t => t.Id);

    public static Task<long> InsertAsync<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.InsertAsync(model, t => t.Id);

    public static Task<long> InsertAsync<T, TIdType>(this IDataService dataService, T model)
        where T : IHasId<TIdType>
        => dataService.InsertAsync(model, t => t.Id);

    public static void Update<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.Update(model, t => t.Id);

    public static void UpdateRange<T>(this IDataService dataService, IEnumerable<T> models)
        where T : IHasId<long>
        => dataService.UpdateRange(models, t => t.Id);

    public static Task UpdateAsync<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.UpdateAsync(model, t => t.Id);

    public static Task UpdateAsync<T, TIdType>(this IDataService dataService, T model)
        where T : IHasId<TIdType>
        => dataService.UpdateAsync(model, t => t.Id);

    public static Task DeleteAsync<T>(this IDataService dataService, T model)
        where T : IHasId<long>
        => dataService.DeleteByIdAsync<T, long>(model.Id);

    public static Task DeleteAsync<T, TIdType>(this IDataService dataService, T model)
        where T : IHasId<TIdType>
        => dataService.DeleteByIdAsync<T, TIdType>(model.Id);
}

public static class RdbmsDataServiceExtensions
{
    private static readonly ILog _log = LogManager.GetLogger("RdbmsDataServiceExtensions");
    private static readonly ConcurrentDictionary<string, long> _enumMap = new(StringComparer.OrdinalIgnoreCase);

    public static long GetOrCreateRydrEnumId(this IRdbmsDataService dataService, string name)
        => _enumMap.GetOrAdd(name,
                             n =>
                             {
                                 var lName = name.ToLowerInvariant();

                                 var existing = dataService.TrySingle<RydrEnum, long>(e => e.Name == lName);

                                 if (existing != null)
                                 {
                                     return existing.Id;
                                 }

                                 var newId = Sequences.Next<RydrEnum>();

                                 dataService.Insert(new RydrEnum
                                                    {
                                                        Id = newId,
                                                        Name = lName
                                                    },
                                                    r => r.Id);

                                 return newId;
                             });

    public static IOrmLiteDialectProvider GetDialectProvider(this IRydrDataService dataService)
        => dataService.Dialect;

    public static async Task SaveIgnoreConflictAsync<T, TIdType>(this IRydrDataService service, T model, Func<T, TIdType> idResolver,
                                                                 bool retry = false)
        where T : IHasId<TIdType>
    {
        var attempted = false;

        do
        {
            try
            {
                await service.SaveAsync(model, idResolver);

                return;
            }
            catch(MySqlException myx) when(myx.Message.IndexOf("Duplicate entry ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (retry && !attempted)
                {
                    await Task.Delay(1250);

                    attempted = true;

                    continue;
                }

                // Ignore duplicate entry exceptions
                _log.Warn($"Duplicate entry exception in RydrDataService.SaveAsync attempt, ignoring and continuing - type/id [{typeof(T).Name} : {idResolver(model)}]", myx);

                return;
            }
        } while (true);
    }

    public static string GetTableName<T>(this IRydrDataService dataService, bool withSchema = false)
        => SqlHelpers.GetTableName<T>(dataService.Dialect, withSchema);

    public static T Scalar<T>(this IRdbmsDataService dataService, Func<IDbConnection, T> query)
        => dataService.Single<T, T>(query, null);

    public static TReturn Single<TReturn>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<long>
        => dataService.Single(d => d.Single(expression), t => t?.Id);

    public static TReturn Single<TReturn, TIdType>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<TIdType>
        => dataService.Single(d => d.Single(expression), t => t == null
                                                                  ? default
                                                                  : t.Id);

    public static TReturn TrySingle<TReturn, TIdType>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : class, IHasId<TIdType>
    {
        try
        {
            return dataService.Single(d => d.Single(expression), t => t == null
                                                                          ? default
                                                                          : t.Id);
        }
        catch(NullReferenceException)
        {
            return default;
        }
        catch(RecordNotFoundException)
        {
            return default;
        }
    }

    public static Task<TReturn> SingleAsync<TReturn>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<long>
        => dataService.SingleAsync(d => d.SingleAsync(expression), t => t.Id);

    public static async Task<TReturn> TrySingleAsync<TReturn>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : class, IHasId<long>
    {
        try
        {
            return await dataService.SingleAsync(d => d.SingleAsync(expression), t => t?.Id);
        }
        catch(NullReferenceException)
        {
            return default;
        }
        catch(RecordNotFoundException)
        {
            return default;
        }
    }

    public static Task<TReturn> SingleAsync<TReturn, TIdType>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<TIdType>
        => dataService.SingleAsync(d => d.SingleAsync(expression), t => t.Id);

    public static IEnumerable<TReturn> QueryLazy<TReturn>(this IRdbmsDataService dataService, Func<IDbConnection, IEnumerable<TReturn>> query)
        where TReturn : IHasId<long>
        => dataService.QueryLazy(query, t => t.Id);

    public static List<TReturn> Query<TReturn>(this IRdbmsDataService dataService, Func<IDbConnection, IEnumerable<TReturn>> query)
        where TReturn : IHasId<long>
        => dataService.QueryLazy(query, t => t.Id).AsList();

    public static IEnumerable<TReturn> QueryLazy<TReturn, TIdType>(this IRdbmsDataService dataService, Func<IDbConnection, IEnumerable<TReturn>> query)
        where TReturn : IHasId<TIdType>
        => dataService.QueryLazy(query, t => t.Id);

    public static Task<IEnumerable<TReturn>> QueryAsync<TReturn>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<long>
        => dataService.QueryAsync(d => d.SelectAsync(expression), t => t.Id);

    public static Task<IEnumerable<TReturn>> QueryAsync<TReturn, TIdType>(this IRdbmsDataService dataService, Expression<Func<TReturn, bool>> expression)
        where TReturn : IHasId<TIdType>
        => dataService.QueryAsync(d => d.SelectAsync(expression), t => t.Id);

    public static void UpdateNonDefaults<T>(this IRdbmsDataService dataService, T model)
        where T : IHasSettableId
    {
        var idValue = model.Id;

        var modelToUseForUpdate = model.CreateCopy().ThenDo(m => m.Id = 0);

        dataService.UpdateNonDefaults(modelToUseForUpdate, m => m.Id == idValue, t => idValue);
    }

    public static void InsertRange<T>(this IRdbmsDataService dataService, IEnumerable<T> models, int batchSize = int.MaxValue)
        where T : IHasId<long>
        => dataService.InsertRange(models, t => t.Id, batchSize);

    public static Task InsertRangeAsync<T>(this IRdbmsDataService dataService, IEnumerable<T> models, int batchSize = int.MaxValue)
        where T : IHasId<long>
        => dataService.InsertRangeAsync(models, t => t.Id, batchSize);

    public static Task InsertRangeAsync<T, TIdType>(this IRdbmsDataService dataService, IEnumerable<T> models, int batchSize = int.MaxValue)
        where T : IHasId<TIdType>
        => dataService.InsertRangeAsync(models, t => t.Id, batchSize);

    public static async Task InsertQueryAsync<T>(this IRdbmsDataService dataService, Func<IDbConnection, SqlExpression<T>> query, Func<T, T> transform = null)
        where T : IHasId<long>
    {
        var toInsert = (await dataService.QueryAdHocAsync(db => db.SelectAsync(query(db)))
                       ).Select(q => transform == null
                                         ? q
                                         : transform(q));

        await InsertRangeAsync(dataService, toInsert);
    }

    public static Dictionary<TKey, TValue> Map<TKey, TValue>(this IRdbmsDataService dataService, Func<IDbConnection, ISqlExpression> query)
        => dataService.QueryAdHoc(db => db.Dictionary<TKey, TValue>(query(db)));
}
