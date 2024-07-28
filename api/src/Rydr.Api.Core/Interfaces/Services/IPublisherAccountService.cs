using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IPublisherAccountService
{
    void FlushModel(long id);

    Task<DynPublisherAccount> TryGetPublisherAccountAsync(long publisherAccountId, bool retryDelayedOnNotFound = false);
    Task<DynPublisherAccount> TryGetPublisherAccountAsync(PublisherType publisherType, string publisherId);
    Task<DynPublisherAccount> TryGetPublisherAccountByUserNameAsync(PublisherType publisherType, string userName);
    Task<DynPublisherAccount> GetPublisherAccountAsync(long publisherAccountId, bool retryDelayedOnNotFound = false);

    IEnumerable<DynPublisherAccount> GetPublisherAccounts(IEnumerable<long> publisherAccountIds);
    IAsyncEnumerable<DynPublisherAccount> GetPublisherAccountsAsync(IEnumerable<long> publisherAccountIds);
    IAsyncEnumerable<DynPublisherAccount> GetPublisherAccountsAsync(IAsyncEnumerable<long> publisherAccountIds);

    Task<bool> HardDeletePublisherAccountForReplacementOnlyAsync(long publisherAccountId);

    Task PutAccessTokenAsync(long publisherAccountId, string accessToken, PublisherType publisherType, long workspaceId = 0);

    Task PutPublisherAccount(DynPublisherAccount newPublisherAccount);

    Task<DynPublisherAccount> UpdatePublisherAccountAsync<T>(T fromRequest)
        where T : RequestBase, IHaveModel<PublisherAccount>;

    Task<DynPublisherAccount> UpdatePublisherAccountAsync(DynPublisherAccount toPublisherAccount, Action<DynPublisherAccount> updateAccountBlock);

    Task<DynPublisherAccount> ConnectPublisherAccountAsync(PublisherAccount publisherAccount, long workspaceId = 0);

    IAsyncEnumerable<DynPublisherAccount> GetLinkedPublisherAccountsAsync(long publisherAccountId);
}
