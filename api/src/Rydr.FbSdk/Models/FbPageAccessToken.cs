using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbPageAccessToken
{
    [DataMember(Name = "access_token")]
    public string AccessToken { get; set; }

    [DataMember(Name = "id")]
    public string Id { get; set; }
}
