using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Interfaces.Services;

public interface IDealRequestService
{
    Task<bool> CanBeRequestedAsync(long dealId, long byPublisherAccountId, IHasUserAuthorizationInfo withState = null);

    Task<bool> CanBeRequestedAsync(DynDeal deal, long byPublisherAccountId, HashSet<long> knownRequestedDealIds = null,
                                   bool isExistingBeingApproved = false, bool readOnlyIntent = false, IHasUserAuthorizationInfo withState = null);

    Task<bool> CanBeApprovedAsync(DynDealRequest dynDealRequest, bool forceAllowUncancel = false);
    Task<bool> CanBeRedeemedAsync(DynDealRequest dynDealRequest);
    Task<bool> CanBeCompletedAsync(DynDealRequest dynDealRequest);
    Task<bool> CanBeDeniedAsync(DynDealRequest dynDealRequest);
    Task<bool> CanBeCancelledAsync(DynDealRequest dynDealRequest);
    Task<bool> CanBeCancelledAsync(long dealId, long publisherAccountId);
    Task<bool> CanBeDelinquentAsync(DynDealRequest dynDealRequest, bool ignoreTimeConstraint = false);

    Task<DynDealRequest> GetDealRequestAsync(long dealId, long publisherAccountId);
    Task<DealRequestExtended> GetDealRequestExtendedAsync(long dealId, long publisherAccountId);
    Task<List<DynDealRequest>> GetDealRequestsAsync(IEnumerable<long> forDealIds, long publisherAccountId);
    Task<List<DynDealRequest>> GetDealRequestsAsync(long dealId, IEnumerable<long> publisherAccountIds);
    Task<List<DynDealRequest>> GetAllActiveDealRequestsAsync(long dealId);
    Task<List<string>> GetActiveDealGroupIdsAsync(long forPublisherAccountId);
    IAsyncEnumerable<DynDealRequest> GetPublisherAccountRequestsAsync(long publisherAccountId, IEnumerable<DealRequestStatus> statuses = null);

    IAsyncEnumerable<DealResponse> GetDealResponseRequestExtendedAsync(ICollection<DynDealRequest> dynDealRequests);

    Task RequestDealAsync(long dealId, long forPublisherAccountId, bool fromInvite, int hoursAllowedInProgress, int hoursAllowedRedeemed);

    Task UpdateDealRequestAsync(long dealId, long publisherAccountId, DealRequestStatus toStatus, int hoursAllowedInProgress,
                                int hoursAllowedRedeemed, bool forceAllowUncancel = false);

    IAsyncEnumerable<DynDealRequest> GetDealOwnerRequestsEverInStatusAsync(long dealOwnerPublisherAccountId, DealRequestStatus status,
                                                                           DateTime statusChangeStart, DateTime statusChangeEnd, long inWorkspaceId = 0);
}
