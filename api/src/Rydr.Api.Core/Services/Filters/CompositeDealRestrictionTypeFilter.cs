using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using ServiceStack;

// ReSharper disable NotResolvedInText

namespace Rydr.Api.Core.Services.Filters;

public class CompositeDealRestrictionTypeFilter : IDealRestrictionFilterService
{
    private readonly IRequestStateManager _requestStateManager;

    private readonly Dictionary<DealRestrictionType, IDealRestrictionTypeFilter> _dealRestrictionTypeFilters =
        new()
        {
            {
                DealRestrictionType.Unknown, NullDealRestrictionTypeFilter.Instance
            },
            {
                DealRestrictionType.MinFollowerCount, MinFollowerCountDealRestrictionTypeFilter.Instance
            },
            {
                DealRestrictionType.MinEngagementRating, MinEngagementRatingDealRestrictionTypeFilter.Instance
            },
            {
                DealRestrictionType.MinAge, MinAgeDealRestrictionTypeFilter.Instance
            }
        };

    public CompositeDealRestrictionTypeFilter(IRequestStateManager requestStateManager)
    {
        _requestStateManager = requestStateManager;
    }

    public async Task<bool> MatchesAsync(IEnumerable<DealRestriction> dealRestrictions, DynPublisherAccount forPublisherAccount = null,
                                         DynPublisherAccount workspacePublisherAccount = null)
    {
        if (dealRestrictions == null)
        {
            return true;
        }

        var state = forPublisherAccount == null || workspacePublisherAccount == null
                        ? _requestStateManager.GetState()
                        : null;

        if (forPublisherAccount == null)
        {
            forPublisherAccount = await PublisherExtensions.DefaultPublisherAccountService.TryGetPublisherAccountAsync(state.RequestPublisherAccountId);
        }

        if (forPublisherAccount == null || forPublisherAccount.IsDeleted())
        {
            return false;
        }

        if (workspacePublisherAccount == null)
        {
            workspacePublisherAccount = await WorkspaceService.DefaultWorkspaceService
                                                              .TryGetDefaultPublisherAccountAsync(state.WorkspaceId);
        }

        foreach (var dealRestriction in dealRestrictions)
        {
            if (!_dealRestrictionTypeFilters.TryGetValue(dealRestriction.Type, out var filter))
            {
                throw new ArgumentOutOfRangeException("DealRestrictionType", dealRestriction.Type, "Unmapped DealRestrictionType in DealRestrictionFilterService");
            }

            if (!filter.Matches(dealRestriction.Value, forPublisherAccount, workspacePublisherAccount))
            {
                return false;
            }
        }

        return true;
    }
}

internal class NullDealRestrictionTypeFilter : IDealRestrictionTypeFilter
{
    private NullDealRestrictionTypeFilter() { }

    public static NullDealRestrictionTypeFilter Instance { get; } = new();

    public bool Matches(string filterValue, DynPublisherAccount forPublisherAccount, DynPublisherAccount workspacePublisherAccount) => true;
}

internal class MinFollowerCountDealRestrictionTypeFilter : PublisherMetricGreaterThanOrEqualToTypeFilter
{
    private MinFollowerCountDealRestrictionTypeFilter() { }

    public static MinFollowerCountDealRestrictionTypeFilter Instance { get; } = new();

    protected override IEnumerable<string> PublisherMetricNames
    {
        get { yield return PublisherMetricName.FollowedBy; }
    }
}

internal class MinEngagementRatingDealRestrictionTypeFilter : PublisherMetricGreaterThanOrEqualToTypeFilter
{
    private static readonly bool _honorEngagementRestrictions = RydrEnvironment.GetAppSetting("DealRestrictions.HonorEngagementFilter", true);

    private MinEngagementRatingDealRestrictionTypeFilter() { }

    public static MinEngagementRatingDealRestrictionTypeFilter Instance { get; } = new();

    protected override IEnumerable<string> PublisherMetricNames
    {
        get
        {
            yield return PublisherMetricName.RecentEngagementRating;
            yield return PublisherMetricName.RecentTrueEngagementRating;
            yield return PublisherMetricName.StoryEngagementRating;
        }
    }

    public override bool Matches(string filterValue, DynPublisherAccount forPublisherAccount, DynPublisherAccount workspacePublisherAccount)
        => !_honorEngagementRestrictions || base.Matches(filterValue, forPublisherAccount, workspacePublisherAccount);
}

internal class MinAgeDealRestrictionTypeFilter : IDealRestrictionTypeFilter
{
    private static readonly bool _honorAgeRestrictions = RydrEnvironment.GetAppSetting("DealRestrictions.HonorAgeFilter", false);

    private static readonly IRequestStateManager _requestStateManager = RydrEnvironment.Container.Resolve<IRequestStateManager>();

    private MinAgeDealRestrictionTypeFilter() { }

    public static MinAgeDealRestrictionTypeFilter Instance { get; } = new();

    public bool Matches(string filterValue, DynPublisherAccount forPublisherAccount, DynPublisherAccount workspacePublisherAccount)
    {
        if (!_honorAgeRestrictions)
        {
            return true;
        }

        var intFilterValue = filterValue.ToInteger();

        if (intFilterValue <= 0)
        {
            return true;
        }

        if ((forPublisherAccount.AgeRangeMin > 0 && forPublisherAccount.AgeRangeMin >= intFilterValue) ||
            (workspacePublisherAccount != null && workspacePublisherAccount.AgeRangeMin >= intFilterValue))
        {
            return true;
        }

        var state = _requestStateManager.GetState();

        if (state.IsSystemRequest || state.UserType == UserType.Admin)
        {
            return true;
        }

        return false;
    }
}

internal abstract class PublisherMetricGreaterThanOrEqualToTypeFilter : IDealRestrictionTypeFilter
{
    protected abstract IEnumerable<string> PublisherMetricNames { get; }

    public virtual bool Matches(string filterValue, DynPublisherAccount forPublisherAccount, DynPublisherAccount workspacePublisherAccount)
    {
        var longFilterValue = filterValue.ToLong(long.MinValue);

        if (longFilterValue <= 0)
        {
            return true;
        }

        if (forPublisherAccount.Metrics.IsNullOrEmptyRydr())
        {
            return false;
        }

        foreach (var publisherMetricName in PublisherMetricNames)
        {
            if (forPublisherAccount.Metrics.TryGetValue(publisherMetricName, out var publisherAccountMetricValue))
            {
                if (publisherAccountMetricValue >= longFilterValue)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
