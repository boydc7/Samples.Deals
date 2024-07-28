using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IPublisherAccountStatsService
{
    Task<List<DealStat>> GetPublisherAccountStats(long publisherAccountId, long inContextWorkspaceId = long.MinValue);
    Task<long> GetPublishedPausedDealCountAsync(long publisherAccountId, long inContextWorkspaceId = long.MinValue);
    Task<Dictionary<long, List<DealStat>>> GetPublisherAccountsStats(IEnumerable<long> publisherAccountIds, long inContextWorkspaceId = long.MinValue);
}
