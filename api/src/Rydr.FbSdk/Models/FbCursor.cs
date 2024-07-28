using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbCursor
{
    [DataMember(Name = "after")]
    public string After { get; set; }

    [DataMember(Name = "before")]
    public string Before { get; set; }
}
