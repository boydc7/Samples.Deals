using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbIdName : FbId
{
    [DataMember(Name = "name")]
    public string Name { get; set; }
}

[DataContract]
public class FbId
{
    [DataMember(Name = "id")]
    public string Id { get; set; }
}
