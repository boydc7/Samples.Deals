using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbIgMedia
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "like_count")]
    public long LikeCount { get; set; }

    [DataMember(Name = "caption")]
    public string Caption { get; set; }

    [DataMember(Name = "comments_count")]
    public long CommentsCount { get; set; }

    //        [DataMember(Name = "ig_id")]
    //        public string InstagramId { get; set; }

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
