using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbBoolResponse
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }
    }
}
