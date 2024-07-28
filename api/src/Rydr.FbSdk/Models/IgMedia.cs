using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class IgMedia
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "caption")]
    public string Caption { get; set; }

    [DataMember(Name = "media_type")]
    public string MediaType { get; set; }

    [DataMember(Name = "media_url")]
    public string MediaUrl { get; set; }

    [DataMember(Name = "permalink")]
    public string Permalink { get; set; }

    [DataMember(Name = "timestamp")]
    public string Timestamp { get; set; }

    [DataMember(Name = "thumbnail_url")]
    public string ThumbnailUrl { get; set; }
}
