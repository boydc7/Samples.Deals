using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IDealRestrictionFilterService
{
    Task<bool> MatchesAsync(IEnumerable<DealRestriction> dealRestrictions, DynPublisherAccount forPublisherAccount = null,
                            DynPublisherAccount workspacePublisherAccount = null);
}

public interface IDealRestrictionTypeFilter
{
    bool Matches(string filterValue, DynPublisherAccount forPublisherAccount, DynPublisherAccount workspacePublisherAccount);
}
