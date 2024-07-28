using Nest;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Es;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.OrmLite.Dapper;

namespace Rydr.Api.Core.Transforms;

public static class DealTransforms
{
    private static readonly int _defaultHoursInProgress = RydrEnvironment.GetAppSetting("Deals.DefaultHoursInProgress", 7);
    private static readonly int _defaultHoursRedeemed = RydrEnvironment.GetAppSetting("Deals.DefaultHoursRedeemed", 7);
    private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();
    private static readonly IDialogCountService _dialogCountService = RydrEnvironment.Container.Resolve<IDialogCountService>();
    private static readonly IRequestStateManager _requestStateManager = RydrEnvironment.Container.Resolve<IRequestStateManager>();

    public static async Task<EsDeal> ToEsDealAsync(this DynDeal source)
    {
        var (latitude, longitude) = await GetDealLocationLatLonAsync(source);

        // For purposes of search/etc., we round to 4 decimal points on locations - this does not impact what is used for displaying distance
        // to the users, as we get that from Dynamo anyhow, but for calc distance purposes and sorting, we stop at 4
        var location = Math.Abs(latitude) > 0 || Math.Abs(longitude) > 0
                           ? GeoLocation.TryCreate(Math.Round(latitude, 4), Math.Round(longitude, 4))
                           : null;

        var minFollowerCount = 0L;
        var minEngagementRating = 0D;
        var minAge = 0;

        if (!source.Restrictions.IsNullOrEmpty())
        {
            foreach (var restriction in source.Restrictions)
            {
                switch (restriction.Type)
                {
                    case DealRestrictionType.MinEngagementRating:
                        minEngagementRating = restriction.Value.ToDoubleRydr().MinGz(0);

                        break;

                    case DealRestrictionType.MinFollowerCount:
                        minFollowerCount = restriction.Value.ToLong(0).MinGz(0);

                        break;

                    case DealRestrictionType.MinAge:
                        minAge = restriction.Value.ToInteger().Gz(0);

                        break;

                    case DealRestrictionType.Unknown:
                        break;

                    default:

                        // ReSharper disable once NotResolvedInText
                        throw new ArgumentOutOfRangeException("deal.restriction.type");
                }
            }
        }

        // List of publishers who have requested the given deal
        // Union those with any invites where the invite request is no longer in an invited status
        var requestedByPublisherAccountIds = await _dynamoDb.FromQuery<DynDealRequest>(dr => dr.Id == source.DealId &&
                                                                                             Dynamo.BeginsWith(dr.EdgeId, "00"))
                                                            .Filter(dr => dr.TypeId == (int)DynItemType.DealRequest &&
                                                                          dr.DeletedOnUtc == null &&
                                                                          dr.StatusId != DealRequestStatus.Invited.ToString())
                                                            .Select(dr => new
                                                                          {
                                                                              dr.EdgeId
                                                                          })
                                                            .QueryAsync(_dynamoDb)
                                                            .Select(dr => dr.EdgeId.ToLong(0))
                                                            .Where(i => i > 0)
                                                            .Take(7500)
                                                            .ToHashSet();

        if (!source.InvitedPublisherAccountIds.IsNullOrEmpty())
        {
            var invitedDealRequestIds = await _dynamoDb.QueryItemsAsync<DynDealRequest>(source.InvitedPublisherAccountIds
                                                                                              .Select(i => new DynamoId(source.DealId, i.ToEdgeId())))
                                                       .Where(dr => dr.RequestStatus != DealRequestStatus.Invited &&
                                                                    dr.DeletedOnUtc == null)
                                                       .Select(dr => dr.PublisherAccountId)
                                                       .ToHashSet();

            requestedByPublisherAccountIds.UnionWith(invitedDealRequestIds);
        }

        var dealPublsiherAccount = await PublisherExtensions.DefaultPublisherAccountService.GetPublisherAccountAsync(source.PublisherAccountId);

        var totalApproved = await DealExtensions.DefaultDealService.GetDealStatAsync(source.DealId, source.PublisherAccountId, DealStatType.TotalApproved);

        var esDeal = new EsDeal
                     {
                         IsDeleted = source.IsDeleted(),
                         PublisherAccountId = source.PublisherAccountId,
                         WorkspaceId = source.WorkspaceId,
                         ContextWorkspaceId = source.DealContextWorkspaceId,
                         OwnerId = source.OwnerId,
                         DealId = source.DealId,
                         Location = location,
                         Value = source.Value,
                         PlaceId = source.PlaceId,
                         MinFollowerCount = minFollowerCount,
                         MinEngagementRating = minEngagementRating,
                         MinAge = minAge,
                         DealStatus = (int)source.DealStatus,
                         DealType = ((int)source.DealType).Gz(1),
                         InvitedPublisherAccountIds = source.InvitedPublisherAccountIds.IsNullOrEmpty()
                                                          ? null
                                                          : source.InvitedPublisherAccountIds.AsList(),
                         IsPrivateDeal = source.IsPrivateDeal,
                         ExpiresOn = source.ExpirationDate <= DateTimeHelper.MinApplicationDate
                                         ? DateTimeHelper.MaxApplicationDateTs
                                         : source.ExpirationDate.ToUnixTimestamp(),
                         PublishedOn = source.PublishedOn?.ToUnixTimestamp() ?? 0,
                         CreatedOn = source.CreatedOnUtc,
                         GroupId = source.DealGroupId,
                         RequestedByPublisherAccountIds = requestedByPublisherAccountIds.AsList(),
                         RequestCount = requestedByPublisherAccountIds.Count,
                         RemainingQuantity = (source.ApprovalLimit - (int)totalApproved.Cnt.GetValueOrDefault()).Gz(0),
                         Tags = source.Tags.IsNullOrEmpty()
                                    ? null
                                    : source.Tags.Select(t => t.ToString()).AsList(),
                         SearchValue = string.Concat(source.DealId,
                                                     " ",
                                                     dealPublsiherAccount.UserName.ToLowerInvariant(),
                                                     " ",
                                                     dealPublsiherAccount.PublisherAccountId,
                                                     " ",
                                                     source.Title.Left(500))
                     };

        return esDeal;
    }

    public static async Task<DynPlace> GetDealLocationAsync(this DynDeal source)
    {
        if (source.ReceivePlaceId <= 0 && source.PlaceId <= 0)
        {
            return null;
        }

        var receivePlace = source.ReceivePlaceId > 0
                               ? await _dynamoDb.TryGetPlaceAsync(source.ReceivePlaceId)
                               : null;

        if (receivePlace != null && receivePlace.Address.IsValidLatLon())
        {
            return receivePlace;
        }

        var place = source.PlaceId > 0
                        ? await _dynamoDb.TryGetPlaceAsync(source.PlaceId)
                        : null;

        if (place != null && place.Address.IsValidLatLon())
        {
            return place;
        }

        return null;
    }

    public static async Task<(double Latitude, double Longitude)> GetDealLocationLatLonAsync(this DynDeal source)
    {
        if (source.ReceivePlaceId <= 0 && source.PlaceId <= 0)
        {
            return (0, 0);
        }

        var receivePlace = source.ReceivePlaceId > 0
                               ? await _dynamoDb.TryGetPlaceAsync(source.ReceivePlaceId)
                               : null;

        var place = source.PlaceId > 0
                        ? await _dynamoDb.TryGetPlaceAsync(source.PlaceId)
                        : null;

        return GetLatLonFrom(receivePlace, place);
    }

    private static (double Latitude, double Longitude) GetLatLonFrom(params IHaveAddress[] from)
    {
        var location = GetFirstValidLocation(from);

        return location == null
                   ? (0, 0)
                   : (location.Address.Latitude.Value, location.Address.Longitude.Value);
    }

    private static T GetFirstValidLocation<T>(params T[] from)
        where T : class, IHaveAddress
        => from.IsNullOrEmptyRydr()
               ? null
               : from.FirstOrDefault(f => f?.Address != null &&
                                          f.Address.IsValidLatLon());

    public static DealStat ToDealStat(this DynDealStat source)
        => new()
           {
               Type = source.StatType,
               Value = source.Value.Coalesce(source.Cnt.Gz(0).ToStringInvariant())
           };

    public static DealStat ToDealStat(this DynPublisherAccountStat source)
        => new()
           {
               Type = source.StatType,
               Value = source.Value.Coalesce(source.Cnt.Gz(0).ToStringInvariant())
           };

    public static RydrDealTextValue ToRydrDealTextValue(this DynDeal source)
    {
        string typeListToString<T>(ICollection<T> typeSource, Func<T, string> selector)
        {
            if (typeSource.IsNullOrEmptyRydr())
            {
                return null;
            }

            return string.Join(',', typeSource.Select(selector))
                         .Left(500)
                         .ToNullIfEmpty();
        }

        var result = new RydrDealTextValue
                     {
                         Id = source.DealId,
                         DealGroupId = source.DealGroupId.ToNullIfEmpty(),
                         Title = source.Title.ToNullIfEmpty(),
                         Description = source.Description.ToString().Left(500).ToNullIfEmpty(),
                         ApprovalNotes = source.ApprovalNotes.ToString().Left(500).ToNullIfEmpty(),
                         ReceiveNotes = source.ReceiveNotes.ToString().Left(500).ToNullIfEmpty(),
                         Restrictions = typeListToString(source.Restrictions, r => string.Concat(r.Type.ToString(), ":", r.Value.Left(25))),
                         Tags = typeListToString(source.Tags, t => t.ToString()),
                         ReceiveTypes = typeListToString(source.ReceiveType, r => string.Concat(r.Type.ToString(), ":", r.Quantity)),
                         MetaData = typeListToString(source.MetaData, r => string.Concat(r.Type.ToString(), ":", r.Value.Left(25))),
                     };

        return result;
    }

    public static IEnumerable<RydrDeal> ToRydrDeal(this DynDeal source)
    {
        var result = source.ConvertTo<RydrDeal>();

        result.DealType = source.DealType == DealType.Unknown
                              ? DealType.Deal
                              : source.DealType;

        result.Id = source.DealId;
        result.PublisherAccountId = source.PublisherAccountId;
        result.Status = source.DealStatus;

        yield return result;
    }

    public static IEnumerable<RydrDealRequest> ToRydrDealRequest(this DynDealRequest source)
    {
        var statusUpdatedOn = source.ReferenceId.ToLong(0).ToDateTime();

        DateTime? completedOn = null;

        if (source.RequestStatus.IsAfterRedeemed())
        {
            if (source.RequestStatus == DealRequestStatus.Completed)
            {
                completedOn = statusUpdatedOn;
            }
            else
            {
                var completedStatusChange = _dynamoDb.GetItem<DynDealRequestStatusChange>(source.DealId, DynDealRequestStatusChange.BuildEdgeId(DealRequestStatus.Completed, source.PublisherAccountId));

                var completionOccurredOn = completedStatusChange?.OccurredOn ?? 0;

                if (completionOccurredOn > DateTimeHelper.MinApplicationDateTs)
                {
                    completedOn = completionOccurredOn.ToDateTime();
                }
            }
        }

        return new RydrDealRequest
               {
                   DealId = source.DealId,
                   PublisherAccountId = source.PublisherAccountId,
                   Status = source.RequestStatus,
                   StatusUpdatedOn = statusUpdatedOn,
                   CompletedOn = completedOn,
                   HoursAllowedRedeemed = source.HoursAllowedRedeemed,
                   HoursAllowedInProgress = source.HoursAllowedInProgress,
                   RescindOn = source.RequestStatus == DealRequestStatus.InProgress
                                   ? statusUpdatedOn.AddHours(source.HoursAllowedInProgress)
                                   : null,
                   DelinquentOn = source.RequestStatus == DealRequestStatus.Redeemed
                                      ? statusUpdatedOn.AddHours(source.HoursAllowedRedeemed)
                                      : null,
                   DealContextWorkspaceId = source.DealContextWorkspaceId,
                   UsageChargedOn = source.UsageChargedOn > DateTimeHelper.MinApplicationDateTs
                                        ? source.UsageChargedOn.ToDateTime()
                                        : null,
                   RequestedOn = source.CreatedOn
               }.AsEnumerable();
    }

    public static void PopulateApprovalsRemaining(this DealResponse dealResponse)
    {
        if ((dealResponse?.Stats).IsNullOrEmpty() || dealResponse.Deal?.MaxApprovals == null || dealResponse.Deal.MaxApprovals.Value <= 0)
        { // Null indicates unlimitted basically
            dealResponse.ApprovalsRemaining = null;

            return;
        }

        var totalApproved = dealResponse.Stats.FirstOrDefault(s => s.Type == DealStatType.TotalApproved)?.Value.ToLong(0) ?? 0;

        PopulateApprovalsRemaining(dealResponse, totalApproved);
    }

    public static void PopulateApprovalsRemaining(this DealResponse dealResponse, long existingTotalApproved)
    {
        if (dealResponse?.Deal?.MaxApprovals == null || dealResponse.Deal.MaxApprovals.Value <= 0)
        { // Null indicates unlimitted basically
            dealResponse.ApprovalsRemaining = null;

            return;
        }

        dealResponse.ApprovalsRemaining = (dealResponse.Deal.MaxApprovals.Value - existingTotalApproved).Gz(0);
    }

    public static void ScrubDeal(this DealResponse dealResponse, DealRequest dealRequest = null)
    {
        if (dealRequest == null)
        {
            dealRequest = dealResponse.DealRequest;
        }

        // Invites are never returned anymore...should already be null, but leaving this here anyhow...
        dealResponse.Deal.InvitedPublisherAccounts = null;

        if (dealResponse.Deal.IsPrivateDeal.HasValue && dealResponse.Deal.IsPrivateDeal.Value &&
            !dealResponse.Deal.Tags.IsNullOrEmpty() && dealResponse.Deal.Tags.Any(t => t.Value.EqualsOrdinalCi(Tag.TagRydrInternalDeal)))
        { // Internal deal being treated as private for prod-test purposes, remove the invite info from the response so the deal is treated as
            // a non-private deal for in-app purposes
            dealResponse.Deal.IsPrivateDeal = false;
        }

        if (dealRequest != null &&
            (dealResponse.PublisherAccountId == dealRequest.PublisherAccountId ||
             dealResponse.Deal.CreatedWorkspaceId == dealRequest.CreatedWorkspaceId))
        {
            return;
        }

        var state = _requestStateManager.GetState();

        if (state.UserId > 0 && (state.UserId == dealResponse.Deal.CreatedBy ||
                                 state.WorkspaceId == dealResponse.Deal.CreatedWorkspaceId ||
                                 state.IsSystemRequest ||
                                 state.RequestPublisherAccountId == dealResponse.PublisherAccountId))
        {
            return;
        }

        // Remove approval notes from the deal if the request is null/unknown, if the status is cancelled,
        // or if the status on the request hasn't reached an inProgress state yet
        if (dealRequest == null ||
            dealResponse.Deal.DeletedOn.HasValue ||
            dealRequest.DeletedOn.HasValue ||
            dealRequest.Status == DealRequestStatus.Cancelled ||
            dealRequest.Status.IsBeforeInProgress())
        {
            dealResponse.Deal.ApprovalNotes = null;
            dealResponse.PendingRecentRequesters = null;

            // If the current user is one of the invitees, we can return some of the info, otherwise, blank it out as well
            if (!dealResponse.Deal.InvitedPublisherAccounts.IsNullOrEmpty())
            {
                dealResponse.Deal.InvitedPublisherAccounts.RemoveAll(pa => pa.Id != state.RequestPublisherAccountId);
            }

            if (dealResponse.Deal.InvitedPublisherAccounts.IsNullOrEmpty())
            {
                dealResponse.Deal.InvitedPublisherAccounts = null;
                dealResponse.Stats = null;
            }
        }
    }

    public static async Task<Deal> ToDealAsync(this DynDeal dynDeal)
    {
        var result = ToDeal(dynDeal, null, null, null, null);

        await DecorateAsyncDealAsync(result, dynDeal);

        return result;
    }

    public static Deal ToDeal(this DynDeal source, Dictionary<long, PublisherMedia> publisherMediaMap,
                              Dictionary<long, Place> placeMap, Dictionary<long, Hashtag> hashtagMap,
                              Dictionary<long, PublisherAccount> publisherMap)
    {
        if (source == null)
        {
            throw new RecordNotFoundException();
        }

        var result = source.ConvertTo<Deal>();

        result.Id = source.DealId;
        result.PublisherAccountId = source.PublisherAccountId;
        result.DealWorkspaceId = source.WorkspaceId;
        result.Restrictions = source.Restrictions;
        result.AutoApproveRequests = source.AutoApproveRequests;
        result.Status = source.DealStatus;
        result.Description = source.Description;
        result.ApprovalNotes = source.ApprovalNotes;
        result.ReceiveNotes = source.ReceiveNotes;
        result.IsPrivateDeal = source.IsPrivateDeal;
        result.PublisherApprovedMediaIds = source.PublisherApprovedMediaIds.AsList();

        if (source.DealType == DealType.Unknown)
        {
            result.DealType = DealType.Deal;
        }

        result.ExpirationDate = source.ExpirationDate >= DateTimeHelper.MaxApplicationDate
                                    ? null
                                    : source.ExpirationDate;

        if (!source.ReceivePublisherAccountIds.IsNullOrEmpty())
        {
            result.ReceivePublisherAccounts = publisherMap == null
                                                  ? null
                                                  : source.ReceivePublisherAccountIds
                                                          .Select(pid => publisherMap.ContainsKey(pid)
                                                                             ? publisherMap[pid]
                                                                             : null)
                                                          .Where(t => t != null)
                                                          .AsList();
        }

        // NOTE: invited publisher accounts are purposely not returned in the deal responses...

        if (!source.ReceiveHashtagIds.IsNullOrEmpty())
        {
            result.ReceiveHashtags = hashtagMap == null
                                         ? null
                                         : source.ReceiveHashtagIds
                                                 .Select(hti => hashtagMap.ContainsKey(hti)
                                                                    ? hashtagMap[hti]
                                                                    : null)
                                                 .Where(t => t != null)
                                                 .AsList();
        }

        if (source.PlaceId > 0)
        {
            result.Place = placeMap != null && placeMap.ContainsKey(source.PlaceId)
                               ? placeMap[source.PlaceId]
                               : null;
        }

        if (source.ReceivePlaceId > 0)
        {
            result.ReceivePlace = placeMap != null && placeMap.ContainsKey(source.ReceivePlaceId)
                                      ? placeMap[source.ReceivePlaceId]
                                      : null;
        }

        if (!source.PublisherMediaIds.IsNullOrEmpty())
        {
            result.PublisherMedias = publisherMediaMap == null
                                         ? null
                                         : source.PublisherMediaIds
                                                 .Select(pmi => publisherMediaMap.ContainsKey(pmi)
                                                                    ? publisherMediaMap[pmi]
                                                                    : null)
                                                 .Where(m => m != null)
                                                 .AsList();
        }

        result.MaxApprovals = source.MaxApprovals == int.MaxValue
                                  ? 0
                                  : source.MaxApprovals;

        return result;
    }

    private static async Task DecorateAsyncDealAsync(Deal toDecorate, DynDeal decorateWith)
    {
        if (decorateWith.PlaceId > 0)
        {
            toDecorate.Place = (await _dynamoDb.TryGetPlaceAsync(decorateWith.PlaceId))?.ToPlace();
        }

        if (decorateWith.ReceivePlaceId > 0)
        {
            toDecorate.ReceivePlace = (await _dynamoDb.TryGetPlaceAsync(decorateWith.ReceivePlaceId))?.ToPlace();
        }

        if (!decorateWith.PublisherMediaIds.IsNullOrEmpty())
        {
            toDecorate.PublisherMedias = await _dynamoDb.QueryItemsAsync<DynPublisherMedia>(decorateWith.PublisherMediaIds
                                                                                                        .Select(pmid => new DynamoId(decorateWith.PublisherAccountId, pmid.ToEdgeId())))
                                                        .Where(dpm => dpm != null && !dpm.IsDeleted())
                                                        .SelectAwait(dpm => dpm.ToPublisherMediaAsyncValue())
                                                        .ToList(decorateWith.PublisherMediaIds.Count);
        }
    }

    public static async Task<DealResponse> ToDealResponseAsync(this DynDeal source, double? fromLatitude = null,
                                                               double? fromLongitude = null, long requestedBy = 0)
    {
        var response = ToDealResponse(source, null, null, null, null, fromLatitude, fromLongitude);

        await DecorateAsyncDealAsync(response.Deal, source);

        return response;
    }

    public static DealResponse ToDealResponse(this DynDeal source, Dictionary<long, PublisherMedia> publisherMediaMap,
                                              Dictionary<long, Place> placeMap, Dictionary<long, Hashtag> hashtagMap,
                                              Dictionary<long, PublisherAccount> publisherMap,
                                              double? fromLatitude = null, double? fromLongitude = null, long requestedBy = 0)
    {
        if (source == null)
        {
            throw new RecordNotFoundException();
        }

        var result = new DealResponse
                     {
                         Deal = ToDeal(source, publisherMediaMap, placeMap, hashtagMap, publisherMap),
                         UnreadMessages = _dialogCountService.GetRecordTypeUnreadCount(new RecordTypeId(RecordType.Deal, source.DealId),
                                                                                       _requestStateManager.GetState().UserId),
                         IsInvited = requestedBy > 0 &&
                                     !source.InvitedPublisherAccountIds.IsNullOrEmpty() &&
                                     source.InvitedPublisherAccountIds.Contains(requestedBy),
                         CreatedOn = source.CreatedOn,
                         PublishedOn = source.PublishedOn
                     };

        var (dealLat, dealLon) = GetLatLonFrom(result.Deal.ReceivePlace, result.Deal.Place);

        result.DistanceInMiles = GeoExtensions.DistanceBetween(fromLatitude, fromLongitude, dealLat, dealLon);

        return result;
    }

    public static DynDeal ToDynDeal(this PostDeal source)
        => ToDynDeal(source.Model);

    public static DynDeal ToDynDeal(this PutDeal source, DynDeal existingBeingUpdated)
    {
        if (existingBeingUpdated == null)
        {
            throw new RecordNotFoundException();
        }

        return ToDynDeal(source.Model, existingBeingUpdated);
    }

    public static DynDeal ToDynDeal(this Deal source, DynDeal existingBeingUpdated = null, ISequenceSource sequenceSource = null)
    {
        var to = source.ConvertTo<DynDeal>();

        if (existingBeingUpdated == null)
        { // New one
            to.Id = source.PublisherAccountId;
            to.DynItemType = DynItemType.Deal;

            to.DealType = source.DealType == DealType.Unknown
                              ? DealType.Deal
                              : source.DealType;

            to.UpdateDateTimeTrackedValues(source);

            to.DealContextWorkspaceId = to.GetContextWorkspaceId(RydrAccountType.Business);

            to.Tags = source.Tags?.Where(t => t.Value.HasValue()).AsHashSet().NullIfEmpty();
            to.MetaData = source.MetaData?.Where(d => d.Value.HasValue()).AsHashSet().NullIfEmpty();
        }
        else
        {
            to.TypeId = existingBeingUpdated.TypeId;
            to.Id = existingBeingUpdated.Id;
            to.EdgeId = existingBeingUpdated.EdgeId;
            to.ReturnedApprovals = existingBeingUpdated.ReturnedApprovals;

            to.DealType = existingBeingUpdated.DealType == DealType.Unknown
                              ? DealType.Deal
                              : source.DealType == DealType.Unknown
                                  ? existingBeingUpdated.DealType
                                  : source.DealType;

            to.UpdateDateTimeDeleteTrackedValues(existingBeingUpdated);
            to.DealContextWorkspaceId = existingBeingUpdated.DealContextWorkspaceId;

            to.Tags = (source.Tags == null
                           ? existingBeingUpdated.Tags
                           : source.Tags?.Where(t => t.Value.HasValue()).AsHashSet()).NullIfEmpty();

            to.MetaData = (source.MetaData == null
                               ? existingBeingUpdated.MetaData
                               : source.MetaData?.Where(t => t.Value.HasValue()).AsHashSet()).NullIfEmpty();
        }

        // DealId is the edge...
        if (to.DealId <= 0)
        {
            to.DealId = existingBeingUpdated != null && existingBeingUpdated.DealId > 0
                            ? existingBeingUpdated.DealId
                            : (sequenceSource ?? Sequences.Provider).Next();
        }

        // NOTE: ReferenceId of a deal is used as the unixtimestamp that a deal was set to published, cannot be used here for something else
        // to.ReferenceId = DO_NOT_USE...
        UpdateDealStatus(to, source.Status);

        to.HoursAllowedInProgress = source.HoursAllowedInProgress.Gz(existingBeingUpdated?.HoursAllowedInProgress ?? 0).Gz(_defaultHoursInProgress);
        to.HoursAllowedRedeemed = source.HoursAllowedRedeemed.Gz(existingBeingUpdated?.HoursAllowedRedeemed ?? 0).Gz(_defaultHoursRedeemed);
        to.AutoApproveRequests = source.AutoApproveRequests ?? existingBeingUpdated?.AutoApproveRequests ?? false;
        to.MaxApprovals = source.MaxApprovals?.Gz(int.MaxValue) ?? (existingBeingUpdated?.MaxApprovals ?? 0).Gz(int.MaxValue);

        to.InvitedPublisherAccountIds = source.InvitedPublisherAccounts?.Select(a => a.Id).AsHashSet() ?? existingBeingUpdated?.InvitedPublisherAccountIds;

        to.Restrictions = (source.Restrictions ?? existingBeingUpdated?.Restrictions)?.Where(r => r.Value.HasValue())
                                                                                     .AsList();

        to.PublisherApprovedMediaIds = (source.PublisherApprovedMediaIds?.AsHashSet() ?? existingBeingUpdated?.PublisherApprovedMediaIds).NullIfEmpty();

        // Once private, remains that way. To become private means null restrictions on a new deal with invites where the deal is being published
        to.IsPrivateDeal = (existingBeingUpdated?.IsPrivateDeal ?? false) ||
                           (source.IsPrivateDeal ?? false) ||
                           (to.Restrictions == null &&
                            !to.InvitedPublisherAccountIds.IsNullOrEmpty() &&
                            to.DealStatus == DealStatus.Published);

        to.ExpirationDate = source.ExpirationDate ?? existingBeingUpdated?.ExpirationDate ?? DateTimeHelper.MaxApplicationDate;

        to.ReceiveNotes = source.ReceiveNotes ?? existingBeingUpdated?.ReceiveNotes;
        to.Description = source.Description ?? existingBeingUpdated?.Description;
        to.ApprovalNotes = source.ApprovalNotes ?? existingBeingUpdated?.ApprovalNotes;

        to.PlaceId = (source.Place?.Id).Gz(existingBeingUpdated?.PlaceId);
        to.ReceivePlaceId = (source.ReceivePlace?.Id).Gz(existingBeingUpdated?.ReceivePlaceId);

        to.PublisherMediaIds = (source.PublisherMedias?
                                      .Where(i => i.Id > 0)
                                      .Select(i => i.Id)
                                      .AsHashSet()
                                      .NullIfEmpty()) ?? existingBeingUpdated?.PublisherMediaIds;

        to.ReceivePublisherAccountIds = source.ReceivePublisherAccounts?.Select(a => a.Id).AsHashSet() ?? existingBeingUpdated?.ReceivePublisherAccountIds;

        to.ReceiveHashtagIds = source.ReceiveHashtags?.Select(h => h.Id).AsHashSet() ?? existingBeingUpdated?.ReceiveHashtagIds;

        // No owner for a deal any longer
        to.OwnerId = 0;

        return to;
    }

    public static bool UpdateDealStatus(this DynDeal source, DealStatus toDealStatus)
    {
        if (toDealStatus == DealStatus.Unknown)
        {
            toDealStatus = source.DealStatus;
        }

        // If the status already matches, and either:
        //    Status is published/paused and the refId reflects a published value
        //    OR
        //    Status is not published/pausedd and the refId reflects a NON-published value (i.e. null)
        //
        // Then we have nothing to update and the record is already in the correct statue
        if (source.DealStatus == toDealStatus &&
            (
                ((toDealStatus == DealStatus.Published || toDealStatus == DealStatus.Paused) && source.ReferenceId.HasValue())
                ||
                ((toDealStatus != DealStatus.Published && toDealStatus != DealStatus.Paused) && !source.ReferenceId.HasValue())
            ))
        {
            return false;
        }

        // Status and/or refId is not set correctly, so sync them up correctly now

        source.DealStatus = toDealStatus;

        if (toDealStatus == DealStatus.Published || toDealStatus == DealStatus.Paused)
        { // Setting to published/paused, so ensure both publishedOn and referenceId have values
            var nowUtc = DateTimeHelper.UtcNow;

            if (toDealStatus == DealStatus.Published && !source.PublishedOn.HasValue)
            {
                source.PublishedOn = nowUtc;
            }

            if (!source.ReferenceId.HasValue())
            {
                source.ReferenceId = nowUtc.ToUnixTimestamp().ToStringInvariant();
            }
        }
        else
        {
            source.ReferenceId = null;
        }

        return true;
    }

    public static DealRequest ToDealRequest(this DynDealRequest source)
    {
        var to = source.ConvertTo<DealRequest>();

        to.PublisherAccountId = source.PublisherAccountId;
        to.Title = source.Title;
        to.DealId = source.DealId;
        to.RequestedOn = source.CreatedOn;
        to.Status = source.RequestStatus;
#pragma warning disable 618
        to.DaysUntilDelinquent = source.HoursAllowedRedeemed / 24;
#pragma warning restore 618
        to.IsDelinquent = source.IsDelinquent();
        to.StatusLastChangedOn = source.ReferenceId.ToLong(0);

        return to;
    }

    public static async Task<DynDealRequest> ToDynDealRequestAsync(this DynDeal source, long forPublisherAccountId, DealRequestStatus requestStatus,
                                                                   int hoursAllowedInProgress, int hoursAllowedRedeemed)
    {
        var existingDealRequest = hoursAllowedInProgress > 0 && hoursAllowedRedeemed > 0
                                      ? null
                                      : await DealExtensions.DefaultDealRequestService.GetDealRequestAsync(source.DealId, forPublisherAccountId);

        var to = new DynDealRequest
                 {
                     Id = source.DealId,
                     EdgeId = forPublisherAccountId.ToEdgeId(),
                     Title = source.Title,
                     RequestStatus = requestStatus,
                     DynItemType = DynItemType.DealRequest,
                     DealContextWorkspaceId = source.DealContextWorkspaceId,
                     HoursAllowedInProgress = hoursAllowedInProgress.Gz(existingDealRequest?.HoursAllowedInProgress ?? 0).Gz(_defaultHoursInProgress),
                     HoursAllowedRedeemed = hoursAllowedRedeemed.Gz(existingDealRequest?.HoursAllowedRedeemed ?? 0).Gz(_defaultHoursRedeemed)
                 };

        to.UpdateDateTimeTrackedValues();

        // Owner for a request is the pub account id who owns the deal (i.e. the pub account that created the deal)
        to.OwnerId = source.PublisherAccountId;
        to.DealWorkspaceId = source.WorkspaceId;

        // Reference is time the request was made
        to.ReferenceId = to.CreatedOnUtc.ToStringInvariant();

        return to;
    }

    public static DealRequestStatusChange ToDealRequestStatusChange(this DynDealRequestStatusChange source)
    {
        var to = source.ConvertTo<DealRequestStatusChange>();

        to.FromStatus = source.FromDealRequestStatus;
        to.ToStatus = source.ToDealRequestStatus;
        to.DealId = source.DealId;

        return to;
    }

    public static async Task<DealRequest> ToDealRequestAsync(this DealRequestExtended source)
    {
        if (source?.DealRequest == null)
        {
            return null;
        }

        var dealRequest = source.DealRequest.ToDealRequest();

        if (!source.CompletionMedia.IsNullOrEmpty())
        {
            dealRequest.CompletionMedia = new List<PublisherMedia>(source.CompletionMedia.Count);

            foreach (var completionMedia in source.CompletionMedia)
            {
                var publisherMedia = await completionMedia.ToPublisherMediaAsync();
                dealRequest.CompletionMedia.Add(publisherMedia);
            }
        }

        dealRequest.StatusChanges = source.StatusChanges?
                                          .Select(sc => sc.ToDealRequestStatusChange())
                                          .AsList();

        if (!source.LifetimeStats.IsNullOrEmptyRydr() && !dealRequest.CompletionMedia.IsNullOrEmpty())
        {
            foreach (var completionMedia in dealRequest.CompletionMedia
                                                       .Where(c => c.Id > 0))
            {
                completionMedia.LifetimeStats = source.LifetimeStats.ContainsKey(completionMedia.Id)
                                                    ? source.LifetimeStats[completionMedia.Id].ToPublisherMediaStat()
                                                    : null;
            }
        }

        dealRequest.LastMessage = source.LastMessage;

        return dealRequest;
    }
}
