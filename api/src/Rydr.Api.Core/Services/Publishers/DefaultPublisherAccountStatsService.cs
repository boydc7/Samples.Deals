using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Services.Publishers
{
    public class DefaultPublisherAccountStatsService : IPublisherAccountStatsService
    {
        private static readonly string _publisherAccountDealStatEdgePrefix = string.Concat((int)DynItemType.PublisherAccountStat, "|", DynItemType.DealStat.ToString(), "|");
        private static readonly Func<ILocalRequestCacheClient> _localRequestCacheClientFactory = () => RydrEnvironment.Container.Resolve<ILocalRequestCacheClient>();

        private readonly IPocoDynamo _dynamoDb;

        public DefaultPublisherAccountStatsService(IPocoDynamo dynamoDb)
        {
            _dynamoDb = dynamoDb;
        }

        public static string[] DealTypeRefPublishedPausedBetwenMinMax { get; } =
            {
                string.Concat((int)DynItemType.Deal, "|1500000000"), string.Concat((int)DynItemType.Deal, "|3000000000")
            };

        public async Task<Dictionary<long, List<DealStat>>> GetPublisherAccountsStats(IEnumerable<long> publisherAccountIds, long inContextWorkspaceId)
        {
            // For an influencer and/or personal workspaces, they are not bound by workspaces
            // Others are, so get items based on a specific workspace...
            var edgePrefix = string.Concat(_publisherAccountDealStatEdgePrefix, inContextWorkspaceId.ToStringInvariant(), "|");

            var allDealStats = await _dynamoDb.GetItemsAsync<DynPublisherAccountStat>(publisherAccountIds.SelectMany(pid => DealEnumHelpers.AllDealStatTypeStrings
                                                                                                                                           .Select(sts => new DynamoId(pid,
                                                                                                                                                                       string.Concat(edgePrefix, "|", sts)))))
                                              .ToDictionaryManySafe(ds => ds.PublisherAccountId,
                                                                    ds => ds.ToDealStat());

            return allDealStats;
        }

        public async Task<List<DealStat>> GetPublisherAccountStats(long publisherAccountId, long inContextWorkspaceId = long.MinValue)
        {
            // For an influencer and/or personal workspaces, they are not bound by workspaces
            // Others are, so get items based on a specific workspace...
            var edgePrefix = string.Concat(_publisherAccountDealStatEdgePrefix,
                                           inContextWorkspaceId >= 0
                                               ? inContextWorkspaceId.ToStringInvariant()
                                               : string.Empty,
                                           inContextWorkspaceId >= 0
                                               ? "|"
                                               : string.Empty);

            var dealStats = await _dynamoDb.FromQuery<DynPublisherAccountStat>(d => d.Id == publisherAccountId &&
                                                                                    Dynamo.BeginsWith(d.EdgeId, edgePrefix))
                                           .Filter(d => d.TypeId == (int)DynItemType.PublisherAccountStat &&
                                                        d.StatType != (int)DealStatType.Unknown)
                                           .ExecAsync()
                                           .Select(i => i.ToDealStat())
                                           .Take(1000)
                                           .ToList(100);

            return dealStats;
        }

        public async Task<long> GetPublishedPausedDealCountAsync(long publisherAccountId, long inContextWorkspaceId = long.MinValue)
        {
            var countIdItem = await _localRequestCacheClientFactory().TryGetAsync(string.Concat(nameof(GetPublishedPausedDealCountAsync), "|", publisherAccountId, "|", inContextWorkspaceId),
                                                                                  () => _dynamoDb.FromQueryIndex<DynItemIdTypeReferenceGlobalIndex>(i => i.Id == publisherAccountId &&
                                                                                                                                                         Dynamo.Between(i.TypeReference,
                                                                                                                                                                        DealTypeRefPublishedPausedBetwenMinMax[0],
                                                                                                                                                                        DealTypeRefPublishedPausedBetwenMinMax[1]))
                                                                                                 .Filter(i => i.DeletedOnUtc == null &&
                                                                                                              Dynamo.In(i.StatusId, DealEnumHelpers.PublishedPausedDealStatuses))
                                                                                                 .Select(i => new
                                                                                                              {
                                                                                                                  i.Id,
                                                                                                                  i.WorkspaceId
                                                                                                              })
                                                                                                 .ExecAsync()
                                                                                                 .Where(i => inContextWorkspaceId < 0 ||
                                                                                                             i.GetContextWorkspaceId() == inContextWorkspaceId)
                                                                                                 .Take(450.ToDynamoBatchCeilingTake())
                                                                                                 .CountAsync()
                                                                                                 .AsTask()
                                                                                                 .Then(c => Int64Id.FromValue(c)),
                                                                                  CacheConfig.LongConfig);

            return countIdItem.Id;
        }
    }
}
