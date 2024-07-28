using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbIgMediaComment
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "like_count")]
    public long LikeCount { get; set; }

    //        [DataMember(Name = "hidden")]
    //        public bool Hidden { get; set; }

    [DataMember(Name = "text")]
    public string Text { get; set; }

    [DataMember(Name = "timestamp")]
    public string Timestamp { get; set; }

    [DataMember(Name = "username")]
    public string UserName { get; set; }

    [DataMember(Name = "thumbnail_url")]
    public string ThumbnailUrl { get; set; }
}
