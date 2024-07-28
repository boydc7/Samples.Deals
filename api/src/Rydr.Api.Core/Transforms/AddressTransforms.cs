using System.Globalization;
using Rydr.Api.Core.Extensions;
using Rydr.Api.Core.Models.Doc;
using Rydr.Api.Dto.Shared;
using Rydr.FbSdk.Extensions;
using ServiceStack;

namespace Rydr.Api.Core.Transforms;

public static class AddressTransforms
{
    public static string ToLocationMapLatLonString(this double latOrLon)
        => $"{Math.Round(latOrLon, 2):0.00}";

    public static long ToLocationMapId(string latitudeString, string longitudeString)
        => latitudeString.IsNullOrEmpty() || longitudeString.IsNullOrEmpty()
               ? 0
               : string.Concat(latitudeString, longitudeString)
                       .Replace(".", string.Empty)
                       .Replace("-", string.Empty)
                       .ToLong(long.MinValue);

    public static string ToLocationMapEdgeId(string latitudeString, string longitudeString)
        => string.Concat("geomap|", latitudeString, longitudeString);

    public static Address ToAddress(this DynItemMap locationMap)
    {
        if ((locationMap?.Items).IsNullOrEmptyRydr())
        {
            return null;
        }

        return new Address
               {
                   Address1 = locationMap.Items.GetValueOrDefaultSafe("Address1"),
                   Address2 = locationMap.Items.GetValueOrDefaultSafe("Address2"),
                   City = locationMap.Items.GetValueOrDefaultSafe("City"),
                   StateProvince = locationMap.Items.GetValueOrDefaultSafe("StateProvince"),
                   CountryCode = locationMap.Items.GetValueOrDefaultSafe("CountryCode"),
                   PostalCode = locationMap.Items.GetValueOrDefaultSafe("PostalCode"),
                   Latitude = locationMap.Items.GetValueOrDefaultSafe("Latitude").ToDoubleRydr(),
                   Longitude = locationMap.Items.GetValueOrDefaultSafe("Longitude").ToDoubleRydr(),
               };
    }

    public static DynItemMap ToLocationMap(this Address source, DynItemMap existingMap = null)
    {
        if (source?.Latitude == null || source.Longitude == null)
        {
            return null;
        }

        if (existingMap?.Items != null &&
            existingMap.Items.ContainsKey("City") &&
            existingMap.Items.ContainsKey("StateProvince") &&
            existingMap.Items.ContainsKey("CountryCode"))
        {
            return null;
        }

        var latString = ToLocationMapLatLonString(source.Latitude.Value);
        var lonString = ToLocationMapLatLonString(source.Longitude.Value);

        var map = new DynItemMap
                  {
                      Id = ToLocationMapId(latString, lonString),
                      EdgeId = ToLocationMapEdgeId(latString, lonString),
                      Items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                              {
                                  {
                                      "Latitude", latString
                                  },
                                  {
                                      "Longitude", lonString
                                  }
                              }
                  };

        if (map.Id <= 0)
        {
            return null;
        }

        if (source.City.HasValue())
        {
            map.Items.Add("City", source.City);
        }

        if (source.CountryCode.HasValue())
        {
            map.Items.Add("CountryCode", source.CountryCode);
        }

        if (source.StateProvince.HasValue())
        {
            map.Items.Add("StateProvince", source.StateProvince);
        }

        if (source.Name.HasValue())
        {
            map.Items.Add("Name", source.Name);
        }

        if (source.Address1.HasValue())
        {
            map.Items.Add("Address1", source.Address1);
        }

        if (source.Address2.HasValue())
        {
            map.Items.Add("Address2", source.Address2);
        }

        if (source.PostalCode.HasValue())
        {
            map.Items.Add("PostalCode", source.PostalCode);
        }

        return map;
    }

    public static string ToEsSearchString(this Address source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        return string.Concat(source.Name, " ",
                             ToEsString(source.Latitude.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                                        source.Longitude.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                                        source.Address1, source.Address2,
                                        source.City, source.StateProvince, source.PostalCode))
                     .Trim();
    }

    public static Address FormatAddress(this Address source)
    {
        if (source == null)
        {
            return source;
        }

        source.PostalCode = source.PostalCode?.LeftPart(',').Left(50).ToNullIfEmpty();

        return source;
    }

    private static string ToEsString(params string[] sources)
    {
        if (sources == null || sources.Length <= 0)
        {
            return string.Empty;
        }

        var returnString = sources.Where(s => s.HasValue())
                                  .Aggregate(string.Empty, (current, source) => current + string.Concat(" ", source).Trim());

        return returnString;
    }
}
