using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.DataAccess;
using ServiceStack;
using ServiceStack.Model;

namespace Rydr.Api.Core.Extensions
{
    public static class StorageMarshallerExtensions
    {
        public static Task StoreAsync<T>(this IStorageMarshaller marshaller, T model, Func<T, Task> setter)
            where T : IHasId<long>
            => marshaller.StoreAsync(model, setter, t => t.Id);

        public static IEnumerable<T> GetRange<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<TIdType> ids,
                                                          Func<IEnumerable<TIdType>, IEnumerable<T>> getter,
                                                          int batchSize = 250, bool intentToUpdate = false)
            where T : IHasId<TIdType>
            => marshaller.GetRange(ids, getter, t => t.Id, batchSize, intentToUpdate);

        public static Task<IEnumerable<T>> GetRangeAsync<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<TIdType> ids,
                                                                     Func<IEnumerable<TIdType>, Task<List<T>>> getter,
                                                                     int batchSize = int.MaxValue, bool intentToUpdate = false)
            where T : IHasId<TIdType>
            => marshaller.GetRangeAsync(ids, getter, t => t.Id, batchSize, intentToUpdate);

        public static IEnumerable<T> Query<T>(this IStorageMarshaller marshaller, Func<IEnumerable<T>> query, bool intentToUpdate = false)
            where T : IHasId<long>
            => marshaller.Query(query, t => t.Id, intentToUpdate);

        public static IEnumerable<T> Query<T, TIdType>(this IStorageMarshaller marshaller, Func<IEnumerable<T>> query, bool intentToUpdate = false)
            where T : IHasId<TIdType>
            => marshaller.Query(query, t => t.Id, intentToUpdate);

        public static Task<IEnumerable<T>> QueryAsync<T>(this IStorageMarshaller marshaller, Func<Task<List<T>>> query, bool intentToUpdate = false)
            where T : IHasId<long>
            => marshaller.QueryAsync(query, t => t.Id, intentToUpdate);

        public static Task<IEnumerable<T>> QueryAsync<T, TIdType>(this IStorageMarshaller marshaller, Func<Task<List<T>>> query, bool intentToUpdate = false)
            where T : IHasId<TIdType>
            => marshaller.QueryAsync(query, t => t.Id, intentToUpdate);

        public static T Get<T>(this IStorageMarshaller marshaller, Func<T> query)
            where T : IHasId<long>
            => marshaller.Query(() => new[]
                                      {
                                          query()
                                      }, t => t.Id).SingleOrDefault();

        public static async Task<T> GetAsync<T>(this IStorageMarshaller marshaller, Func<Task<T>> query)
            where T : IHasId<long>
        {
            var results = await marshaller.QueryAsync(async () =>
                                                      {
                                                          var result = await query();

                                                          return result.InList();
                                                      },
                                                      i => i.Id);

            return results.SingleOrDefault();
        }

        public static T Get<T, TIdType>(this IStorageMarshaller marshaller, Func<T> query, Func<T, TIdType> idResolver)
            => marshaller.Query(() => new[]
                                      {
                                          query()
                                      }, idResolver).SingleOrDefault();

        public static async Task<T> GetAsync<T, TIdType>(this IStorageMarshaller marshaller, Func<Task<T>> query, Func<T, TIdType> idResolver)
        {
            var results = await marshaller.QueryAsync(async () =>
                                                      {
                                                          var result = await query();

                                                          return result.InList();
                                                      },
                                                      idResolver);

            return results.SingleOrDefault();
        }

        public static T Store<T, TIdType>(this IStorageMarshaller marshaller, T model, Func<T, T> setter)
            where T : IHasId<TIdType>
            => marshaller.Store(model, setter, t => t.Id);

        public static void Store<T, TIdType>(this IStorageMarshaller marshaller, T model, Action<T> setter)
            where T : IHasId<TIdType>
            => marshaller.Store(model, setter, t => t.Id);

        public static Task StoreAsync<T, TIdType>(this IStorageMarshaller marshaller, T model, Func<T, Task> setter)
            where T : IHasId<TIdType>
            => marshaller.StoreAsync(model, setter, t => t.Id);

        public static Task<T> StoreAsync<T, TIdType>(this IStorageMarshaller marshaller, T model, Func<T, Task<T>> setter)
            where T : IHasId<TIdType>
            => marshaller.StoreAsync(model, setter, t => t.Id);

        public static void StoreRange<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<T> models, Action<IEnumerable<T>> setter)
            where T : IHasId<TIdType>
            => marshaller.StoreRange(models, setter, t => t.Id);

        public static IEnumerable<T> StoreRange<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<T> models, Func<IEnumerable<T>, IEnumerable<T>> setter)
            where T : IHasId<TIdType>
            => marshaller.StoreRange(models, setter, t => t.Id);

        public static Task<IEnumerable<T>> StoreRangeAsync<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<T> models, Func<IEnumerable<T>, Task<IEnumerable<T>>> setter)
            where T : IHasId<TIdType>
            => marshaller.StoreRangeAsync(models, setter, t => t.Id);

        public static Task StoreRangeAsync<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<T> models, Func<IEnumerable<T>, Task> setter)
            where T : IHasId<TIdType>
            => marshaller.StoreRangeAsync(models, setter, t => t.Id);

        public static Task DeleteAsync<T, TIdType>(this IStorageMarshaller marshaller, T model, Func<TIdType, Task> exec)
            where T : IHasId<TIdType>
            => marshaller.DeleteAsync<T, TIdType>(model.Id, exec);

        public static Task DeleteRangeAsync<T, TIdType>(this IStorageMarshaller marshaller, IEnumerable<T> models, Func<IEnumerable<TIdType>, Task> exec)
            where T : IHasId<TIdType>
            => marshaller.DeleteRangeAsync<T, TIdType>(models.Select(m => m.Id), exec);
    }
}
