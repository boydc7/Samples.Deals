using GeoCoordinatePortable;
using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;

namespace Rydr.FbSdk.Extensions;

public static class GeoExtensions
{
    public static bool IsValidGeoQuery(this IGeoQuery geoQuery)
        => geoQuery != null &&
           Math.Abs(geoQuery.Latitude.GetValueOrDefault()) > 0 &&
           Math.Abs(geoQuery.Longitude.GetValueOrDefault()) > 0 &&
           (geoQuery.Miles.GetValueOrDefault() > 0 ||
            IsValidGeoBoundingBox(geoQuery.BoundingBox));

    public static bool IsValidGeoBoundingBox(this GeoBoundingBox boundingBox)
        => boundingBox != null &&
           (
               (HasNorthWestLocation(boundingBox) && HasSouthEastLocation(boundingBox))
               ||
               (HasNorthEastLocation(boundingBox) && HasSouthWestLocation(boundingBox))
           );

    public static bool IsValidElasticGeoBoundingBox(this GeoBoundingBox boundingBox)
        => boundingBox != null && HasNorthWestLocation(boundingBox) && HasSouthEastLocation(boundingBox);

    public static bool HasNorthWestLocation(this GeoBoundingBox boundingBox)
        => Math.Abs(boundingBox.NorthWestLatitude) > 0 &&
           Math.Abs(boundingBox.NorthWestLongitude) > 0;

    public static bool HasNorthEastLocation(this GeoBoundingBox boundingBox)
        => Math.Abs(boundingBox.NorthEastLatitude) > 0 &&
           Math.Abs(boundingBox.NorthEastLongitude) > 0;

    public static bool HasSouthWestLocation(this GeoBoundingBox boundingBox)
        => Math.Abs(boundingBox.SouthWestLatitude) > 0 &&
           Math.Abs(boundingBox.SouthWestLongitude) > 0;

    public static bool HasSouthEastLocation(this GeoBoundingBox boundingBox)
        => Math.Abs(boundingBox.SouthEastLatitude) > 0 &&
           Math.Abs(boundingBox.SouthEastLongitude) > 0;

    public static bool IsValidUserLatLon(this IHasUserLatitudeLongitude source)
        => source != null && IsValidLatLon(source.UserLatitude, source.UserLongitude);

    public static bool IsValidLatLon(this IHasLatitudeLongitude source)
        => source != null && IsValidLatLon(source.Latitude, source.Longitude);

    public static bool IsValidLatLon(double? latitude, double? longitude)
        => latitude.HasValue && longitude.HasValue && IsValidLatLon(latitude.Value, longitude.Value);

    public static bool IsValidLatLon(double latitude, double longitude)
        => latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180 &&
           Math.Abs(latitude) > 0 && Math.Abs(longitude) > 0;

    public static double? DistanceBetween(double? fromLatitude, double? fromLongitude, double? toLatitude, double? toLongitude)
    {
        if (Math.Abs(fromLatitude.GetValueOrDefault()) <= 0 || Math.Abs(fromLongitude.GetValueOrDefault()) <= 0 ||
            Math.Abs(toLatitude.GetValueOrDefault()) <= 0 || Math.Abs(toLongitude.GetValueOrDefault()) <= 0)
        {
            return null;
        }

        var fromLocation = new GeoCoordinate(fromLatitude.Value, fromLongitude.Value);
        var toLocation = new GeoCoordinate(toLatitude.Value, toLongitude.Value);

        // GetDistanceTo is in meters...
        return Math.Round((fromLocation.GetDistanceTo(toLocation) / 1609.344), 4);
    }

    public static double? DistanceBetween(double? fromLatitude, double? fromLongitude, GeoBoundingBox to)
        => !to.TryGetFirstValidLatLon(out var toLatLon)
               ? null
               : DistanceBetween(fromLatitude, fromLongitude, toLatLon.Latitude, toLatLon.Longitude);

    private static bool TryGetFirstValidLatLon(this GeoBoundingBox boundingBox, out (double Latitude, double Longitude) latLon)
    {
        if (!IsValidGeoBoundingBox(boundingBox))
        {
            latLon = (0, 0);

            return false;
        }

        latLon = Math.Abs(boundingBox.NorthEastLatitude) > 0
                     ? (boundingBox.NorthEastLatitude, boundingBox.NorthEastLongitude)
                     : Math.Abs(boundingBox.NorthWestLatitude) > 0
                         ? (boundingBox.NorthWestLatitude, boundingBox.NorthWestLongitude)
                         : Math.Abs(boundingBox.SouthWestLatitude) > 0
                             ? (boundingBox.SouthWestLatitude, boundingBox.SouthWestLongitude)
                             : (boundingBox.SouthEastLatitude, boundingBox.SouthEastLongitude);

        return true;
    }
}
