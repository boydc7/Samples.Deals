using System.Collections.Generic;
using ServiceStack.Caching;

namespace Rydr.Api.Core.Interfaces.Services
{
    public interface ILocalDistributedCacheClient : ICacheClient
    {
        IEnumerable<T> GetRange<T>(IEnumerable<string> keys);
    }

    public interface ILocalRequestCacheClient : ICacheClient { }
}
