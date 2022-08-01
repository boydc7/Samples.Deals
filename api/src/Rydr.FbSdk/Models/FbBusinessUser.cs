using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbBusinessUser
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "assigned_pages")]
        public FbPagedResult<FbBusinessPage> AssignedPages { get; set; }
    }
}
