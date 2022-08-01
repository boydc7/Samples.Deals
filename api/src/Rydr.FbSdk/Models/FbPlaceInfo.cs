using System.Runtime.Serialization;

namespace Rydr.FbSdk.Models
{
    [DataContract]
    public class FbPlaceInfo
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "about")]
        public string About { get; set; }

        //        [DataMember(Name = "category_list")]
        //        public List<FbIdName> Categories { get; set; }

        [DataMember(Name = "cover")]
        public FbCoverPhoto CoverPhoto { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        //        [DataMember(Name = "is_always_open")]
        //        public bool IsAlwaysOpen { get; set; }

        [DataMember(Name = "is_permanently_closed")]
        public bool IsPermanentlyClosed { get; set; }

        [DataMember(Name = "is_verified")]
        public bool IsVerified { get; set; }

        [DataMember(Name = "link")]
        public string FbUrl { get; set; }

        [DataMember(Name = "location")]
        public FbLocation Location { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "phone")]
        public string Phone { get; set; }

        [DataMember(Name = "single_line_address")]
        public string SingleLineAddress { get; set; }

        [DataMember(Name = "website")]
        public string Website { get; set; }
    }
}
