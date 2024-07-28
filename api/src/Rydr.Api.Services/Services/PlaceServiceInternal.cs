using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Models.Rydr;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto;
using ServiceStack;

namespace Rydr.Api.Services.Services;

public class PlaceServiceInternal : BaseInternalOnlyApiService
{
    private readonly IRydrDataService _rydrDataService;

    public PlaceServiceInternal(IRydrDataService rydrDataService)
    {
        _rydrDataService = rydrDataService;
    }

    public async Task Post(PlaceUpdated request)
    {
        var dynPlace = await _dynamoDb.TryGetPlaceAsync(request.PlaceId);

        if (dynPlace == null)
        {
            return;
        }

        string addressIdHash = null;

        if (dynPlace.Address != null)
        {
            var rydrAddress = dynPlace.Address.ToRydrAddress();

            addressIdHash = rydrAddress?.Id;

            if (addressIdHash.HasValue())
            {
                await _rydrDataService.SaveIgnoreConflictAsync(rydrAddress, t => t.Id);
            }
        }

        var rydrPlace = dynPlace.ConvertTo<RydrPlace>();

        rydrPlace.Id = dynPlace.PlaceId;
        rydrPlace.AddressId = addressIdHash;

        await _rydrDataService.SaveAsync(rydrPlace);
    }
}
