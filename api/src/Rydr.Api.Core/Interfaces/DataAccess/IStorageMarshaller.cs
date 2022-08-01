using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rydr.Api.Core.Interfaces.DataAccess
{
    public interface IStorageMarshaller
    {
        T Get<T, TIdType>(TIdType id, Func<TIdType, T> getter);
        Task<T> GetAsync<T, TIdType>(TIdType id, Func<TIdType, Task<T>> getter);

        IEnumerable<T> GetRange<T, TIdType>(IEnumerable<TIdType> ids, Func<IEnumerable<TIdType>, IEnumerable<T>> getter, Func<T, TIdType> idResolver,
                                            int batchSize = 250, bool intentToUpdate = false);

        Task<IEnumerable<T>> GetRangeAsync<T, TIdType>(IEnumerable<TIdType> ids, Func<IEnumerable<TIdType>, Task<List<T>>> getter, Func<T, TIdType> idResolver,
                                                       int batchSize = int.MaxValue, bool intentToUpdate = false);

        IEnumerable<T> Query<T, TIdType>(Func<IEnumerable<T>> query, Func<T, TIdType> idResolver, bool intentToUpdate = false);
        Task<IEnumerable<T>> QueryAsync<T, TIdType>(Func<Task<List<T>>> query, Func<T, TIdType> idResolver, bool intentToUpdate = false);

        T Store<T, TIdType>(T model, Func<T, T> setter, Func<T, TIdType> idResolver);
        void Store<T, TIdType>(T model, Action<T> setter, Func<T, TIdType> idResolver, bool partialModel = false);

        Task StoreAsync<T, TIdType>(T model, Func<T, Task> setter, Func<T, TIdType> idResolver, bool partialModel = false);
        Task<T> StoreAsync<T, TIdType>(T model, Func<T, Task<T>> setter, Func<T, TIdType> idResolver);

        IEnumerable<T> StoreRange<T, TIdType>(IEnumerable<T> models, Func<IEnumerable<T>, IEnumerable<T>> setter, Func<T, TIdType> idResolver);
        void StoreRange<T, TIdType>(IEnumerable<T> models, Action<IEnumerable<T>> setter, Func<T, TIdType> idResolver);

        Task<IEnumerable<T>> StoreRangeAsync<T, TIdType>(IEnumerable<T> models, Func<IEnumerable<T>, Task<IEnumerable<T>>> setter, Func<T, TIdType> idResolver);
        Task StoreRangeAsync<T, TIdType>(IEnumerable<T> models, Func<IEnumerable<T>, Task> setter, Func<T, TIdType> idResolver);

        Task DeleteAsync<T, TIdType>(TIdType id, Func<TIdType, Task> exec);
        Task DeleteRangeAsync<T, TIdType>(IEnumerable<TIdType> ids, Func<IEnumerable<TIdType>, Task> exec);
        void DeleteRange<T, TIdType>(IEnumerable<TIdType> ids, Action<IEnumerable<TIdType>> exec);

        T QueryAdHoc<T>(Func<T> query);
        Task<T> QueryAdHocAsync<T>(Func<Task<T>> query);

        void DeleteAdHoc<T>(Action action);
        Task DeleteAdHocAsync<T>(Func<Task> action);
    }
}
