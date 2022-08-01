using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rydr.Api.Dto.Interfaces;
using Rydr.FbSdk.Extensions;
using Rydr.FbSdk.Models;

namespace Rydr.FbSdk
{
    public partial class FacebookClient
    {
        public async IAsyncEnumerable<List<FbPlaceInfo>> SearchPlacesAsync(string query, IGeoQuery geoQuery = null)
        {
            var fields = _fieldNameMap.GetOrAdd(typeof(FbPlaceInfo), t => GetFieldStringForType(typeof(FbPlaceInfo)));

            var param = geoQuery.IsValidGeoQuery()
                            ? new
                              {
                                  type = "place",
                                  q = query,
                                  center = string.Concat(geoQuery.Latitude, ",", geoQuery.Longitude),
                                  distance = Math.Round((geoQuery.Miles.GetValueOrDefault() > 0
                                                             ? geoQuery.Miles.Value
                                                             : GeoExtensions.DistanceBetween(geoQuery.Latitude, geoQuery.Longitude, geoQuery.BoundingBox).Value) * 1609.344,
                                                        4),
                                  fields,
                                  limit = 50
                              }
                            : (object)new
                                      {
                                          type = "place",
                                          q = query,
                                          fields,
                                          limit = 50
                                      };

            await foreach (var places in GetPagedAsync<FbPlaceInfo>("search", param).ConfigureAwait(false))
            {
                yield return places;
            }
        }
    }
}
