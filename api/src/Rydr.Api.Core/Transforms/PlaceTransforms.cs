using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Models.Internal;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Models.Supporting;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Core.Transforms;

public static class PlaceTransforms
{
    private static readonly Func<ILocalRequestCacheClient> _localRequestCacheClientFactory = () => RydrEnvironment.Container.Resolve<ILocalRequestCacheClient>();

    private static readonly string _dynPlaceTypeOwnSpaceFilter = string.Concat(((int)DynItemType.Place), "|", UserAuthInfo.PublicOwnerId);

    public static RydrAddress ToRydrAddress(this Address source)
    {
        if (source == null)
        {
            return null;
        }

        var rydrAddress = new RydrAddress
                          {
                              Name = source.Name?.Trim(),
                              Address1 = source.Address1?.Trim(),
                              Address2 = source.Address2?.Trim(),
                              City = source.City?.Trim(),
                              StateProvince = source.StateProvince?.Trim(),
                              CountryCode = source.CountryCode?.Trim(),
                              PostalCode = source.PostalCode?.Trim(),
                              Latitude = source.Latitude.NullIf(l => Math.Abs(l) < 0.00001),
                              Longitude = source.Longitude.NullIf(l => Math.Abs(l) < 0.00001),
                          };

        rydrAddress.Id = string.Concat(rydrAddress.Name, "|",
                                       rydrAddress.Address1, "|",
                                       rydrAddress.Address2, "|",
                                       rydrAddress.City, "|",
                                       rydrAddress.StateProvince, "|",
                                       rydrAddress.CountryCode, "|",
                                       rydrAddress.PostalCode, "|",
                                       rydrAddress.Latitude.GetValueOrDefault(), "|",
                                       rydrAddress.Longitude.GetValueOrDefault(), "|").ToShaBase64();

        return rydrAddress.Id.IsNullOrEmpty()
                   ? null
                   : rydrAddress;
    }

    public static string ToDisplayLocation(this DynPlace source)
    {
        if (source?.Address == null)
        {
            return source?.Name.ToNullIfEmpty();
        }

        return string.Concat(source.Name, "\n", ToDisplayLocation(source.Address));
    }

    public static string ToDisplayAddress(this Address source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        string getAddressPart(string from, string suffix = "\n")
            => from.HasValue()
                   ? string.Concat(from.Trim(), suffix)
                   : null;

        var hasState = source.StateProvince.HasValue();
        var hasCity = source.City.HasValue();

        return string.Concat(getAddressPart(source.Address1),
                             getAddressPart(source.Address2),
                             hasState && hasCity
                                 ? getAddressPart(source.City, ",")
                                 : hasCity
                                     ? getAddressPart(source.City)
                                     : null,
                             getAddressPart(source.StateProvince),
                             getAddressPart(source.PostalCode));
    }

    public static string ToDisplayLocation(this Address source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        return string.Concat(ToDisplayAddress(source),
                             source.IsValidLatLon()
                                 ? string.Concat(source.Latitude, ",", source.Longitude)
                                 : null);
    }

    public static async Task<DynPlace> TryGetPlaceAsync(this IPocoDynamo dynamoDb, PublisherType publisherType, string publisherId)
    {
        var indexModel = await _localRequestCacheClientFactory().TryGetAsync(string.Concat("typeOwnSpacePlace|", publisherType, "|", publisherId),
                                                                             () => dynamoDb.FromQueryIndex<DynItemTypeOwnerSpaceReferenceGlobalIndex>(i => i.TypeOwnerSpace == _dynPlaceTypeOwnSpaceFilter &&
                                                                                                                                                           i.ReferenceId == DynPlace.BuildRefId(publisherType, publisherId))
                                                                                           .ExecAsync()
                                                                                           .SingleOrDefaultAsync(),
                                                                             CacheConfig.LongConfig);

        return indexModel == null
                   ? null
                   : await dynamoDb.GetItemAsync<DynPlace>(indexModel.GetDynamoId());
    }

    public static Task<DynPlace> TryGetPlaceAsync(this IPocoDynamo dynamoDb, long placeId)
        => GetPlaceAsync(dynamoDb, placeId, true);

    public static async Task<DynPlace> GetPlaceAsync(this IPocoDynamo dynamoDb, long placeId, bool ignoreNotFound = false)
    {
        var place = await dynamoDb.GetItemAsync<DynPlace>(placeId.ToItemDynamoId());

        if (!ignoreNotFound)
        {
            Guard.AgainstRecordNotFound(place == null || place.IsDeleted(), placeId.ToStringInvariant());
        }

        return place;
    }

    public static Place ToPlace(this DynPlace source)
    {
        if (source == null)
        {
            throw new RecordNotFoundException();
        }

        var result = source.ConvertTo<Place>();

        if (result.Address != null && result.Address.Name.IsNullOrEmpty())
        {
            result.Address.Name = source.Name;
        }

        return result;
    }

    public static DynPlace ToDynPlace(this Place source, DynPlace existingBeingUpdated = null, ISequenceSource sequenceSource = null)
    {
        var to = source.ConvertTo<DynPlace>();

        if (existingBeingUpdated == null)
        { // New one
            to.DynItemType = DynItemType.Place;
            to.UpdateDateTimeTrackedValues(source);
        }
        else
        {
            to.TypeId = existingBeingUpdated.TypeId;
            to.Id = existingBeingUpdated.Id;
            to.UpdateDateTimeDeleteTrackedValues(existingBeingUpdated);
        }

        to.Address = to.Address.FormatAddress();

        if (to.Address != null && to.Address.Name.IsNullOrEmpty())
        {
            to.Address.Name = to.Name;
        }

        if (to.Id <= 0)
        {
            to.Id = existingBeingUpdated != null && existingBeingUpdated.Id > 0
                        ? existingBeingUpdated.Id
                        : (sequenceSource ?? Sequences.Provider).Next();
        }

        to.EdgeId = to.Id.ToEdgeId();
        to.ReferenceId = existingBeingUpdated?.ReferenceId ?? DynPlace.BuildRefId(to.PublisherType, to.PublisherId.Coalesce(to.Id.ToStringInvariant()));

        // Places are generally publicly available for use by anyone
        to.OwnerId = UserAuthInfo.PublicOwnerId;

        return to;
    }
}
