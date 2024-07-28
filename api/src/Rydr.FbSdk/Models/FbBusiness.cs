using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbBusiness
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "profile_picture_uri")]
    public string ProfilePictureUrl { get; set; }

    [DataMember(Name = "permitted_roles")]
    public List<string> PermittedRoles { get; set; }
}
