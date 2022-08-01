using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbPagedResult<T>
    {
        [DataMember(Name = "data")]
        public List<T> Data { get; set; }

        [DataMember(Name = "paging")]
        public FbPaging Paging { get; set; }
    }
}
