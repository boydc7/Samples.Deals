using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbLocation
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "city")]
    public string City { get; set; }

    [DataMember(Name = "city_id")]
    public string CityId { get; set; }

    [DataMember(Name = "country_code")]
    public string CountryCode { get; set; }

    [DataMember(Name = "latitude")]
    public double Latitude { get; set; }

    [DataMember(Name = "longitude")]
    public double Longitude { get; set; }

    [DataMember(Name = "located_in")]
    public string ParentLocationId { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "region")]
    public string Region { get; set; }

    [DataMember(Name = "region_id")]
    public string RegionId { get; set; }

    [DataMember(Name = "state")]
    public string State { get; set; }

    [DataMember(Name = "street")]
    public string Street { get; set; }

    [DataMember(Name = "zip")]
    public string PostalCode { get; set; }
}
