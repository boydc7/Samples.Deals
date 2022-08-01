using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk
{
    public interface IInstagramBasicClient : IDisposable
    {
        string AppId { get; }

        Task<IgLongLivedAccessTokenResponse> RefreshLongLivedAccessTokenAsync();

        Task<IgAccount> GetMyAccountAsync(bool honorEtag = true);
        IAsyncEnumerable<List<IgMedia>> GetBasicIgAccountMediaAsync(int pageLimit = 50);
        Task<IgMedia> GetBasicIgMediaAsync(string igMediaId);
    }
}
