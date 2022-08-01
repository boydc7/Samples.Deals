using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Supporting;

namespace Rydr.Api.Core.Services.Publishers
{
    public class NullPublisherMediaService : IPublisherMediaSyncService
    {
        private static readonly Task<List<long>> _emptyMediaTask = Task.FromResult(new List<long>());

        private NullPublisherMediaService() { }

        public static NullPublisherMediaService Instance { get; } = new NullPublisherMediaService();

        public Task<List<long>> SyncMediaAsync(IEnumerable<string> publisherMediaIds, long publisherAccountId, bool isCompletionMedia = false, long publisherAppId = 0)
            => _emptyMediaTask;

        public Task SyncRecentMediaAsync(SyncPublisherAppAccountInfo appAccount)
            => Task.CompletedTask;

        public Task SyncUserDataAsync(SyncPublisherAppAccountInfo appAccount)
            => Task.CompletedTask;

        public Task AddOrUpdateMediaSyncAsync(long publisherAccountId)
            => Task.CompletedTask;
    }
}
