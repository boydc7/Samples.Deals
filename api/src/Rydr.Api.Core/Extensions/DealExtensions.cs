using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto.Enums;
using Rydr.FbSdk.Extensions;

namespace Rydr.Api.Core.Extensions;

public static class DealExtensions
{
    private static readonly int _defaultHoursRedeemed = RydrEnvironment.GetAppSetting("Deals.DefaultHoursRedeemed", 7);
    private static readonly IDialogService _dialogService = RydrEnvironment.Container.Resolve<IDialogService>();

    public static IDealService DefaultDealService { get; } = RydrEnvironment.Container.Resolve<IDealService>();
    public static IDealRequestService DefaultDealRequestService { get; } = RydrEnvironment.Container.Resolve<IDealRequestService>();

    public static async Task<DynDialog> TryGetDealRequestDialogAsync(this DynDealRequest dynDealRequest)
    {
        var dynDealRequestDialog = await _dialogService.TryGetDynDialogAsync(new[]
                                                                             {
                                                                                 dynDealRequest.DealId, dynDealRequest.PublisherAccountId, dynDealRequest.DealPublisherAccountId
                                                                             });

        return dynDealRequestDialog;
    }

    public static async Task<bool> TryRequestDealAsync(this IDealRequestService dealRequestService, long dealId, long forPublisherAccountId,
                                                       bool fromInvite = false, int hoursAllowedInProgress = 0, int hoursAllowedRedeemed = 0)
    {
        try
        {
            await dealRequestService.RequestDealAsync(dealId, forPublisherAccountId, fromInvite, hoursAllowedInProgress, hoursAllowedRedeemed);

            return true;
        }
        catch(OperationCannotBeCompletedException)
        {
            return false;
        }
    }

    public static string ToDealPublicLinkId(this DynDeal source, string additionalInput = null)
        => string.Concat(additionalInput,
                         RecordType.DealLink, "|-To_Deal_Public_Link_Id-|jXAt2-+7h~hqEi*^546{sXP:kdW:yZd+4dw==|",
                         source.DealId, "|", source.PublisherAccountId, "|", source.CreatedOn.ToIso8601Utc(), "|",
                         source.CreatedBy, "|", source.CreatedWorkspaceId, "|", DynItemType.Deal)
                 .ToSafeSha64();

    public static bool IsExpired(this DynDeal deal)
        => deal.ExpirationDate >= DateTimeHelper.MinApplicationDate &&
           deal.ExpirationDate <= DateTimeHelper.UtcNow;

    public static bool IsDelinquent(this DynDealRequest source, bool ignoreTimeConstraint = false)
    {
        if (source.RequestStatus == DealRequestStatus.Delinquent)
        {
            return true;
        }

        if (source.RequestStatus != DealRequestStatus.Redeemed)
        {
            return false;
        }

        return ignoreTimeConstraint || source.DelinquentOn < DateTimeHelper.UtcNow;
    }
}
