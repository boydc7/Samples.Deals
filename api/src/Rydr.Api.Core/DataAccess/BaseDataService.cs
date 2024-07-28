using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using ServiceStack.Logging;
using ServiceStack.Model;

namespace Rydr.Api.Core.DataAccess;

public abstract class BaseDataService : IDataService
{
    private static readonly List<string> _messageContentsToNotLog = new()
                                                                    {
                                                                        "Duplicate",
                                                                        "Deadlock"
                                                                    };

    protected readonly ILog _log;
    protected readonly string _typeName;

    protected BaseDataService()
    {
        _typeName = GetType().Name;
        _log = LogManager.GetLogger(GetType());
    }

    public bool OnDataAccessFail(Exception x, string extraMsg = null, [CallerMemberName] string method = null)
    {
        foreach (var messageToIgnore in _messageContentsToNotLog)
        {
            if (x.Message.IndexOf(messageToIgnore, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _log.DebugInfoFormat(" OnDataAccessFail ignore failure, error msg [{0}], extra info [{1}]", x.Message, extraMsg);

                return false;
            }
        }

        _log.Exception(x, string.Concat("[DataAccess Failure] in [", _typeName, "] method [", method, "]",
                                        extraMsg.HasValue()
                                            ? string.Concat(" - Extra Msg [", extraMsg, "]")
                                            : string.Empty));

        return false;
    }

    public abstract TReturn SingleById<TReturn, TIdType>(TIdType id);
    public abstract Task<TReturn> SingleByIdAsync<TReturn, TIdType>(TIdType id);

    public abstract Task<IEnumerable<TReturn>> SelectByIdAsync<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver, int batchSize = int.MaxValue)
        where TReturn : IHasId<TIdType>;

    public abstract IEnumerable<TReturn> SelectById<TReturn, TIdType>(IEnumerable<TIdType> ids, Func<TReturn, TIdType> idResolver, int batchSize = int.MaxValue)
        where TReturn : IHasId<TIdType>;

    public abstract Task DeleteByIdAsync<T, TIdType>(TIdType id);

    public abstract Task DeleteByIdsAsync<T, TIdType>(IEnumerable<TIdType> ids);
    public abstract void DeleteByIds<T, TIdType>(IEnumerable<TIdType> ids);

    public abstract long Save<T, TIdType>(T model, Func<T, TIdType> idResolver);
    public abstract Task<long> SaveAsync<T, TIdType>(T model, Func<T, TIdType> idResolver);

    public abstract void SaveRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver);
    public abstract Task SaveRangeAsync<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver);

    public abstract long Insert<T, TIdType>(T model, Func<T, TIdType> idResolver);
    public abstract Task<long> InsertAsync<T, TIdType>(T model, Func<T, TIdType> idResolver);

    public abstract void Update<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null);
    public abstract void UpdateRange<T, TIdType>(IEnumerable<T> models, Func<T, TIdType> idResolver);
    public abstract Task UpdateAsync<T, TIdType>(T model, Func<T, TIdType> idResolver, Expression<Func<T, bool>> where = null);
}
