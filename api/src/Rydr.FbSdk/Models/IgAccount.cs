using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class IgAccount
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "username")]
    public string UserName { get; set; }

    [DataMember(Name = "account_type")]
    public string AccountType { get; set; }

    [DataMember(Name = "media_count")]
    public long MediaCount { get; set; }
}
