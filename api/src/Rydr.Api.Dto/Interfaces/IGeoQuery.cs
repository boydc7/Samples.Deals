using Rydr.Api.Dto.Shared;

namespace Rydr.Api.Dto.Interfaces;

public interface IHasUserLatitudeLongitude
{
    double? UserLatitude { get; }
    double? UserLongitude { get; }
}

public interface IHasLatitudeLongitude
{
    double? Latitude { get; }
    double? Longitude { get; }
}

public interface IGeoQuery : IHasLatitudeLongitude
{
    double? Miles { get; }
    GeoBoundingBox BoundingBox { get; }
}
