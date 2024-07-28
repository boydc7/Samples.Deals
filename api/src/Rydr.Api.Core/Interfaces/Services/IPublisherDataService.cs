using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Publishers;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IPublisherDataService
{
    Task<DynPublisherApp> GetDefaultPublisherAppAsync();
    Task<List<PublisherMedia>> GetRecentMediaAsync(long tokenPublisherAccountId, long publisherAccountId, long publisherAppId = 0, int limit = 50);
    Task<DynPublisherApp> GetPublisherAppOrDefaultAsync(long publisherAppId, AccessIntent accessIntent = AccessIntent.Unspecified, long workspaceId = 0);
    Task PutAccessTokenAsync(long publisherAccountId, string accessToken, int expiresIn = 0, long workspaceId = 0, long publisherAppId = 0);
}
