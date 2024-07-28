using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Helpers;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Publishers;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;
using ServiceStack.Host;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Dapper;
using ServiceStack.Web;

#pragma warning disable 162

namespace Rydr.Api.Services.Filters;

public class DealUpsertFilter : ITypedFilter<IRequestBaseWithModel<Deal>>
{
    private static readonly IPocoDynamo _dynamoDb = RydrEnvironment.Container.Resolve<IPocoDynamo>();

    private static readonly ILog _log = LogManager.GetLogger("RydrAccountPrivateDealFilter");
    private static readonly List<PublisherAccount> _internalInvites;
    private static readonly HashSet<long> _internalInviteIdentifiers;

    private DealUpsertFilter() { }

    static DealUpsertFilter()
    {
        var internalInviteFbAccountIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                         {
                                             // "574243964", // Chad Boyd live
                                             // "10156942417033965", // Chad Boyd personal login
                                             "142716683632243", // Rydr Jones user login
                                             //"10219484436400617", // Brian Curley personal login,
                                             //"10157851213802784", // Andre Refay personal login,
                                             //"115394106497865", // Joanna getrydr personal login
                                             // "",                     // GetRydr login - don't have it
                                             "17841411449838030", // techrider999 fbig
                                             //"17841407480526035", // andrerefay fbig
                                             "17841401262722784", // rydrjones fbig
                                             "17841411318206470" // boydc77 fbig
                                             //"17841401281033914", // chasingbrian fbig
                                             //"17841410718664133", // get_rydr fbig
                                             //"17841405671082588", // jojosdottir fbig (Joanna's)
                                         };

        var dynPublisherAccounts = internalInviteFbAccountIds.Select(fbi => PublisherExtensions.DefaultPublisherAccountService
                                                                                               .TryGetPublisherAccountAsync(PublisherType.Facebook, fbi)
                                                                                               .GetAwaiter().GetResult())
                                                             .Where(i => i != null && i.DeletedOnUtc == null)
                                                             .AsList();

        if (!dynPublisherAccounts.IsNullOrEmpty())
        { // The list of publisherAccounts (IDs are all that is needed) for deals that should stay internal
            _internalInvites = dynPublisherAccounts.Select(i => new PublisherAccount
                                                                {
                                                                    Id = i.PublisherAccountId
                                                                })
                                                   .AsList()
                                                   .ToNullIfEmpty();

            // Get the identifiers used to id a deal put/post/update from an internal account (publisher account, workspace, etc.)
            _internalInviteIdentifiers = dynPublisherAccounts.Select(p => WorkspaceService.DefaultWorkspaceService.TryGetWorkspace(p.WorkspaceId.Gz(p.CreatedWorkspaceId)))
                                                             .Where(w => w != null)
                                                             .Select(w => w.Id)
                                                             .Concat(_internalInvites.Select(i => i.Id))
                                                             .AsHashSet()
                                                             .NullIfEmpty();
        }
    }

    public static DealUpsertFilter Instance { get; } = new();

    public void Invoke(IRequest req, IResponse res, IRequestBaseWithModel<Deal> dto)
    {
        var dynDeal = dto.Model.Id > 0
                          ? _dynamoDb.GetItemByEdgeInto<DynDeal>(DynItemType.Deal, dto.Model.Id.ToEdgeId(), true)
                          : null;

        SetDealGroupId(dto, dynDeal);

#if LOCAL || DEBUG || DEVELOPMENT
        return;
#endif

        ProcessRydrInternalDeal(dto, dynDeal);
    }

    private void SetDealGroupId(IRequestBaseWithModel<Deal> dto, DynDeal existingDynDeal)
    {
        if (dto.Model.DealGroupId.HasValue() || dto.Model.Status != DealStatus.Published)
        {
            return;
        }

        // To determine if a deal is at a same place, try to use the lat/lon first...then the placeId, and last the type/pubid combo if new
        string locationPart = null;

        if (dto.Model.Place == null && existingDynDeal != null && existingDynDeal.PlaceId > 0)
        {
            var place = _dynamoDb.GetItem<DynPlace>(existingDynDeal.PlaceId.ToItemDynamoId());

            locationPart = place?.Address.IsValidLatLon() ?? false
                               ? string.Concat(Math.Round(place.Address.Latitude.Value, 4), "|", Math.Round(place.Address.Longitude.Value, 4))
                               : existingDynDeal.PlaceId.ToStringInvariant();
        }
        else if (dto.Model.Place != null)
        {
            locationPart = dto.Model.Place.Address.IsValidLatLon()
                               ? string.Concat(Math.Round(dto.Model.Place.Address.Latitude.Value, 4), "|", Math.Round(dto.Model.Place.Address.Longitude.Value, 4))
                               : dto.Model.Place.Id.ToStringInvariant().Coalesce(string.Concat(dto.Model.Place.PublisherType, "|", dto.Model.Place.PublisherId));
        }

        // Same deal group = same:
        //    PublisherAccountId
        //    Title
        //    Exp. Date
        //    Location
        dto.Model.DealGroupId = string.Concat(dto.Model.PublisherAccountId.Gz(existingDynDeal?.PublisherAccountId ?? 0), "|",
                                              dto.Model.Title.Coalesce(existingDynDeal?.Title).Trim().ToLowerInvariant(), "|",
                                              (dto.Model.ExpirationDate.ToUnixTimestamp() ?? existingDynDeal?.ExpirationDate.ToUnixTimestamp() ?? 0), "|",
                                              locationPart ?? string.Empty)
                                      .ToSafeSha64();
    }

    private void ProcessRydrInternalDeal(IRequestBaseWithModel<Deal> dto, DynDeal existingDynDeal)
    {
        if (_internalInviteIdentifiers == null || dto.Model.Status != DealStatus.Published)
        {
            return;
        }

        var dealPublisherAccountId = dto.Model.PublisherAccountId;

        if (dealPublisherAccountId <= 0 && dto.Model.Id > 0 && existingDynDeal != null)
        {
            if (existingDynDeal.IsPrivateDeal)
            { // Once a deal is private, that cannot change, nothing to do here
                return;
            }

            dealPublisherAccountId = existingDynDeal.PublisherAccountId;
        }

        if (dealPublisherAccountId <= 0)
        {
            return;
        }

        if (!_internalInviteIdentifiers.Contains(dealPublisherAccountId) &&
            !_internalInviteIdentifiers.Contains(dto.RequestPublisherAccountId) &&
            !_internalInviteIdentifiers.Contains(dto.WorkspaceId))
        {
            return;
        }

        if (dto.Model.Tags != null && dto.Model.Tags.Any(t => t.Value.EqualsOrdinalCi(Tag.TagRydrExternalDeal)))
        { // Forcing non-private creation
            return;
        }

        _log.DebugInfoFormat("RydrAccountPrivateDeal [{0}] being published, forcing to private with known list of internal invitees", dto.Model.Id > 0
                                                                                                                                          ? dto.Model.Id.ToStringInvariant()
                                                                                                                                          : dto.Model.Title);

        // NOTE: We do not set the restrictions to null here, as we typically want to be able to test various restriction levels, this flag is honored however
        dto.Model.IsPrivateDeal = true;

        if (dto.Model.Tags == null)
        {
            dto.Model.Tags = new HashSet<Tag>();
        }

        dto.Model.Tags.Add(Tag.TagRydrInternalDeal);

        if (dto.Model.InvitedPublisherAccounts.IsNullOrEmpty())
        {
            dto.Model.InvitedPublisherAccounts = _internalInvites;
        }
    }
}
