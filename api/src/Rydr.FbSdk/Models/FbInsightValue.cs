using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    public class FbInsightValue
    {
        [DataMember(Name = "value")]
        public long Value { get; set; }

        [DataMember(Name = "end_time")]
        public string EndTime { get; set; }
    }

    public class FbComplexInsightValue
    {
        [DataMember(Name = "value")]
        public Dictionary<string, long> Value { get; set; }

        [DataMember(Name = "end_time")]
        public string EndTime { get; set; }
    }
}
