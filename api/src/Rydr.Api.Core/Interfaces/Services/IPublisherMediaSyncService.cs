using Rydr.Api.Core.Models.Supporting;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IPublisherMediaSyncService
{
    Task<List<long>> SyncMediaAsync(IEnumerable<string> publisherMediaIds, long publisherAccountId,
                                    bool isCompletionMedia = false, long publisherAppId = 0);

    Task SyncRecentMediaAsync(SyncPublisherAppAccountInfo appAccount);
    Task SyncUserDataAsync(SyncPublisherAppAccountInfo appAccount);

    Task AddOrUpdateMediaSyncAsync(long publisherAccountId);
}
