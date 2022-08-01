using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbAgeRange
    {
        [DataMember(Name = "min")]
        public int Min { get; set; }

        [DataMember(Name = "max")]
        public int Max { get; set; }
    }
}
