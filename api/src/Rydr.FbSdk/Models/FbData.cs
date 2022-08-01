using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbData<T>
    {
        [DataMember(Name = "data")]
        public T Data { get; set; }
    }
}
