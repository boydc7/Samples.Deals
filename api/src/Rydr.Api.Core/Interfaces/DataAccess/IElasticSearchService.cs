using System.Threading.Tasks;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto.Publishers;

namespace Rydr.Api.Core.Interfaces.DataAccess
{
    public interface IElasticSearchService
    {
        Task<EsSearchResult<EsBusiness>> SearchBusinessIdsAsync(EsBusinessSearch businessSearch);
        Task<EsSearchResult<EsCreator>> SearchCreatorIdsAsync(EsCreatorSearch creatorSearch);
        Task<EsSearchResult<EsCreatorStatAggregate>> SearchAggregateCreatorsAsync(EsCreatorSearch creatorSearch);
        Task<EsSearchResult<EsDeal>> SearchDealsAsync(EsDealSearch publishedDealSearch);
        Task<EsSearchResult<EsMedia>> SearchMediaAsync(PublisherAccountMediaVisionSectionSearchDescriptor request);
    }
}
