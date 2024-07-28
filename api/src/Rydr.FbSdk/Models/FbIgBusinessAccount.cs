using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbIgBusinessAccount
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "followers_count")]
    public long FollowersCount { get; set; }

    [DataMember(Name = "follows_count")]
    public long FollowsCount { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "biography")]
    public string Description { get; set; }

    [DataMember(Name = "ig_id")]
    public string InstagramId { get; set; }

    [DataMember(Name = "media_count")]
    public long MediaCount { get; set; }

    [DataMember(Name = "profile_picture_url")]
    public string ProfilePictureUrl { get; set; }

    [DataMember(Name = "username")]
    public string UserName { get; set; }

    [DataMember(Name = "website")]
    public string Website { get; set; }
}
