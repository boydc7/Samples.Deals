using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Rydr.Api.Core.Configuration;
using Rydr.Api.Core.Enums;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Interfaces.DataAccess;
using Rydr.Api.Core.Interfaces.Internal;
using Rydr.Api.Core.Interfaces.Services;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Core.Transforms;
using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Shared;
using Rydr.Api.Dto.Users;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Aws.DynamoDb;

namespace Rydr.Api.Services.Services
{
    public class LocationService : BaseAuthenticatedApiService
    {
        private static readonly string _googleMapsGeoKey;
        private readonly IWorkspaceService _workspaceService;
        private readonly IRydrDataService _rydrDataService;

        static LocationService()
        {
            _googleMapsGeoKey = RydrEnvironment.GetAppSetting("Google.MapsGeoCode.ApiKey", "SECRET");

            if (_googleMapsGeoKey.EqualsOrdinalCi("SECRET"))
            {
                var secretService = RydrEnvironment.Container.Resolve<ISecretService>();

                var secretValue = secretService.TryGetSecretStringAsync(string.Concat("Google.MapsGeoCode.ApiKey.", RydrEnvironment.CurrentEnvironment))
                                               .GetAwaiter().GetResult();

                _googleMapsGeoKey = secretValue;
            }
        }

        public LocationService(IWorkspaceService workspaceService, IRydrDataService rydrDataService)
        {
            _workspaceService = workspaceService;
            _rydrDataService = rydrDataService;
        }

        public async Task Put(PutLocationGeoMap request)
        {
            var existingMap = await GetExistingLocationMapAsync(request.Address.Latitude.Value, request.Address.Longitude.Value);

            var newMap = request.Address.ToLocationMap(existingMap);

            if (newMap == null)
            {
                return;
            }

            await _dynamoDb.PutItemAsync(newMap);
        }

        public async Task Post(AddLocationMap request)
        {
            if (!request.IsValidLatLon())
            {
                return;
            }

            var existingMap = request.ForceUpdate
                                  ? null
                                  : await GetExistingLocationMapAsync(request.Latitude.Value, request.Longitude.Value);

            if (existingMap != null &&
                existingMap.Items.ContainsKey("City") &&
                existingMap.Items.ContainsKey("StateProvince") &&
                existingMap.Items.ContainsKey("CountryCode"))
            {
                if (request.ForPublisherAccountId > 0)
                {
                    await StorePublisherLocationAsync(request.ForPublisherAccountId, existingMap.ToAddress());
                }

                return;
            }

            var address = await GetGeoCodedAddressAsync(request.Latitude.Value, request.Longitude.Value);

            var locationMap = address.ToLocationMap(existingMap);

            await _dynamoDb.PutItemAsync(locationMap);

            if (request.ForPublisherAccountId > 0)
            {
                await StorePublisherLocationAsync(request.ForPublisherAccountId, existingMap.ToAddress());
            }
        }

        public async Task Put(PutWorkspaceAccountLocation request)
        {
            var workspace = await _workspaceService.TryGetWorkspaceAsync(request.WorkspaceId);

            if (workspace == null || workspace.IsDeleted() || workspace.OwnerId != request.UserId || workspace.WorkspaceType != WorkspaceType.Personal)
            {
                return;
            }

            var workspacePublisherAccount = await _workspaceService.TryGetDefaultPublisherAccountAsync(request.WorkspaceId);

            if (workspacePublisherAccount == null)
            {
                return;
            }

            var capturedAt = (request.CapturedAt ?? _dateTimeProvider.UtcNow).ToUnixTimestamp();

            var lastLocation = await _dynamoDb.FromQuery<DynAccountLocation>(l => l.Id == workspacePublisherAccount.PublisherAccountId &&
                                                                                  Dynamo.BeginsWith(l.EdgeId, DynAccountLocation.AccountLocationStartsWith))
                                              .Filter(l => l.TypeId == (int)DynItemType.AccountLocation)
                                              .ExecAsync(50.ToDynamoBatchCeilingTake())
                                              .OrderByDescending(l => l.CapturedAt)
                                              .FirstOrDefaultAsync();

            // If we have an existing location skip some cases where we do not need to write a new one
            if (lastLocation != null)
            {
                if (lastLocation.CapturedAt >= (capturedAt - 180))
                { // Existing is new enough, nothing to do
                    return;
                }

                // 3 decimal degrees of lat/long is about a football field plus long (365 feet or so)
                if (Math.Abs(lastLocation.Latitude - request.Latitude) < 0.001 &&
                    Math.Abs(lastLocation.Longitude - request.Longitude) < 0.001)
                { // Same location(ish), no need to re-store
                    return;
                }
            }

            // Store a new one
            var dynLocation = new DynAccountLocation
                              {
                                  PublisherAccountId = workspacePublisherAccount.PublisherAccountId,
                                  EdgeId = DynAccountLocation.BuildEdgeId(capturedAt),
                                  WorkspaceId = workspace.Id,
                                  DynItemType = DynItemType.AccountLocation,
                                  ExpiresAt = _dateTimeProvider.UtcNow.AddDays(95).ToUnixTimestamp(),
                                  CapturedAt = capturedAt,
                                  Latitude = request.Latitude,
                                  Longitude = request.Longitude
                              };

            await _dynamoDb.PutItemTrackedAsync(dynLocation);
        }

        private async Task StorePublisherLocationAsync(long publisherAccountId, Address address)
        {
            if (publisherAccountId <= 0 || address == null)
            {
                return;
            }

            var rydrAddress = address.ToRydrAddress();

            if ((rydrAddress?.Id).IsNullOrEmpty())
            {
                return;
            }

            await _rydrDataService.SaveIgnoreConflictAsync(rydrAddress, t => t.Id);

            await _rydrDataService.ExecAdHocAsync(@"
INSERT    IGNORE INTO PublisherLocations
          (PublisherAccountId, AddressId, CreatedOn)
VALUES    (@PublisherAccountId, @AddressId, @CreatedOn);
",
                                                  new
                                                  {
                                                      PublisherAccountId = publisherAccountId,
                                                      AddressId = rydrAddress.Id,
                                                      CreatedOn = _dateTimeProvider.UtcNow.Date
                                                  });
        }

        private async Task<DynItemMap> GetExistingLocationMapAsync(double latitude, double longitude)
        {
            var latString = latitude.ToLocationMapLatLonString();
            var lonString = longitude.ToLocationMapLatLonString();
            var locationId = AddressTransforms.ToLocationMapId(latString, lonString);

            var existingMap = await _dynamoDb.GetItemAsync<DynItemMap>(locationId, AddressTransforms.ToLocationMapEdgeId(latString, lonString));

            return existingMap;
        }

        private async Task<Address> GetGeoCodedAddressAsync(double latitude, double longitude)
        {
            if (_googleMapsGeoKey.IsNullOrEmpty())
            {
                _log.DebugInfoFormat("Cannot GetGeoCodedAddressAsync, no maps geo key to lookup with");

                return null;
            }

            var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={_googleMapsGeoKey}";

            string response = null;

            try
            {
                response = await requestUrl.GetJsonFromUrlAsync();
            }
            catch(Exception x)
            {
                _log.Exception(x, "Could not successfully get google goecode json in GetGeoCodedAddressAsync");
            }

            if (response.IsNullOrEmpty())
            {
                return null;
            }

            var geoCodeObject = response.FromJson<GoogleMapGeoCodeResponse>();

            if ((geoCodeObject?.Results).IsNullOrEmptyRydr())
            {
                return null;
            }

            GoogleMapGeoCodeResult geoResult = null;

            foreach (var geoCodeResult in geoCodeObject.Results.Where(r => !r.AddressComponents.IsNullOrEmptyRydr() &&
                                                                           r.FormattedAddress.HasValue() &&
                                                                           !r.Types.IsNullOrEmptyRydr()))
            {
                if (geoResult == null)
                {
                    geoResult = geoCodeResult;
                }

                if (geoResult.Types.Contains("street_address", StringComparer.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            if (geoResult == null)
            {
                return null;
            }

            var address = new Address
                          {
                              Name = geoResult.FormattedAddress,
                              Address1 = string.Concat(geoResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("street_number", StringComparer.OrdinalIgnoreCase))?.ShortName,
                                                       " ",
                                                       geoResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("route", StringComparer.OrdinalIgnoreCase))?.ShortName)
                                               .Trim()
                                               .ToNullIfEmpty(),
                              City = geoResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("locality", StringComparer.OrdinalIgnoreCase))?.ShortName,
                              StateProvince = geoResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("administrative_area_level_1", StringComparer.OrdinalIgnoreCase))?.ShortName,
                              County = geoResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("administrative_area_level_2", StringComparer.OrdinalIgnoreCase))?.ShortName,
                              PostalCode = geoResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("postal_code", StringComparer.OrdinalIgnoreCase))?.ShortName,
                              Latitude = latitude,
                              Longitude = longitude,
                          };

            return address;
        }

        [DataContract]
        private class GoogleMapGeoCodeResponse
        {
            [DataMember(Name = "results")]
            public List<GoogleMapGeoCodeResult> Results { get; set; }
        }

        [DataContract]
        private class GoogleMapGeoCodeResult
        {
            [DataMember(Name = "address_components")]
            public List<GoogleMapGeoCodeAddressComponent> AddressComponents { get; set; }

            [DataMember(Name = "formatted_address")]
            public string FormattedAddress { get; set; }

            [DataMember(Name = "types")]
            public List<string> Types { get; set; }
        }

        [DataContract]
        private class GoogleMapGeoCodeAddressComponent
        {
            [DataMember(Name = "long_name")]
            public string LongName { get; set; }

            [DataMember(Name = "short_name")]
            public string ShortName { get; set; }

            [DataMember(Name = "types")]
            public List<string> Types { get; set; }
        }
    }
}
