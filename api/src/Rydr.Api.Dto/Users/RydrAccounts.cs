using Rydr.Api.Dto.Interfaces;
using Rydr.Api.Dto.Shared;
using ServiceStack;

namespace Rydr.Api.Dto.Users;

[Route("/accounts/location", "PUT")]
public class PutWorkspaceAccountLocation : RequestBase, IReturnVoid
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? CapturedAt { get; set; }
}

[Route("/locationmaps", "PUT")]
public class PutLocationGeoMap : RequestBase, IReturnVoid
{
    public Address Address { get; set; }
}

// INTERNAL endpoints....

[Route("/internal/addlocationmap", "POST")]
public class AddLocationMap : RequestBase, IReturnVoid, IHasLatitudeLongitude
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool ForceUpdate { get; set; }
    public long ForPublisherAccountId { get; set; }
}
