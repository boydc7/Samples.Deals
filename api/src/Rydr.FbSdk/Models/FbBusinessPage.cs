using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbBusinessPage
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "instagram_business_account")]
    public FbId InstagramBusinessAccountId { get; set; }
}
