using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models;

[DataContract]
public class FbIgMediaInsight
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "period")]
    public string Period { get; set; }

    [DataMember(Name = "values")]
    public List<FbInsightValue> Values { get; set; }
}

[DataContract]
public class FbComplexIgMediaInsight
{
    [DataMember(Name = "id")]
    public string Id { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "period")]
    public string Period { get; set; }

    [DataMember(Name = "values")]
    public List<FbComplexInsightValue> Values { get; set; }
}
